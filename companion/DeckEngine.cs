using System.Text.Json.Nodes;
using System.Text.Json;

namespace TabletDeck;

public sealed class DeckEngine
{
    private readonly DataStore store;
    private readonly ObsClient obs;
    public event Action? Invalidated;
    private int generation;
    private long snapshots;
    private readonly string session=Guid.NewGuid().ToString("N");
    public ActionExecution Execution {get;}=new();
    private readonly Func<string,string,string,CancellationToken,Task> soundpadExecute;
    private readonly Func<CancellationToken,Task<SoundpadLibraryResult>> soundpadRead;
    public DeckEngine(DataStore store,ObsClient obs):this(store,obs,SoundpadClient.Execute,SoundpadLibrary.CurrentSounds){}
    internal DeckEngine(DataStore store,ObsClient obs,Func<string,string,string,CancellationToken,Task> soundpadExecute,Func<CancellationToken,Task<SoundpadLibraryResult>> soundpadRead){this.store=store;this.obs=obs;this.soundpadExecute=soundpadExecute;this.soundpadRead=soundpadRead;obs.Changed+=Invalidate;Execution.Changed+=Invalidate;}
    private readonly SemaphoreSlim stateGate=new(1,1);
    private JsonObject? cachedState;
    private DateTime cacheTime=DateTime.MinValue;
    public void Invalidate() {Interlocked.Increment(ref generation);cacheTime=DateTime.MinValue;Invalidated?.Invoke();}
    public async Task<JsonObject> State()
    {
        await stateGate.WaitAsync();
        try {
            if(cachedState!=null&&(DateTime.UtcNow-cacheTime).TotalMilliseconds<900) return WithExecution(cachedState);
            var version=Volatile.Read(ref generation);
            var state=new JsonObject { ["session"]=session,["revision"]=version,["obsConnected"]=false,["scene"]="",["streaming"]=false,["recording"]=false,["inputs"]=new JsonObject() };
            try {
                var sceneTask=obs.Call("GetSceneList");var streamTask=obs.Call("GetStreamStatus");var recordTask=obs.Call("GetRecordStatus");
                async Task<(string Channel,JsonObject Value)> InputState(string channel,string name) {
                    try {
                        if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Choose an OBS input in Edit.");
                        var volume=obs.Call("GetInputVolume",new{inputName=name});var mute=obs.Call("GetInputMute",new{inputName=name});
                        await Task.WhenAll(volume,mute);
                        return(channel,new JsonObject{["name"]=name,["db"]=(await volume)["inputVolumeDb"]?.DeepClone(),["muted"]=(await mute)["inputMuted"]?.DeepClone(),["available"]=true});
                    }catch(Exception ex){return(channel,new JsonObject{["name"]=name,["available"]=false,["error"]=ex.Message});}
                }
                var inputs=Channels().Select(ch=>InputState(ch.Id,ch.Input)).ToArray();
                await Task.WhenAll(inputs.Cast<Task>().Concat([sceneTask,streamTask,recordTask]));
                var scenes=await sceneTask;var stream=await streamTask;var record=await recordTask;
                state["obsConnected"]=true;state["scene"]=scenes["currentProgramSceneName"]?.DeepClone();
                state["streaming"]=stream["outputActive"]?.DeepClone();state["recording"]=record["outputActive"]?.DeepClone();
                state["scenes"]=scenes["scenes"]?.DeepClone();
                foreach(var task in inputs){var input=await task;state["inputs"]![input.Channel]=input.Value;}
            } catch(Exception ex) {state["error"]="OBS unavailable. Enable Tools > WebSocket Server Settings, then check the password in Edit. "+ex.Message;}
            state["sceneCatalogRevision"]=state["obsConnected"]?.GetValue<bool>()==true?LiveScenes.Revision(state["scenes"]):"offline";
            state["revision"]=Interlocked.Increment(ref snapshots);cachedState=state;cacheTime=version==Volatile.Read(ref generation)?DateTime.UtcNow:DateTime.MinValue;return WithExecution(state);
        } finally {stateGate.Release();}
    }
    private JsonObject WithExecution(JsonObject state){var copy=state.DeepClone().AsObject();copy["execution"]=JsonSerializer.SerializeToNode(Execution.Status,DataStore.Json);copy["profileId"]=store.Config.ProfileId;return copy;}
    public static bool NeedsConfirm(DeckAction a)=>a.Confirm||a.Kind is "stream" or "record" or "streamStart" or "streamStop" or "recordStart" or "recordStop"||a.Steps.Any(NeedsConfirm);
    public Task Execute(DeckAction action)=>Execution.Run(action,ct=>Run(action,ct));
    private async Task Run(DeckAction a,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(a.Kind!="macro")Execution.Step(a);
        if(SoundpadClient.Supports(a.Kind)) {
            var target=a.Target;
            LibrarySound? recent=null;
            if(a.Kind=="soundpadPlay"&&a.SoundpadKey.Length>0){var library=await soundpadRead(ct);var index=SoundpadBindings.Resolve(a,library);target=index.ToString(System.Globalization.CultureInfo.InvariantCulture);recent=library.Sounds.Single(s=>s.Index==index);}
            await soundpadExecute(a.Kind,target,a.Value,ct);
            if(recent is not null)store.RecordRecent(recent);
            return;
        }
        switch(a.Kind) {
            case "scene": await obs.Call("SetCurrentProgramScene",new{sceneName=a.Target});break;
            case "liveScene":{
                var list=await obs.Call("GetSceneList",ct:ct);var scene=LiveScenes.Read(list["scenes"]).FirstOrDefault(s=>s.Id==a.Target)??throw new InvalidOperationException("That OBS scene no longer exists. The live page will refresh.");
                await obs.Call("SetCurrentProgramScene",scene.Uuid is {} uuid?new{sceneUuid=uuid}:(object)new{sceneName=scene.Name},ct);break;
            }
            case "preview": await obs.Call("SetCurrentPreviewScene",new{sceneName=a.Target});break;
            case "transition": await obs.Call("TriggerStudioModeTransition");break;
            case "sound": WindowsActions.PlaySound(a.Target);break;
            case "stopSounds": WindowsActions.StopSounds();break;
            case "app": WindowsActions.OpenApp(a.Target);break;
            case "url": WindowsActions.OpenUrl(a.Target);break;
            case "hotkey": WindowsActions.Hotkey(a.Target);break;
            case "holdHotkey": throw new ArgumentException("Press and hold this button on the board.");
            case "voicemeeter": VoicemeeterClient.Execute(a.Target,a.Value);break;
            case "mute": await obs.Call("ToggleInputMute",new{inputName=a.Target});break;
            case "muteOn":case "muteOff":await obs.Call("SetInputMute",new{inputName=a.Target,inputMuted=a.Kind=="muteOn"});break;
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
            case "recordStart":case "recordStop":case "streamStart":case "streamStop":{
                var stream=a.Kind.StartsWith("stream",StringComparison.Ordinal);var start=a.Kind.EndsWith("Start",StringComparison.Ordinal);
                var status=await obs.Call(stream?"GetStreamStatus":"GetRecordStatus");
                if((status["outputActive"]?.GetValue<bool>()??false)!=start)await obs.Call((start?"Start":"Stop")+(stream?"Stream":"Record"));break;
            }
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
    public MixerChannel[] Channels()=>new[]{new MixerChannel("mic","Microphone",store.Config.MicInput),new MixerChannel("desktop","Desktop audio",store.Config.DesktopInput)}.Concat(store.Config.MixerInputs).ToArray();
    public string Input(string channel)=>Channels().FirstOrDefault(ch=>ch.Id==channel)?.Input??throw new ArgumentException("Unknown audio channel.");
    public async Task Volume(string channel,double db)
    {
        if(!double.IsFinite(db)||db is < -60 or > 0) throw new ArgumentException("Volume must be between -60 and 0 dB.");
        await obs.Call("SetInputVolume",new{inputName=Input(channel),inputVolumeDb=db});Invalidate();
    }
    public async Task Mute(string channel) {await obs.Call("ToggleInputMute",new{inputName=Input(channel)});Invalidate();}
}
