using System.Globalization;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace TabletDeck;

// Uses the documented Soundpad remote-control protocol, not shell commands.
public static class SoundpadClient
{
    public static bool Supports(string kind)=>kind is "soundpadPlay" or "soundpadStop" or "soundpadPause" or "soundpadNext" or "soundpadPrevious";
    public static string BuildCommand(string kind,string target,string output)
    {
        if(kind=="soundpadPlay") {
            if(!int.TryParse(target,NumberStyles.None,CultureInfo.InvariantCulture,out var index)||index<1)
                throw new ArgumentException("Choose a positive Soundpad index from its All sounds category.");
            var number=index.ToString(CultureInfo.InvariantCulture);
            return output switch {
                "" or "default"=>$"DoPlaySound({number})",
                "both"=>$"DoPlaySound({number},true,true)",
                "mic"=>$"DoPlaySound({number},false,true)",
                "speakers"=>$"DoPlaySound({number},true,false)",
                _=>throw new ArgumentException("Soundpad output must be default, both, mic, or speakers.")
            };
        }
        return kind switch {
            "soundpadStop"=>"DoStopSound()",
            "soundpadPause"=>"DoTogglePause()",
            "soundpadNext"=>"DoPlayNextSound()",
            "soundpadPrevious"=>"DoPlayPreviousSound()",
            _=>throw new ArgumentException("Unknown Soundpad control.")
        };
    }
    public static Task Execute(string kind,string target,string output,CancellationToken ct)=>Send(BuildCommand(kind,target,output),"sp_remote_control",ct);
    internal static async Task Send(string command,string pipeName,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification);
        try {
            await pipe.ConnectAsync(timeout.Token);
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(command),timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            // Mutating commands respond with an R-NNN status prefix. Reading exactly
            // that prefix handles fragmentation without waiting for the pipe to close.
            var status=new byte[5];
            await pipe.ReadExactlyAsync(status,timeout.Token);
            var code=Encoding.ASCII.GetString(status);
            if(code!="R-200") throw new InvalidOperationException($"Soundpad rejected this action ({code}). Check its All sounds index and playback setup.");
        } catch(OperationCanceledException) when(!ct.IsCancellationRequested) {
            throw new IOException("Soundpad did not respond. Start Soundpad on this PC under the same Windows account, then try again.");
        } catch(IOException ex) {
            throw new IOException("Cannot communicate with Soundpad. Start it under the same Windows account and check its Remote Control support.",ex);
        }
    }
}
