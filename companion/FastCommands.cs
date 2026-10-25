using System.Text.Json;

namespace TabletDeck;

// HTTP and persistent TLS commands share the same validation and execution.
internal sealed class FastCommands(DataStore store,DeckEngine engine,HoldKeys holds,AppAudioMixer appMixer)
{
    internal sealed record HoldLease(string Lease,int ExpiresIn=3);
    public void Release(string principal,string lease)=>holds.Release(principal,lease);
    public async Task<object> Action(ActionRequest request)
    {
        var action=store.Config.Pages.SelectMany(page=>page.Buttons).FirstOrDefault(action=>action.Id==request.Id)??throw new ArgumentException("This button no longer exists. Reload the board.");
        if(DeckEngine.NeedsConfirm(action)&&!request.Confirmed)throw new InvalidOperationException("Confirm this action on the tablet.");
        await engine.Execute(action);return new{ok=true};
    }
    public object Hold(HoldRequest request,string principal)
    {
        if(request.Operation=="release"){holds.Release(principal,request.Lease);return new{ok=true};}
        if(request.Operation=="renew"){holds.Renew(principal,request.Lease);return new{ok=true};}
        if(request.Operation!="begin")throw new ArgumentException("Unknown hold operation.");
        var action=store.Config.Pages.SelectMany(page=>page.Buttons).FirstOrDefault(action=>action.Id==request.Id);
        if(action?.Kind!="holdHotkey")throw new ArgumentException("Choose a configured hold shortcut button.");
        return new HoldLease(holds.Begin(principal,action.Target));
    }
    public static bool Supports(string path)=>path is "/api/action" or "/api/audio" or "/api/mute" or "/api/hold" or "/api/apps" or "/api/ping" or "/api/cancel";
    public async Task<object> Execute(string path,JsonElement body,string principal)
    {
        switch(path) {
            case "/api/ping":return new{pong=true};
            case "/api/cancel":return new{cancelled=engine.Execution.Cancel((body.Deserialize<CancelRequest>(DataStore.Json)??throw new ArgumentException("Missing cancellation request.")).Id)};
            case "/api/action":return await Action(body.Deserialize<ActionRequest>(DataStore.Json)??throw new ArgumentException("Missing action."));
            case "/api/hold":return Hold(body.Deserialize<HoldRequest>(DataStore.Json)??throw new ArgumentException("Missing hold request."),principal);
            case "/api/apps":{var request=body.Deserialize<AppAudioRequest>(DataStore.Json)??throw new ArgumentException("Missing app volume request.");await appMixer.Set(request.Id,request.Percent,request.Muted);return new{ok=true};}
            case "/api/audio":{var request=body.Deserialize<AudioRequest>(DataStore.Json)??throw new ArgumentException("Missing audio request.");await engine.Volume(request.Channel,request.Db);return new{ok=true};}
            case "/api/mute":{var request=body.Deserialize<MuteRequest>(DataStore.Json)??throw new ArgumentException("Missing mute request.");await engine.Mute(request.Channel);return new{ok=true};}
            default:throw new ArgumentException("This request is not allowed on the fast control link.");
        }
    }
}
