using TabletDeck;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Globalization;

if(args.Contains("--self-test")) { SelfTests.Run();return; }
if(args.Contains("--audio-smoke-test")) {await SelfTests.AudioSmokeTest();return;}
string Option(string name,string fallback) {var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
var dataDir=Option("--data-dir",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TabletDeck"));
var store=new DataStore(dataDir);
var httpPort=int.Parse(Option("--http-port","8740"),CultureInfo.InvariantCulture);
var httpsPort=int.Parse(Option("--https-port","8743"),CultureInfo.InvariantCulture);
var certPath=Path.Combine(store.DirectoryPath,"companion.pfx");
X509Certificate2 cert;
if(File.Exists(certPath)) cert=X509CertificateLoader.LoadPkcs12FromFile(certPath,null,X509KeyStorageFlags.UserKeySet);
else {
    using var rsa=RSA.Create(3072);
    var request=new CertificateRequest("CN=Tablet Deck local companion",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
    var usages=new System.Security.Cryptography.OidCollection {new Oid("1.3.6.1.5.5.7.3.1")};request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages,true));
    using var made=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(5));
    File.WriteAllBytes(certPath,made.Export(X509ContentType.Pfx));
    cert=X509CertificateLoader.LoadPkcs12FromFile(certPath,null,X509KeyStorageFlags.UserKeySet);
}
var fingerprint=Convert.ToHexString(SHA256.HashData(cert.RawData));
var adminToken=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
var pairing=new Pairing();
using var singleton=AcquireLock(Path.Combine(store.DirectoryPath,"runtime.lock"));
if(singleton is null) {Console.Error.WriteLine("Tablet Deck is already running. Close the existing companion before starting another.");return;}
var builder=WebApplication.CreateBuilder(new WebApplicationOptions {Args=[],ContentRootPath=AppContext.BaseDirectory,WebRootPath=Path.Combine(AppContext.BaseDirectory,"wwwroot")});
builder.Logging.ClearProviders();builder.Logging.AddSimpleConsole(o=>o.SingleLine=true);builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k=>{k.Limits.MaxRequestBodySize=1024*1024;k.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);k.Listen(IPAddress.Loopback,httpPort);k.Listen(IPAddress.Any,httpsPort,l=>l.UseHttps(cert));});
var app=builder.Build();
await using var obs=new ObsClient(()=>store.Config);
var engine=new DeckEngine(store,obs);
using var appMixer=new AppAudioMixer();
bool IsAdmin(HttpContext c)=>c.Connection.LocalPort==httpPort&&IPAddress.IsLoopback(c.Connection.RemoteIpAddress??IPAddress.None)&&SafeEqual(c.Request.Headers["X-Deck-Admin"].ToString(),adminToken);
bool IsAuthorized(HttpContext c)=>IsAdmin(c)||store.Authenticate(c.Request.Headers.Authorization.ToString().Replace("Bearer ","",StringComparison.Ordinal));
app.Use(async (ctx,next)=>{
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
    ctx.Response.Headers["X-Frame-Options"]="DENY";
    ctx.Response.Headers["Referrer-Policy"]="no-referrer";
    ctx.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    ctx.Response.Headers.CacheControl="no-store";
    if(ctx.Request.Path.StartsWithSegments("/api")) {
        if(ctx.Connection.LocalPort==httpPort&&ctx.Request.Host.Value!=$"127.0.0.1:{httpPort}"&&ctx.Request.Host.Value!=$"localhost:{httpPort}") {ctx.Response.StatusCode=403;return;}
        var origin=ctx.Request.Headers.Origin.ToString();
        if(origin.Length>0&&origin!=$"http://127.0.0.1:{httpPort}"&&origin!=$"http://localhost:{httpPort}") {ctx.Response.StatusCode=403;return;}
        if(ctx.Request.Headers["Sec-Fetch-Site"]=="cross-site") {ctx.Response.StatusCode=403;return;}
        if(ctx.Request.Path.StartsWithSegments("/api/admin")&&!IsAdmin(ctx)) {ctx.Response.StatusCode=403;return;}
        if(ctx.Request.Path!="/api/pair"&&!IsAuthorized(ctx)) {ctx.Response.StatusCode=401;await ctx.Response.WriteAsJsonAsync(new{error="Pair this tablet with the companion first."});return;}
        try {await next(ctx);} catch(Exception ex) when(ex is not OutOfMemoryException) {
            ctx.Response.StatusCode=ex is JsonException or ArgumentException ? 400 : 409;
            await ctx.Response.WriteAsJsonAsync(new {error=ex is OperationCanceledException?"The action timed out. Check OBS and try again.":ex.Message});
        }
    } else {
        if(ctx.Connection.LocalPort!=httpPort) {ctx.Response.StatusCode=404;return;}
        await next(ctx);
    }
});
app.UseDefaultFiles();app.UseStaticFiles();
app.MapGet("/api/board",()=>new {pages=store.Config.Pages.Select(p=>new {p.Id,p.Name,p.Icon,buttons=p.Buttons.Select(a=>new{a.Id,a.Label,a.Kind,a.Icon,a.Color,confirm=DeckEngine.NeedsConfirm(a),target=a.Kind is "scene" or "preview"?a.Target:"",configured=!(a.Kind is "app" or "sound" or "url" or "hotkey" or "soundpadPlay")||a.Target.Length>0})})});
app.MapGet("/api/state",()=>engine.State());
app.MapGet("/api/apps",async ()=>new {apps=await appMixer.List()});
app.MapPost("/api/apps",async (AppAudioRequest req)=>{await appMixer.Set(req.Id,req.Percent,req.Muted);return Results.Ok(new{ok=true});});
app.MapPost("/api/layout",(LayoutRequest req)=>{store.Reorder(req.Pages);return Results.Ok(new{ok=true});});
app.MapGet("/api/soundpad/library",async (HttpContext ctx)=>await SoundpadLibrary.Get(false,ctx.RequestAborted));
app.MapPost("/api/soundpad/library",async (HttpContext ctx)=>await SoundpadLibrary.Get(true,ctx.RequestAborted));
app.MapPost("/api/soundpad/play",async (SoundpadPlayRequest req,HttpContext ctx)=>{
    var library=await SoundpadLibrary.Get(false,ctx.RequestAborted);
    if(!library.Sounds.Any(s=>s.Index==req.Index))throw new ArgumentException("This sound is no longer in the library. Refresh Soundpad.");
    await engine.Execute(new DeckAction{Kind="soundpadPlay",Target=req.Index.ToString(CultureInfo.InvariantCulture),Value=req.Output});
    return Results.Ok(new{ok=true});
});
app.MapPost("/api/action",async (ActionRequest req)=>{
    var action=store.Config.Pages.SelectMany(p=>p.Buttons).FirstOrDefault(a=>a.Id==req.Id)??throw new ArgumentException("This button no longer exists. Reload the board.");
    if(DeckEngine.NeedsConfirm(action)&&!req.Confirmed) return Results.Json(new{error="Confirm this action on the tablet."},statusCode:409);
    await engine.Execute(action);return Results.Ok(new{ok=true});
});
app.MapPost("/api/audio",async (AudioRequest req)=>{await engine.Volume(req.Channel,req.Db);return Results.Ok(new{ok=true});});
app.MapPost("/api/mute",async (MuteRequest req)=>{await engine.Mute(req.Channel);return Results.Ok(new{ok=true});});
app.MapPost("/api/pair",(PairRequest req)=>{
    if(!pairing.Accept(req.Code)) return Results.Json(new{error="Pairing code expired or incorrect. Generate a new one on the PC."},statusCode:403);
    var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var name=(req.Name??"Android tablet").Trim();if(name.Length>80) name=name[..80];
    store.AddDevice(new(Guid.NewGuid().ToString("N"),name,Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))),DateTimeOffset.UtcNow));
    return Results.Ok(new{token,fingerprint,name});
});
app.MapGet("/api/admin/info",()=>new {httpPort,httpsPort,fingerprint,certificateCode=fingerprint[..24],addresses=NetworkAddresses(),dataDirectory=store.DirectoryPath,devices=store.Devices.Select(d=>new{d.Id,d.Name,d.Added})});
app.MapPost("/api/admin/pairing",()=>new{code=pairing.Open(),expiresIn=300,certificateCode=fingerprint[..24]});
app.MapPost("/api/admin/revoke",(RevokeRequest req)=>{store.Revoke(req.Id);return Results.Ok(new{ok=true});});
app.MapGet("/api/admin/config",()=>{
    var result=JsonSerializer.SerializeToNode(store.Config,DataStore.Json)!.AsObject();result["obsPassword"]="";result["passwordConfigured"]=store.Config.ObsPassword.Length>0;return result;
});
app.MapPost("/api/admin/config",(ConfigRequest req)=>{
    if(!req.ReplacePassword) req.Config.ObsPassword=store.Config.ObsPassword;
    store.Save(req.Config);engine.Invalidate();return Results.Ok(new{ok=true});
});
app.MapPost("/api/admin/soundpad/import",async (SoundpadImportRequest req,HttpContext ctx)=>{
    if(req.Indices.Length is <1 or >120)throw new ArgumentException("Select between 1 and 120 sounds.");
    var library=await SoundpadLibrary.Get(false,ctx.RequestAborted);
    var sounds=library.Sounds.ToDictionary(s=>s.Index);
    var selected=req.Indices.Distinct().Select(index=>sounds.TryGetValue(index,out var sound)?sound:throw new ArgumentException("A selected sound is no longer in the library. Refresh Soundpad.")).ToArray();
    store.ImportSounds(req.PageId,selected,req.Output);
    return Results.Ok(new{ok=true});
});
app.MapGet("/api/admin/obs",async ()=>new {scenes=await obs.Call("GetSceneList"),inputs=await obs.Call("GetInputList")});
app.MapPost("/api/admin/import-scenes",async ()=>{
    var list=await obs.Call("GetSceneList");
    var c=JsonSerializer.Deserialize<DeckConfig>(JsonSerializer.Serialize(store.Config,DataStore.Json),DataStore.Json)!;
    var page=c.Pages.FirstOrDefault(p=>p.Name=="Scenes");if(page==null) {page=new DeckPage{Name="Scenes",Icon="clapper"};c.Pages.Add(page);}
    foreach(var scene in list["scenes"]!.AsArray()) {
        var name=scene!["sceneName"]!.GetValue<string>();
        if(!page.Buttons.Any(b=>b.Kind=="scene"&&b.Target==name)) page.Buttons.Add(new DeckAction{Label=name.Length>60?name[..60]:name,Kind="scene",Target=name,Icon="clapper",Color="violet"});
    }
    store.Save(c);return Results.Ok(new{ok=true});
});
app.MapPost("/api/admin/shutdown",(IHostApplicationLifetime lifetime)=>{lifetime.StopApplication();return Results.Ok(new{ok=true});});
var panel=$"http://127.0.0.1:{httpPort}/#admin={adminToken}";
File.WriteAllText(Path.Combine(store.DirectoryPath,"admin-session.json"),JsonSerializer.Serialize(new{token=adminToken,httpPort,httpsPort},DataStore.Json));
app.Lifetime.ApplicationStarted.Register(()=>{
    Console.WriteLine($"Tablet Deck Studio is running. PC editor: http://127.0.0.1:{httpPort}");
    Console.WriteLine($"Tablet encrypted port: {httpsPort}. Keep this window open; Ctrl+C closes the companion.");
    if(!args.Contains("--no-browser")) Process.Start(new ProcessStartInfo(panel){UseShellExecute=true});
});
try {await app.RunAsync();} finally {WindowsActions.StopSounds();cert.Dispose();}
static FileStream? AcquireLock(string path) {try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){return null;}}
static bool SafeEqual(string a,string b)=>a.Length==b.Length&&CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(a),System.Text.Encoding.UTF8.GetBytes(b));
static string[] NetworkAddresses()=>System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address)).Select(a=>a.Address.ToString()).Distinct().ToArray();
record ActionRequest(string Id,bool Confirmed=false);
record AudioRequest(string Channel,double Db);
record MuteRequest(string Channel);
record PairRequest(string Code,string? Name);
record RevokeRequest(string Id);
record ConfigRequest(DeckConfig Config,bool ReplacePassword=false);
record AppAudioRequest(string Id,double? Percent=null,bool? Muted=null);
record LayoutRequest(PageOrder[] Pages);
record SoundpadPlayRequest(int Index,string Output="mic");
record SoundpadImportRequest(string PageId,int[] Indices,string Output="mic");
sealed class Pairing
{
    private readonly object gate=new();private string code="";private DateTime expires=DateTime.MinValue;private int attempts;
    public string Open() {lock(gate) {code=RandomNumberGenerator.GetInt32(100000,1000000).ToString(CultureInfo.InvariantCulture);expires=DateTime.UtcNow.AddMinutes(5);attempts=0;return code;} }
    public bool Accept(string submitted) {lock(gate) {if(DateTime.UtcNow>expires||++attempts>5||submitted!=code) return false;expires=DateTime.MinValue;return true;} }
}
