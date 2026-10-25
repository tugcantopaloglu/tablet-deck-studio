using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TabletDeck;

public sealed class DeckAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "New button";
    public string Kind { get; set; } = "scene";
    public string Target { get; set; } = "";
    public string Value { get; set; } = "";
    public string Icon { get; set; } = "monitor";
    public string Color { get; set; } = "violet";
    public bool Confirm { get; set; }
    public List<DeckAction> Steps { get; set; } = [];
}
public sealed class DeckPage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New page";
    public string Icon { get; set; } = "grid";
    public List<DeckAction> Buttons { get; set; } = [];
}
public sealed class DeckConfig
{
    public int ObsPort { get; set; } = 4455;
    public string ObsPassword { get; set; } = "";
    public string MicInput { get; set; } = "Mic/Aux";
    public string DesktopInput { get; set; } = "Desktop Audio";
    public List<DeckPage> Pages { get; set; } = [];
    public static DeckConfig Defaults()
    {
        DeckAction A(string label, string kind, string target, string icon, string color, bool confirm = false) =>
            new() { Label = label, Kind = kind, Target = target, Icon = icon, Color = color, Confirm = confirm };
        var main = new DeckPage { Name = "Main", Icon = "home", Buttons = [
            A("Gameplay", "scene", "Gameplay", "gamepad", "violet"),
            A("Just chatting", "scene", "Just chatting", "chat", "violet"),
            A("Be right back", "scene", "Be right back", "bed", "violet"),
            A("Starting", "scene", "Starting", "clapper", "violet"),
            A("Applause", "sound", Path.Combine(AppContext.BaseDirectory,"Sounds","applause.wav"), "clap", "amber"),
            A("Air horn", "sound", Path.Combine(AppContext.BaseDirectory,"Sounds","air-horn.wav"), "horn", "amber"),
            A("Stop sounds", "stopSounds", "", "stop", "amber"),
            A("Save replay", "replay", "", "replay", "amber"),
            A("Open OBS", "app", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"), "obs", "blue"),
            A("Open Discord", "app", FindDiscord(), "discord", "blue"),
            A("Record", "record", "", "record", "red", true),
            A("Start stream", "stream", "", "broadcast", "red", true)
        ] };
        return new DeckConfig { Pages = [main,
            new() { Name="Scenes", Icon="image", Buttons=main.Buttons.Take(4).Select(a=>A(a.Label,a.Kind,a.Target,a.Icon,a.Color)).ToList() },
            new() { Name="Sounds", Icon="speaker", Buttons=main.Buttons.Skip(4).Take(3).Select(a=>A(a.Label,a.Kind,a.Target,a.Icon,a.Color)).ToList() },
            new() { Name="Apps", Icon="grid", Buttons=main.Buttons.Skip(8).Take(2).Select(a=>A(a.Label,a.Kind,a.Target,a.Icon,a.Color)).ToList() },
            new() { Name="Macros", Icon="list", Buttons=[] }
        ] };
    }
    private static string FindDiscord()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Discord");
        if(!Directory.Exists(root))return "";
        return Directory.EnumerateDirectories(root,"app-*").OrderByDescending(p=>p,StringComparer.Ordinal).Select(p=>Path.Combine(p,"Discord.exe")).FirstOrDefault(File.Exists)??"";
    }
}
public sealed record PairedDevice(string Id, string Name, string TokenHash, DateTimeOffset Added);
public sealed class DataStore
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, MaxDepth = 24 };
    public string DirectoryPath { get; }
    public DeckConfig Config { get; private set; }
    public List<PairedDevice> Devices { get; private set; }
    private readonly object gate = new();
    public DataStore(string path)
    {
        DirectoryPath = Path.GetFullPath(path);
        Directory.CreateDirectory(DirectoryPath);
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Windows user unavailable");
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.SetOwner(user);
        acl.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(DirectoryPath).SetAccessControl(acl);
        Config = Read<DeckConfig>("config.json") ?? DeckConfig.Defaults();
        Devices = Read<List<PairedDevice>>("devices.json") ?? [];
    }
    private T? Read<T>(string file) => File.Exists(Path.Combine(DirectoryPath,file)) ? JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(DirectoryPath,file)), Json) : default;
    private void Write<T>(string file,T value)
    {
        var dest=Path.Combine(DirectoryPath,file); var temp=dest+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(value,Json)); File.Move(temp,dest,true);
    }
    public void Save(DeckConfig config) { lock(gate) { Validate(config); Write("config.json",config); Config=config; } }
    public void Modify(Action<DeckConfig> change)
    {
        lock(gate) {
            var copy=JsonSerializer.Deserialize<DeckConfig>(JsonSerializer.Serialize(Config,Json),Json)!;
            change(copy);Save(copy);
        }
    }
    public void Reorder(IReadOnlyList<PageOrder> order)
    {
        Modify(c=>{
            if(order.Count!=c.Pages.Count||order.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=c.Pages.Count)throw new ArgumentException("The pages changed. Reload the board before arranging.");
            foreach(var entry in order) {
                var page=c.Pages.FirstOrDefault(p=>p.Id==entry.Id)??throw new ArgumentException("Unknown page.");
                var byId=page.Buttons.ToDictionary(a=>a.Id,StringComparer.Ordinal);
                if(entry.ButtonIds.Length!=byId.Count||entry.ButtonIds.Distinct(StringComparer.Ordinal).Count()!=byId.Count||entry.ButtonIds.Any(id=>!byId.ContainsKey(id)))throw new ArgumentException("Arranging may only reorder existing buttons within their page.");
                page.Buttons=entry.ButtonIds.Select(id=>byId[id]).ToList();
            }
        });
    }
    public void ImportSounds(string pageId,IReadOnlyList<LibrarySound> sounds,string output)
    {
        if(sounds.Count is <1 or >120)throw new ArgumentException("Select between 1 and 120 sounds.");
        foreach(var sound in sounds)SoundpadClient.BuildCommand("soundpadPlay",sound.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),output);
        Modify(c=>{
            var page=c.Pages.FirstOrDefault(p=>p.Id==pageId)??throw new ArgumentException("Select an existing page.");
            foreach(var sound in sounds) {
                var target=sound.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if(page.Buttons.Any(a=>a.Kind=="soundpadPlay"&&a.Target==target&&a.Value==output))continue;
                page.Buttons.Add(new DeckAction{Label=sound.Name.Length>60?sound.Name[..60]:sound.Name,Kind="soundpadPlay",Target=target,Value=output,Icon="speaker",Color="amber"});
            }
        });
    }
    public void AddDevice(PairedDevice device) { lock(gate) { Devices = [..Devices,device]; Write("devices.json",Devices); } }
    public void Revoke(string id) { lock(gate) { Devices=Devices.Where(x=>x.Id!=id).ToList();Write("devices.json",Devices); } }
    public bool Authenticate(string token)
    {
        if(token.Length < 32 || token.Length > 128) return false;
        var hash=SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        lock(gate) return Devices.Any(x=>CryptographicOperations.FixedTimeEquals(hash,Convert.FromHexString(x.TokenHash)));
    }
    public static void Validate(DeckConfig c)
    {
        if(c.ObsPort is <1 or >65535 || c.Pages.Count is <1 or >30 || c.ObsPassword.Length>1024) throw new ArgumentException("Invalid settings or page count (1–30).");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach(var page in c.Pages)
        {
            if(string.IsNullOrWhiteSpace(page.Name)||page.Name.Length>30||page.Buttons.Count>120) throw new ArgumentException("Each page needs a name and at most 120 buttons.");
            foreach(var action in page.Buttons)
            {
                if(string.IsNullOrWhiteSpace(action.Id)||!ids.Add(action.Id)) throw new ArgumentException("Button IDs must be unique.");
                ValidateAction(action,0);
            }
        }
    }
    private static void ValidateAction(DeckAction a,int depth)
    {
        string[] kinds=["scene","preview","transition","sound","stopSounds","app","url","hotkey","mute","source","filter","media","record","stream","replay","virtualCamera","studioMode","macro","delay"];
        if(depth>3||(!kinds.Contains(a.Kind)&&!SoundpadClient.Supports(a.Kind))||a.Label.Length is <1 or >60||a.Target.Length>2048||a.Value.Length>1024||a.Steps.Count>30) throw new ArgumentException("Invalid button or macro (maximum 30 steps, 3 nesting levels).");
        if(SoundpadClient.Supports(a.Kind)&&!(a.Kind=="soundpadPlay"&&a.Target.Length==0)) SoundpadClient.BuildCommand(a.Kind,a.Target,a.Value);
        if(a.Kind=="app" && a.Target.Length>0 && (!Path.IsPathFullyQualified(a.Target)||!new[]{".exe",".lnk"}.Contains(Path.GetExtension(a.Target).ToLowerInvariant()))) throw new ArgumentException("App actions require an absolute .exe or .lnk path.");
        if(a.Kind=="url"&&a.Target.Length>0&&(!Uri.TryCreate(a.Target,UriKind.Absolute,out var u)||u.Scheme is not ("https" or "http"))) throw new ArgumentException("Website actions require http:// or https://.");
        if(a.Kind=="sound"&&a.Target.Length>0&&(!Path.IsPathFullyQualified(a.Target)||!new[]{".wav",".mp3",".wma"}.Contains(Path.GetExtension(a.Target).ToLowerInvariant()))) throw new ArgumentException("Sound actions require an absolute WAV, MP3, or WMA file path.");
        foreach(var s in a.Steps) ValidateAction(s,depth+1);
    }
}
public sealed record PageOrder(string Id,string[] ButtonIds);
