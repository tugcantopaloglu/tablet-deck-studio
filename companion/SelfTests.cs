using System.Security.Cryptography;
using System.Text;
using System.IO.Pipes;

namespace TabletDeck;
public static class SelfTests
{
    public static void Run()
    {
        var passed=0;
        void Check(bool condition,string label) {if(!condition) throw new Exception("FAILED: "+label);passed++;Console.WriteLine("PASS "+label);}
        void Reject(Action a,string label) {try {a();throw new Exception("FAILED: accepted "+label);}catch(ArgumentException) {passed++;Console.WriteLine("PASS rejects "+label);} }
        var c=DeckConfig.Defaults();DataStore.Validate(c);
        Check(c.Pages.Count==5&&c.Pages[0].Buttons.Count==12,"Studio defaults: 12 keys, 5 pages");
        Check(c.Pages.SelectMany(p=>p.Buttons).Where(a=>a.Kind=="sound").All(a=>File.Exists(a.Target)),"included demo sounds exist beside the executable");
        c.Pages[0].Buttons[0].Kind="shell";Reject(()=>DataStore.Validate(c),"arbitrary shell action");c=DeckConfig.Defaults();
        c.Pages[0].Buttons[8].Target="cmd.exe";Reject(()=>DataStore.Validate(c),"relative app executable");
        c=DeckConfig.Defaults();c.Pages[0].Buttons[0].Id=c.Pages[0].Buttons[1].Id;Reject(()=>DataStore.Validate(c),"duplicate action IDs");
        Check(WindowsActions.ParseHotkey("CTRL+SHIFT+M").SequenceEqual(new ushort[]{17,16,77}),"hotkey key mapping");
        Reject(()=>WindowsActions.ParseHotkey("CTRL+BADKEY"),"invalid shortcut");
        c=DeckConfig.Defaults();c.Pages[0].Buttons[0].Kind="url";c.Pages[0].Buttons[0].Target="file:///C:/Windows/system32/cmd.exe";Reject(()=>DataStore.Validate(c),"file protocol URL");
        var macro=new DeckAction {Kind="macro",Steps=[new(){Kind="stream"}]};
        Check(DeckEngine.NeedsConfirm(macro),"macro inherits stream confirmation");
        var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));var salt="salt";var challenge="challenge";
        var expected=Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password+salt)))+challenge)));
        Check(ObsClient.Auth(password,salt,challenge)==expected,"OBS v5 authentication challenge");
        Check(SoundpadClient.BuildCommand("soundpadPlay","7","mic")=="DoPlaySound(7,false,true)","Soundpad routes sound to microphone");
        Check(SoundpadClient.BuildCommand("soundpadPlay","2","both")=="DoPlaySound(2,true,true)","Soundpad speakers and microphone routing");
        Check(SoundpadClient.BuildCommand("soundpadPlay","1","")=="DoPlaySound(1)","Soundpad default play mode");
        Check(SoundpadClient.BuildCommand("soundpadStop","","")=="DoStopSound()"&&SoundpadClient.BuildCommand("soundpadPause","","")=="DoTogglePause()"&&SoundpadClient.BuildCommand("soundpadNext","","")=="DoPlayNextSound()"&&SoundpadClient.BuildCommand("soundpadPrevious","","")=="DoPlayPreviousSound()","Soundpad transport controls are allowlisted");
        Reject(()=>SoundpadClient.BuildCommand("soundpadPlay","1);DoStopSound()","mic"),"Soundpad command injection");
        Reject(()=>SoundpadClient.BuildCommand("soundpadPlay","0","mic"),"Soundpad zero index");
        Reject(()=>SoundpadClient.BuildCommand("soundpadPlay","1","bad-output"),"unknown Soundpad output");
        Reject(()=>SoundpadClient.BuildCommand("arbitrary","",""),"unknown Soundpad command");
        TestSoundpadPipe().GetAwaiter().GetResult();Check(true,"Soundpad named-pipe status framing, fragmentation, and rejection");
        var silentPath=Path.Combine(Path.GetTempPath(),"tabletdeck-silence-"+Guid.NewGuid().ToString("N")+".wav");
        try {
            using(var stream=File.Create(silentPath)) using(var writer=new BinaryWriter(stream)) {
                var data=new byte[8820];writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(44100);writer.Write(88200);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(data.Length);writer.Write(data);
            }
            WindowsActions.PlaySound(silentPath);WindowsActions.StopSounds();Check(true,"Windows WAV playback and stop using a silent test file");
        } finally {WindowsActions.StopSounds();File.Delete(silentPath);}
        Console.WriteLine($"{passed} self-tests passed.");
    }
    private static async Task TestSoundpadPipe()
    {
        foreach(var response in new[]{"R-200","R-404"}) {
            var name="tabletdeck-selftest-"+Guid.NewGuid().ToString("N");
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            var command="DoPlaySound(7,false,true)";
            var peer=Task.Run(async()=>{
                await server.WaitForConnectionAsync(timeout.Token);
                var input=new byte[Encoding.UTF8.GetByteCount(command)];await server.ReadExactlyAsync(input,timeout.Token);
                if(Encoding.UTF8.GetString(input)!=command)throw new Exception("Soundpad protocol request mismatch.");
                await server.WriteAsync(Encoding.ASCII.GetBytes(response[..2]),timeout.Token);await server.FlushAsync(timeout.Token);
                await Task.Delay(15,timeout.Token);
                await server.WriteAsync(Encoding.ASCII.GetBytes(response[2..]),timeout.Token);await server.FlushAsync(timeout.Token);
            });
            var rejected=false;
            try {await SoundpadClient.Send(command,name,timeout.Token);}catch(InvalidOperationException){rejected=true;}
            await peer;
            if(rejected!=(response!="R-200"))throw new Exception("Soundpad status handling mismatch.");
        }
    }
}
