using System.Text.Json.Nodes;

namespace TabletDeck;

public sealed class DeckEngine(DataStore store,ObsClient obs)
{
    private readonly SemaphoreSlim stateGate=new(1,1), actionGate=new(1,1);
    private JsonObject? cachedState;
    private DateTime cacheTime=DateTime.MinValue;
    public void Invalidate() { cacheTime=DateTime.MinValue; }
    public async Task<JsonObject> State()
    {
        await stateGate.WaitAsync();
        try {
            if(cachedState!=null&&(DateTime.UtcNow-cacheTime).TotalMilliseconds<900) return cachedState.DeepClone().AsObject();
            var state=new JsonObject { ["obsConnected"]=false,["scene"]="",["streaming"]=false,["recording"]=false,["inputs"]=new JsonObject() };
            try {
                var scenes=await obs.Call("GetSceneList");
                var stream=await obs.Call("GetStreamStatus");
                var record=await obs.Call("GetRecordStatus");
                state["obsConnected"]=true;state["scene"]=scenes["currentProgramSceneName"]?.DeepClone();
                state["streaming"]=stream["outputActive"]?.DeepClone();state["recording"]=record["outputActive"]?.DeepClone();
                state["scenes"]=scenes["scenes"]?.DeepClone();
                foreach(var pair in new[]{("mic",store.Config.MicInput),("desktop",store.Config.DesktopInput)}) {
                    try {
                        if(string.IsNullOrWhiteSpace(pair.Item2)) throw new ArgumentException("Choose an OBS input in Edit.");
                        var vol=await obs.Call("GetInputVolume",new{inputName=pair.Item2});
                        var mute=await obs.Call("GetInputMute",new{inputName=pair.Item2});
                        state["inputs"]![pair.Item1]=new JsonObject { ["name"]=pair.Item2,["db"]=vol["inputVolumeDb"]?.DeepClone(),["muted"]=mute["inputMuted"]?.DeepClone(),["available"]=true };
                    } catch(Exception ex) { state["inputs"]![pair.Item1]=new JsonObject{["name"]=pair.Item2,["available"]=false,["error"]=ex.Message}; }
                }
            } catch(Exception ex) {state["error"]="OBS unavailable. Enable Tools > WebSocket Server Settings, then check the password in Edit. "+ex.Message;}
            cachedState=state;cacheTime=DateTime.UtcNow;return state.DeepClone().AsObject();
        } finally {stateGate.Release();}
    }
    public static bool NeedsConfirm(DeckAction a)=>a.Confirm||a.Kind is "stream" or "record"||a.Steps.Any(NeedsConfirm);
    public async Task Execute(DeckAction action)
    {
        if(!await actionGate.WaitAsync(0)) throw new InvalidOperationException("An action is still running. Wait for it to finish.");
        try {using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));await Run(action,timeout.Token);}
        finally {Invalidate();actionGate.Release();}
    }
    private async Task Run(DeckAction a,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch(a.Kind) {
            case "scene": await obs.Call("SetCurrentProgramScene",new{sceneName=a.Target});break;
            case "preview": await obs.Call("SetCurrentPreviewScene",new{sceneName=a.Target});break;
            case "transition": await obs.Call("TriggerStudioModeTransition");break;
            case "sound": WindowsActions.PlaySound(a.Target);break;
            case "stopSounds": WindowsActions.StopSounds();break;
            case "app": WindowsActions.OpenApp(a.Target);break;
            case "url": WindowsActions.OpenUrl(a.Target);break;
            case "hotkey": WindowsActions.Hotkey(a.Target);break;
            case "mute": await obs.Call("ToggleInputMute",new{inputName=a.Target});break;
            case "source": {
                var id=await obs.Call("GetSceneItemId",new{sceneName=a.Target,sourceName=a.Value});
                var sceneItemId=id["sceneItemId"]!.GetValue<int>();
                var item=await obs.Call("GetSceneItemEnabled",new{sceneName=a.Target,sceneItemId});
                await obs.Call("SetSceneItemEnabled",new{sceneName=a.Target,sceneItemId,sceneItemEnabled=!(item["sceneItemEnabled"]?.GetValue<bool>()??false)});break;
            }
            case "filter": {
                var filter=await obs.Call("GetSourceFilter",new{sourceName=a.Target,filterName=a.Value});
                await obs.Call("SetSourceFilterEnabled",new{sourceName=a.Target,filterName=a.Value,filterEnabled=!(filter["filterEnabled"]?.GetValue<bool>()??false)});break;
            }
            case "media": await obs.Call("TriggerMediaInputAction",new{inputName=a.Target,mediaAction=a.Value=="stop"?"OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP":"OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART"});break;
            case "record": await obs.Call("ToggleRecord");break;
            case "stream": await obs.Call("ToggleStream");break;
            case "replay": await obs.Call("SaveReplayBuffer");break;
            case "virtualCamera": await obs.Call("ToggleVirtualCam");break;
            case "studioMode": {
                var mode=await obs.Call("GetStudioModeEnabled");await obs.Call("SetStudioModeEnabled",new{studioModeEnabled=!(mode["studioModeEnabled"]?.GetValue<bool>()??false)});break;
            }
            case "delay": if(!int.TryParse(a.Target,out var delay)||delay<0||delay>10000) throw new ArgumentException("Delay must be 0–10000 milliseconds.");else await Task.Delay(delay,ct);break;
            case "macro": foreach(var step in a.Steps) {ct.ThrowIfCancellationRequested();await Run(step,ct);}break;
            default: throw new ArgumentException("Unknown action.");
        }
    }
    public string Input(string channel)=>channel switch { "mic"=>store.Config.MicInput,"desktop"=>store.Config.DesktopInput,_=>throw new ArgumentException("Unknown audio channel.") };
    public async Task Volume(string channel,double db)
    {
        if(!double.IsFinite(db)||db is < -60 or > 0) throw new ArgumentException("Volume must be between -60 and 0 dB.");
        await obs.Call("SetInputVolume",new{inputName=Input(channel),inputVolumeDb=db});Invalidate();
    }
    public async Task Mute(string channel) {await obs.Call("ToggleInputMute",new{inputName=Input(channel)});Invalidate();}
}
