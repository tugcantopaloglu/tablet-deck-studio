using System.Security.Cryptography;
using Timer=System.Threading.Timer;

namespace TabletDeck;

// A short renewable lease releases keys even if a tablet disappears mid-press.
public sealed class HoldKeys : IDisposable
{
    private readonly object gate=new();
    private readonly Action<ushort[],bool> send;
    private readonly Func<DateTimeOffset> now;
    private readonly Timer timer;
    private string owner="",token="";
    private ushort[] keys=[];
    private DateTimeOffset expires;
    public HoldKeys():this(WindowsActions.SetKeys,()=>DateTimeOffset.UtcNow) { }
    internal HoldKeys(Action<ushort[],bool> send,Func<DateTimeOffset> now)
    {
        this.send=send;this.now=now;
        timer=new Timer(_=>{try{Expire();}catch(IOException){/* Retry key-up while Windows is temporarily blocking input. */}},null,200,200);
    }
    public string Begin(string principal,string shortcut)
    {
        var parsed=WindowsActions.ParseHotkey(shortcut);
        lock(gate) {
            Expire();if(keys.Length>0)throw new InvalidOperationException("Another hold button is pressed. Release it first.");
            send(parsed,true);keys=parsed;owner=principal;token=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));expires=now().AddSeconds(3);return token;
        }
    }
    public void Renew(string principal,string lease)
    {
        lock(gate) {Expire();if(keys.Length==0||principal!=owner||lease!=token)throw new ArgumentException("This hold ended. Release and press again.");expires=now().AddSeconds(3);}
    }
    public void Release(string principal,string lease)
    {
        lock(gate) {if(keys.Length>0&&owner==principal&&token==lease)ReleaseAll();}
    }
    public void ReleaseOwner(string principal){lock(gate){if(owner==principal)ReleaseAll();}}
    internal void Expire(){lock(gate){if(keys.Length>0&&now()>=expires)ReleaseAll();}}
    public void ReleaseAll()
    {
        lock(gate) {
            if(keys.Length==0)return;
            // Retain the lease on failure so the timer can retry key-up.
            send(keys,false);keys=[];owner="";token="";
        }
    }
    public void Dispose(){timer.Dispose();ReleaseAll();}
}
