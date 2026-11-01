using System.Globalization;

namespace TabletDeck;

internal static class LiveSoundpad
{
    public const string Prefix="live-soundpad:";
    public static DeckAction[] Buttons(DeckPage page,SoundpadLibraryResult? library)=>library?.Sounds.Select(sound=>new DeckAction{
        Id=Prefix+page.Id+":"+sound.Key+":"+sound.Index.ToString(CultureInfo.InvariantCulture),
        Kind="soundpadPlay",Label=sound.Name.Length>60?sound.Name[..60]:sound.Name,
        Target=sound.Index.ToString(CultureInfo.InvariantCulture),SoundpadKey=sound.Key,
        Value=page.LiveSoundpadOutput,Icon="speaker",Color="amber"
    }).ToArray()??[];
    public static DeckAction Action(string id,DeckConfig config)
    {
        foreach(var page in config.Pages.Where(p=>p.LiveSoundpad)){
            var prefix=Prefix+page.Id+":";if(!id.StartsWith(prefix,StringComparison.Ordinal))continue;
            var parts=id[prefix.Length..].Split(':');
            if(parts.Length!=2||parts[0].Length!=64||!parts[0].All(Uri.IsHexDigit)||!int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out var index)||index is <1 or >5000)continue;
            // The displayed index is never trusted for playback: DeckEngine resolves the current
            // unique file identity immediately before sending Soundpad a command.
            return new(){Id=id,Kind="soundpadPlay",Label="Play Soundpad sound",Target="1",SoundpadKey=parts[0],Value=page.LiveSoundpadOutput,Icon="speaker",Color="amber"};
        }
        throw new ArgumentException("This live Soundpad button is no longer available. Reload the board.");
    }
}
