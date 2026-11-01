using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TabletDeck;
public sealed record AppProfileRule(string Application,string ProfileId);
public sealed record AutoProfileSettings(bool Enabled=false,AppProfileRule[]? Rules=null);
public sealed class AutoProfiles : IDisposable
{
    private readonly string path;
    private readonly Func<string,bool> profileExists,apply;
    private readonly object gate=new();
    private AutoProfileSettings settings=new(false,[]);
    private string candidate="",status="Automatic profiles are off.";
    private DateTimeOffset candidateAt;
    private readonly CancellationTokenSource stop=new();private Task? loop;
    public AutoProfiles(string root,Func<string,bool> profileExists,Func<string,bool> apply){
        path=Path.Combine(root,"auto-profiles.json");this.profileExists=profileExists;this.apply=apply;
        try{if(File.Exists(path)){if(new FileInfo(path).Length>20000)throw new IOException();settings=JsonSerializer.Deserialize<AutoProfileSettings>(File.ReadAllText(path),DataStore.Json)??new(false,[]);Validate(settings);}}catch(Exception ex) when(ex is IOException or JsonException or ArgumentException){settings=new(false,[]);status="Automatic profile settings were damaged; switching is off. Save the rules again.";}
    }
    public object Snapshot(){lock(gate)return new{settings,status};}
    public static void Validate(AutoProfileSettings value){
        if(value.Rules is null||value.Rules.Length>30||value.Rules.Any(r=>r is null||string.IsNullOrWhiteSpace(r.Application)||r.Application.Length>100||!r.Application.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||r.Application.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not (' ' or '.' or '-' or '_'))||!Guid.TryParseExact(r.ProfileId,"N",out _))||value.Rules.Select(r=>r.Application).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=value.Rules.Length)throw new ArgumentException("Choose up to 30 unique executable names, such as obs64.exe, and existing profiles.");
    }
    public void Save(AutoProfileSettings value){Validate(value);lock(gate){if(value.Rules!.Any(r=>!profileExists(r.ProfileId)))throw new ArgumentException("A rule refers to a missing profile.");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,DataStore.Json));File.Move(path+".tmp",path,true);settings=value;candidate="";status=value.Enabled?"Waiting for a matching foreground app.":"Automatic profiles are off.";}}
    internal void Tick(string app,DateTimeOffset now){lock(gate){
        if(!settings.Enabled){candidate="";return;}
        var rule=settings.Rules!.FirstOrDefault(r=>string.Equals(r.Application,app,StringComparison.OrdinalIgnoreCase));
        if(rule is null){candidate="";status="No matching foreground app; keeping the current profile.";return;}
        if(!profileExists(rule.ProfileId)){status="The matching profile was deleted. Update this rule.";return;}
        if(candidate!=rule.ProfileId){candidate=rule.ProfileId;candidateAt=now;status="Waiting for the app to stay in front for two seconds.";return;}
        if(now-candidateAt<TimeSpan.FromSeconds(2))return;
        status=apply(rule.ProfileId)?"Profile selected for "+rule.Application+".":"Switching paused while editing, holding keys, running actions, or after a manual switch.";
    }}
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    private static string Foreground(){var window=GetForegroundWindow();if(window==IntPtr.Zero)return "";GetWindowThreadProcessId(window,out var id);using var process=Process.GetProcessById(checked((int)id));return process.ProcessName+".exe";}
    public void Start()=>loop=Task.Run(async()=>{while(!stop.IsCancellationRequested){try{bool enabled;lock(gate)enabled=settings.Enabled;if(enabled)Tick(Foreground(),DateTimeOffset.UtcNow);await Task.Delay(1000,stop.Token);}catch(OperationCanceledException){break;}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){lock(gate)status="Cannot read the foreground app. Rules will retry automatically.";try{await Task.Delay(1000,stop.Token);}catch(OperationCanceledException){break;}}}});
    public void Dispose(){stop.Cancel();try{loop?.Wait(TimeSpan.FromSeconds(2));}catch(AggregateException){}stop.Dispose();}
}
