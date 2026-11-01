using System.Text.Json;

namespace TabletDeck;
public sealed record RecentSound(string Key,string Name,DateTimeOffset PlayedAt);
public sealed partial class DataStore
{
    private List<RecentSound>? recentSounds;
    public string RecentWarning {get;private set;}="";
    public RecentSound[] Recents(){lock(gate){
        if(recentSounds is null)try{var path=Path.Combine(DirectoryPath,"recent-sounds.json");if(File.Exists(path)&&new FileInfo(path).Length>30000)throw new IOException();recentSounds=Read<List<RecentSound>>("recent-sounds.json")??[];if(recentSounds.Any(s=>s is null||!ValidSoundKey(s.Key)||string.IsNullOrWhiteSpace(s.Name)||s.Name.Length>120))throw new JsonException();recentSounds=recentSounds.GroupBy(s=>s.Key).Select(g=>g.OrderByDescending(s=>s.PlayedAt).First()).OrderByDescending(s=>s.PlayedAt).Take(30).ToList();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){recentSounds=[];RecentWarning="Recent-play history could not be loaded. New successful sounds will start a fresh history.";}
        return recentSounds.ToArray();
    }}
    public void RecordRecent(LibrarySound sound){lock(gate){
        if(!ValidSoundKey(sound.Key))return;
        var list=Recents().Where(s=>s.Key!=sound.Key).Prepend(new RecentSound(sound.Key,sound.Name,DateTimeOffset.UtcNow)).Take(30).ToList();
        try{Write("recent-sounds.json",list);recentSounds=list;RecentWarning="";}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){RecentWarning="Soundpad played, but recent history could not be saved.";}
    }}
    public void ClearRecents(){lock(gate){Write("recent-sounds.json",new List<RecentSound>());recentSounds=[];RecentWarning="";}}
}
