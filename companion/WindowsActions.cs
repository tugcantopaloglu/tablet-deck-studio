using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TabletDeck;

public static class WindowsActions
{
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern int mciSendString(string command,StringBuilder? buffer,int length,IntPtr callback);
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool mciGetErrorString(int error,StringBuilder buffer,int length);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,INPUT[] inputs,int size);
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort key,scan;public uint flags,time;public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int x,y;public uint data,flags,time;public UIntPtr extra; }
    private static readonly object soundGate=new();
    private static readonly List<string> aliases=[];
    public static void PlaySound(string path)
    {
        if(!File.Exists(path)||path.Contains('"')||path.Contains('\r')||path.Contains('\n')) throw new ArgumentException("Choose an existing WAV, MP3, or WMA sound file in Edit on the PC.");
        lock(soundGate) {
            foreach(var old in aliases.ToArray()) { var status=new StringBuilder(64);mciSendString($"status {old} mode",status,64,IntPtr.Zero);if(status.ToString()!="playing") {mciSendString($"close {old}",null,0,IntPtr.Zero);aliases.Remove(old);} }
            if(aliases.Count>=16) throw new InvalidOperationException("16 sounds are already playing. Stop sounds first.");
            var alias="td"+Guid.NewGuid().ToString("N");
            Check(mciSendString($"open \"{path}\" alias {alias}",null,0,IntPtr.Zero));
            try { Check(mciSendString($"play {alias} from 0",null,0,IntPtr.Zero));aliases.Add(alias); }
            catch { mciSendString($"close {alias}",null,0,IntPtr.Zero);throw; }
        }
    }
    private static void Check(int error) { if(error!=0) {var message=new StringBuilder(256);mciGetErrorString(error,message,256);throw new IOException(message.ToString());} }
    public static void StopSounds() {lock(soundGate) {foreach(var alias in aliases) mciSendString($"close {alias}",null,0,IntPtr.Zero);aliases.Clear();} }
    public static void OpenApp(string path)
    {
        if(!Path.IsPathFullyQualified(path)||!File.Exists(path)||!new[]{".exe",".lnk"}.Contains(Path.GetExtension(path).ToLowerInvariant())) throw new ArgumentException("Choose an existing absolute .exe or .lnk path in Edit on the PC.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)??"" });
    }
    public static void OpenUrl(string target)
    {
        if(!Uri.TryCreate(target,UriKind.Absolute,out var uri)||uri.Scheme is not ("https" or "http")) throw new ArgumentException("Only HTTP or HTTPS websites are supported.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute=true });
    }
    public static ushort[] ParseHotkey(string target)
    {
        var parts=target.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length is <1 or >6) throw new ArgumentException("Use a shortcut such as CTRL+SHIFT+M.");
        return parts.Select(x=>{
            var key=x.ToUpperInvariant();
            if(key.Length==1&&char.IsAsciiLetterOrDigit(key[0])) return (ushort)key[0];
            if(key.StartsWith('F')&&int.TryParse(key[1..],out var n)&&n is >=1 and <=24) return (ushort)(0x6F+n);
            return key switch {"CTRL" or "CONTROL"=>0x11,"SHIFT"=>0x10,"ALT"=>0x12,"WIN"=>0x5B,"ENTER"=>0x0D,"TAB"=>0x09,"ESC"=>0x1B,"SPACE"=>0x20,"BACKSPACE"=>0x08,"DELETE"=>0x2E,"LEFT"=>0x25,"UP"=>0x26,"RIGHT"=>0x27,"DOWN"=>0x28,"PLAYPAUSE"=>0xB3,"NEXTTRACK"=>0xB0,"PREVTRACK"=>0xB1,"VOLUMEUP"=>0xAF,"VOLUMEDOWN"=>0xAE,"VOLUMEMUTE"=>0xAD,_=>throw new ArgumentException("Unsupported hotkey key: "+x)};
        }).Select(k=>(ushort)k).ToArray();
    }
    public static void Hotkey(string target)
    {
        var keys=ParseHotkey(target);
        INPUT Key(ushort k,bool up)=>new() {type=1,data=new InputUnion{keyboard=new KEYBDINPUT{key=k,flags=up?2u:0u}}};
        var inputs=keys.Select(k=>Key(k,false)).Concat(keys.Reverse().Select(k=>Key(k,true))).ToArray();
        var sent=SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<INPUT>());
        if(sent!=inputs.Length) {
            var release=keys.Reverse().Select(k=>Key(k,true)).ToArray();SendInput((uint)release.Length,release,Marshal.SizeOf<INPUT>());
            throw new IOException("Windows blocked the shortcut. The target app may be running as administrator.");
        }
    }
    public static void SetKeys(ushort[] keys,bool down)
    {
        var sequence=down?keys:keys.Reverse().ToArray();
        var inputs=sequence.Select(key=>new INPUT{type=1,data=new InputUnion{keyboard=new KEYBDINPUT{key=key,flags=down?0u:2u}}}).ToArray();
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<INPUT>())==inputs.Length)return;
        if(down) {
            var release=keys.Reverse().Select(key=>new INPUT{type=1,data=new InputUnion{keyboard=new KEYBDINPUT{key=key,flags=2}}}).ToArray();
            SendInput((uint)release.Length,release,Marshal.SizeOf<INPUT>());
        }
        throw new IOException("Windows blocked the hold shortcut. Use the same privilege level as the target app.");
    }
}
