using System.Security.Cryptography;
using System.Text;

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
        var silentPath=Path.Combine(Path.GetTempPath(),"tabletdeck-silence-"+Guid.NewGuid().ToString("N")+".wav");
        try {
            using(var stream=File.Create(silentPath)) using(var writer=new BinaryWriter(stream)) {
                var data=new byte[8820];writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(44100);writer.Write(88200);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(data.Length);writer.Write(data);
            }
            WindowsActions.PlaySound(silentPath);WindowsActions.StopSounds();Check(true,"Windows WAV playback and stop using a silent test file");
        } finally {WindowsActions.StopSounds();File.Delete(silentPath);}
        Console.WriteLine($"{passed} self-tests passed.");
    }
}
