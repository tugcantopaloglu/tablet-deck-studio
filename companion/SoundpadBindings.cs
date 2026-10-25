using System.Globalization;

namespace TabletDeck;

public static class SoundpadBindings
{
    public static int Resolve(DeckAction action,SoundpadLibraryResult library)
    {
        var matches=library.Sounds.Where(sound=>sound.Key.Length>0&&sound.Key==action.SoundpadKey).ToArray();
        if(matches.Length!=1)throw new InvalidOperationException("This linked Soundpad sound is missing or duplicated. Reimport it from the library; playback was stopped to avoid playing the wrong sound.");
        return matches[0].Index;
    }
    public static object Remap(DataStore store,SoundpadLibraryResult library,bool linkLegacy)
    {
        var linked=0;var updated=0;var missing=0;
        void Visit(DeckAction action) {
            foreach(var step in action.Steps)Visit(step);
            if(action.Kind!="soundpadPlay")return;
            if(action.SoundpadKey.Length==0&&linkLegacy&&int.TryParse(action.Target,out var index)) {
                var sound=library.Sounds.SingleOrDefault(sound=>sound.Index==index);
                if(sound?.Key.Length==64){action.SoundpadKey=sound.Key;linked++;}
            }
            if(action.SoundpadKey.Length==0)return;
            try {var target=Resolve(action,library).ToString(CultureInfo.InvariantCulture);if(target!=action.Target){action.Target=target;updated++;}}
            catch(InvalidOperationException){missing++;}
        }
        store.Modify(config=>{foreach(var action in config.Pages.SelectMany(page=>page.Buttons))Visit(action);});
        return new{linked,updated,missing};
    }
}
