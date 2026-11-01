using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TabletDeck;

public static class LiveSoundpadTests
{
    public static async Task Run()
    {
        var passed=0;
        void Check(bool value,string message){if(!value)throw new InvalidOperationException("Live Soundpad test: "+message);passed++;}
        void Reject(Action action,string message){var rejected=false;try{action();}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){rejected=true;}Check(rejected,message);}
        async Task RejectAsync(Func<Task> action,string message){var rejected=false;try{await action();}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){rejected=true;}Check(rejected,message);}
        string Key(int index)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fixture-file-"+index)));
        var sounds=Enumerable.Range(1,2007).Select(index=>new LibrarySound(index,"Sound "+index.ToString("D4",CultureInfo.InvariantCulture),"Artist "+index%17,"00:03",["Category "+index%9],Key(index))).ToArray();
        var library=new SoundpadLibraryResult(sounds,null);
        var manual=new DeckAction{Id="manual-kept",Label="Saved manual sound",Kind="soundpadPlay",Target="17",Value="both",SoundpadKey=Key(17),Icon="music",Color="amber"};
        var page=new DeckPage{Id="live-page",Name="Memes",LiveSoundpad=true,LiveSoundpadOutput="mic",Buttons=[manual,..Enumerable.Range(1,39).Select(index=>new DeckAction{Id="manual-kept-"+index,Label="Retained manual "+index,Kind="soundpadPlay",Target=index.ToString(CultureInfo.InvariantCulture),SoundpadKey=Key(index),Value="both"})]};
        var config=new DeckConfig{Pages=[page]};DataStore.Validate(config);
        var raw=JsonSerializer.Serialize(page.Buttons,DataStore.Json);
        var buttons=LiveSoundpad.Buttons(page,library);
        Check(buttons.Length==2007,"the entire 2007-entry library becomes runtime pad buttons");
        Check(buttons.Select(a=>a.Id).Distinct().Count()==2007,"every runtime sound has a unique identity");
        Check(buttons.Select(a=>a.SoundpadKey).SequenceEqual(sounds.Select(s=>s.Key)),"runtime buttons retain stable file identities in source order");
        Check(buttons.All(a=>a.Kind=="soundpadPlay"&&a.Value=="mic"),"runtime sounds use the PC-selected microphone output");
        Check(page.Buttons.Count==40&&JsonSerializer.Serialize(page.Buttons,DataStore.Json)==raw,"large runtime projection preserves all 40 raw saved manual buttons");
        Check(!JsonSerializer.Serialize(config,DataStore.Json).Contains("Sound 2007",StringComparison.Ordinal),"runtime library is not persisted into board configuration");
        var last=buttons[^1];var action=LiveSoundpad.Action(last.Id,config);
        Check(action.SoundpadKey==sounds[^1].Key&&action.Target=="1","runtime action trusts the stable key rather than a client index");
        var reindexed=new SoundpadLibraryResult(sounds.Select((sound,index)=>sound with{Index=2007-index}).ToArray(),null);
        Check(SoundpadBindings.Resolve(action,reindexed)==1,"old runtime button resolves the current index after a full reindex");
        foreach(var output in new[]{"mic","speakers","both"})
        {
            page.LiveSoundpadOutput=output;DataStore.Validate(config);
            Check(LiveSoundpad.Action(last.Id,config).Value==output&&LiveSoundpad.Buttons(page,library).All(a=>a.Value==output),"runtime playback follows PC output "+output);
        }
        page.LiveSoundpadOutput="mic";
        var missing=new SoundpadLibraryResult(sounds[..^1],null);
        var duplicate=new SoundpadLibraryResult([..sounds,sounds[^1] with{Index=2008}],null);
        Reject(()=>SoundpadBindings.Resolve(action,missing),"removed sound never falls back to a stale index");
        Reject(()=>SoundpadBindings.Resolve(action,duplicate),"duplicate file identity refuses ambiguous playback");
        foreach(var invalid in new[]{"live-soundpad:","live-soundpad:other:"+Key(1)+":1","live-soundpad:live-page:"+new string('G',64)+":1","live-soundpad:live-page:"+Key(1)+":0","live-soundpad:live-page:"+Key(1)+":5001","live-soundpad:live-page:"+Key(1)+":1:extra","live-soundpad:live-page:"+Key(1)+":-1","live-soundpad:live-page:"+Key(1)+":1.0"})Reject(()=>LiveSoundpad.Action(invalid,config),"invalid runtime identity rejected: "+invalid[..Math.Min(45,invalid.Length)]);
        var missingIdentity=LiveSoundpad.Buttons(page,new([sounds[0] with{Key=""}],null))[0];
        Reject(()=>LiveSoundpad.Action(missingIdentity.Id,config),"missing file identity cannot become a runtime playback action");
        page.LiveSoundpad=false;Reject(()=>LiveSoundpad.Action(last.Id,config),"disabling the current live page invalidates old runtime buttons");
        page.LiveSoundpad=true;
        var inactive=new BoardProfile{Id=Guid.NewGuid().ToString("N"),Name="Other",Pages=[new(){Id="inactive-live",Name="Other sounds",LiveSoundpad=true,Buttons=[]}]};
        config.Profiles=[inactive];DataStore.Validate(config);
        var inactiveId=LiveSoundpad.Buttons(inactive.Pages[0],library)[0].Id;
        Reject(()=>LiveSoundpad.Action(inactiveId,config),"a live page from another profile cannot play in the current profile");
        var bad=DataStore.Copy(config);bad.Pages[0].LiveSoundpadOutput="default";Reject(()=>DataStore.Validate(bad),"unsupported live playback output rejected");
        bad=DataStore.Copy(config);bad.Pages[0].LiveScenes=true;Reject(()=>DataStore.Validate(bad),"one page cannot follow OBS and Soundpad simultaneously");
        bad=DataStore.Copy(config);bad.Pages.Add(new(){Id="second-live",Name="Second",LiveSoundpad=true});Reject(()=>DataStore.Validate(bad),"one profile cannot have two live Soundpad pages");
        Check(config.Profiles[0].Pages[0].LiveSoundpad&&config.Pages[0].LiveSoundpad,"each independent profile may have its own live Soundpad page");
        Check(LiveSoundpad.Buttons(page,null).Length==0&&LiveSoundpad.Buttons(page,new([],null)).Length==0,"offline and empty libraries have no invented sounds");
        Check(LiveSoundpad.Buttons(page,new([sounds[0] with{Name=new string('X',120)}],null))[0].Label.Length==60,"long source names remain bounded on pad buttons");

        // Exercise the real engine's stable-key resolution while replacing both Soundpad I/O boundaries.
        var directory=Path.Combine(Path.GetTempPath(),"tabletdeck-live-soundpad-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var store=new DataStore(directory);await using var obs=new ObsClient(()=>store.Config);
            SoundpadLibraryResult current=reindexed;var calls=new List<(string Kind,string Index,string Output)>();
            var engine=new DeckEngine(store,obs,(kind,index,output,ct)=>{calls.Add((kind,index,output));return Task.CompletedTask;},ct=>Task.FromResult(current));
            await engine.Execute(action);
            Check(calls.Count==1&&calls[0]==("soundpadPlay","1","mic"),"real engine sends the reindexed stable-key match to injected playback");
            current=missing;await RejectAsync(()=>engine.Execute(action),"real engine refuses a removed sound before playback");
            current=duplicate;await RejectAsync(()=>engine.Execute(action),"real engine refuses duplicate identity before playback");
            Check(calls.Count==1,"missing and ambiguous sounds never reach playback I/O");
            Check(JsonSerializer.Serialize(page.Buttons,DataStore.Json)==raw,"execution and identity changes preserve saved manual sound routing");
        }
        finally
        {
            var absolute=Path.GetFullPath(directory);var temporaryRoot=Path.GetFullPath(Path.GetTempPath());
            if(absolute.StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(absolute).StartsWith("tabletdeck-live-soundpad-",StringComparison.Ordinal)&&Directory.Exists(absolute))Directory.Delete(absolute,true);
        }
        Console.WriteLine($"Live Soundpad: {passed} library, identity, execution and source-guard checks passed; 2007 sounds, no real Soundpad I/O or audio.");
    }
}
