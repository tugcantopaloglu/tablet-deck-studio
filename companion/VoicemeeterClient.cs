using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TabletDeck;

public static class VoicemeeterClient
{
    private static readonly object gate=new();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Login();
    [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Ansi)] private delegate int SetFloat([MarshalAs(UnmanagedType.LPStr)] string parameter,float value);
    private static IntPtr connectedModule;
    private static Login? disconnect;
    private static SetFloat? setParameter;
    public static float Validate(string parameter,string text)
    {
        var match=Regex.Match(parameter,@"^(Strip|Bus)\[[0-7]\]\.(Gain|Mute)$",RegexOptions.CultureInvariant);
        if(!match.Success||!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!float.IsFinite(value))throw new ArgumentException("Use Strip[0].Gain or Bus[0].Mute and a numeric value.");
        if(parameter.EndsWith(".Mute",StringComparison.Ordinal)?value is not (0 or 1):value is < -60 or > 12)throw new ArgumentException("Voicemeeter gain is -60 to +12 dB; mute is 0 (off) or 1 (on).");
        return value;
    }
    public static void Execute(string parameter,string text)
    {
        var value=Validate(parameter,text);
        lock(gate) {
            if(connectedModule!=IntPtr.Zero){Set(parameter,value);return;}
            var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)};
            var path=roots.Select(root=>Path.Combine(root,"VB","Voicemeeter","VoicemeeterRemote64.dll")).FirstOrDefault(File.Exists);
            if(path is null)throw new InvalidOperationException("Install Voicemeeter in its standard Windows Program Files location and start it before using this button.");
            var module=NativeLibrary.Load(path);
            var keep=false;
            try {
                var login=Marshal.GetDelegateForFunctionPointer<Login>(NativeLibrary.GetExport(module,"VBVMR_Login"));
                var logout=Marshal.GetDelegateForFunctionPointer<Login>(NativeLibrary.GetExport(module,"VBVMR_Logout"));
                var result=login();
                if(result<0)throw new IOException("Voicemeeter remote login failed ("+result+").");
                try {
                    if(result==1)throw new InvalidOperationException("Voicemeeter is installed but is not running. Start it and try again.");
                    setParameter=Marshal.GetDelegateForFunctionPointer<SetFloat>(NativeLibrary.GetExport(module,"VBVMR_SetParameterFloat"));
                    connectedModule=module;disconnect=logout;keep=true;
                } finally {if(!keep)logout();}
            } finally {if(!keep)NativeLibrary.Free(module);}
            Set(parameter,value);
        }
    }
    private static void Set(string parameter,float value)
    {
        var result=setParameter!(parameter,value);
        if(result==-2)Shutdown();
        if(result!=0)throw new IOException("Voicemeeter rejected this channel or value ("+result+"). Check that it is running and that your edition supports this index.");
    }
    public static void Shutdown()
    {
        lock(gate){if(connectedModule==IntPtr.Zero)return;try{disconnect?.Invoke();}finally{NativeLibrary.Free(connectedModule);connectedModule=IntPtr.Zero;setParameter=null;disconnect=null;}}
    }
}
