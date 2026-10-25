using System.Text.Json;

namespace TabletDeck;

public sealed class BoardProfile
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="New profile";
    public List<DeckPage> Pages {get;set;}=[];
}
public sealed record FavouriteSound(string Key,string Name);
public sealed record ProfileSummary(string Id,string Name,int Pages,int Buttons,bool Active);
public sealed record BackupSummary(string Id,DateTimeOffset Created,string Reason,string ActiveProfile,int Profiles,int Pages,int Buttons,int Favourites);
internal sealed record BoardBackup(string Id,DateTimeOffset Created,string Reason,DeckConfig Config);

public sealed partial class DataStore
{
    private ConfigHistory history=null!;
    public string RecoveryWarning {get;private set;}="";
    public static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Json),Json)!;
    internal static bool ValidSoundKey(string? key)=>key is {Length:64}&&key.All(Uri.IsHexDigit)&&key==key.ToUpperInvariant();
    private void InitializeConfig()
    {
        history=new ConfigHistory(DirectoryPath);
        var path=Path.Combine(DirectoryPath,"config.json");var existed=File.Exists(path);
        try{Config=existed?ReadConfig(path):DeckConfig.Defaults();Validate(Config);}
        catch(Exception ex) when(ex is JsonException or ArgumentException or IOException){
            // Retain the original bytes privately; never replace a damaged file without a copy.
            if(existed)File.Copy(path,Path.Combine(DirectoryPath,"config.corrupt-"+Guid.NewGuid().ToString("N")+".json"));
            Config=history.Latest()??DeckConfig.Defaults();
            RecoveryWarning="The saved board was damaged. "+(history.HasValidBackup?"The latest valid backup was recovered.":"A default board was loaded.")+" The original file was kept privately. Re-enter your OBS password; pairing is unchanged.";
        }
        var migrated=Config.ProfileId.Length==0;
        if(migrated)Config.ProfileId=Guid.NewGuid().ToString("N");
        Validate(Config);
        if(existed&&migrated&&RecoveryWarning.Length==0)history.Capture(Config,"Before profile migration");
        if(!existed||migrated||RecoveryWarning.Length>0)Write("config.json",Config);
    }
    internal static DeckConfig ReadConfig(string path)
    {
        if(new FileInfo(path).Length>1024*1024)throw new ArgumentException("The saved board exceeds 1 MB.");
        return JsonSerializer.Deserialize<DeckConfig>(File.ReadAllText(path),Json)??throw new ArgumentException("The saved board is empty.");
    }
    private void SaveBoard(DeckConfig config,string reason,bool backup=true)
    {
        lock(gate){
            Validate(config);
            if(config.ProfileId.Length==0)config.ProfileId=Config.ProfileId;
            if(JsonSerializer.SerializeToUtf8Bytes(config,Json).Length>1024*1024)throw new ArgumentException("All profiles together must fit within 1 MB.");
            if(JsonSerializer.Serialize(Config,Json)==JsonSerializer.Serialize(config,Json))return;
            if(backup)history.Capture(Config,reason);
            Write("config.json",config);Config=config;
        }
    }
    public void SaveActive(DeckConfig submitted,bool replacePassword)
    {
        lock(gate){
            if(submitted is null||submitted.ProfileId is null)throw new ArgumentException("Invalid board settings.");
            if(submitted.ProfileId.Length>0&&submitted.ProfileId!=Config.ProfileId)throw new InvalidOperationException("The profile changed. Reload Edit before saving.");
            var next=Copy(submitted);next.ProfileId=Config.ProfileId;next.ProfileName=Config.ProfileName;
            next.Profiles=Copy(Config.Profiles);next.Favourites=Copy(Config.Favourites);
            if(!replacePassword)next.ObsPassword=Config.ObsPassword;
            SaveBoard(next,"Before board edit");
        }
    }
    public ProfileSummary[] ProfileList(){lock(gate)return new[]{new ProfileSummary(Config.ProfileId,Config.ProfileName,Config.Pages.Count,Config.Pages.Sum(p=>p.Buttons.Count),true)}.Concat(Config.Profiles.Select(p=>new ProfileSummary(p.Id,p.Name,p.Pages.Count,p.Pages.Sum(page=>page.Buttons.Count),false))).ToArray();}
    public string CreateProfile(string name,bool duplicate)
    {
        lock(gate){
            if(Config.Profiles.Count>=9)throw new ArgumentException("Keep at most 10 profiles.");
            var next=Copy(Config);var pages=duplicate?Copy(next.Pages):new List<DeckPage>{new(){Name="Main",Icon="home"}};
            void NewIds(DeckAction a){a.Id=Guid.NewGuid().ToString("N");foreach(var step in a.Steps)NewIds(step);}
            foreach(var page in pages){page.Id=Guid.NewGuid().ToString("N");foreach(var action in page.Buttons)NewIds(action);}
            var profile=new BoardProfile{Name=ProfileName(name),Pages=pages};next.Profiles.Add(profile);SaveBoard(next,"Before profile creation");return profile.Id;
        }
    }
    public void RenameProfile(string id,string name)
    {
        lock(gate){var next=Copy(Config);if(next.ProfileId==id)next.ProfileName=ProfileName(name);else (next.Profiles.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("Unknown profile.")).Name=ProfileName(name);SaveBoard(next,"Before profile rename");}
    }
    private static string ProfileName(string? name)=>!string.IsNullOrWhiteSpace(name)&&name.Trim().Length<=30?name.Trim():throw new ArgumentException("Profile names need 1–30 characters.");
    public void DeleteProfile(string id)
    {
        lock(gate){if(id==Config.ProfileId)throw new ArgumentException("Switch to another profile before deleting this one.");var next=Copy(Config);if(next.Profiles.RemoveAll(p=>p.Id==id)!=1)throw new ArgumentException("Unknown profile.");SaveBoard(next,"Before profile deletion");}
    }
    public void SwitchProfile(string id)
    {
        lock(gate){
            if(id==Config.ProfileId)return;
            var next=Copy(Config);var profile=next.Profiles.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("Unknown profile.");next.Profiles.Remove(profile);
            next.Profiles.Add(new(){Id=next.ProfileId,Name=next.ProfileName,Pages=next.Pages});next.ProfileId=profile.Id;next.ProfileName=profile.Name;next.Pages=profile.Pages;
            SaveBoard(next,"Profile switch",false);
        }
    }
    public FavouriteSound[] Favourites(){lock(gate)return Config.Favourites.ToArray();}
    public void Favourite(string key,bool selected,SoundpadLibraryResult? library=null)
    {
        if(!ValidSoundKey(key))throw new ArgumentException("Choose a sound with a stable library identity.");
        lock(gate){
            var next=Copy(Config);next.Favourites.RemoveAll(s=>s.Key==key);
            if(selected){var matches=library?.Sounds.Where(s=>s.Key==key).ToArray();if(matches?.Length!=1)throw new ArgumentException("This sound is missing or duplicated. Refresh the library.");next.Favourites.Add(new(key,matches[0].Name));}
            SaveBoard(next,"Before favourite change");
        }
    }
    public BackupSummary[] Backups(){lock(gate)return history.List();}
    public BackupSummary BackupNow(){lock(gate)return history.Capture(Config,"Manual backup");}
    public object PreviewBackup(string id){lock(gate){var backup=history.Load(id);return new{backup=ConfigHistory.Summary(backup),profiles=ProfileSummaries(backup.Config),current=ProfileList(),credentialsPreserved=true};}}
    private static ProfileSummary[] ProfileSummaries(DeckConfig c)=>new[]{new ProfileSummary(c.ProfileId,c.ProfileName,c.Pages.Count,c.Pages.Sum(p=>p.Buttons.Count),true)}.Concat(c.Profiles.Select(p=>new ProfileSummary(p.Id,p.Name,p.Pages.Count,p.Pages.Sum(page=>page.Buttons.Count),false))).ToArray();
    public void RestoreBackup(string id)
    {
        lock(gate){var next=Copy(history.Load(id).Config);next.ObsPassword=Config.ObsPassword;SaveBoard(next,"Before backup restore");}
    }
}

internal sealed class ConfigHistory
{
    private readonly string directory;
    public bool HasValidBackup {get;private set;}
    public ConfigHistory(string root){directory=Path.Combine(root,"backups");Directory.CreateDirectory(directory);}
    private string FilePath(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid backup identity.");return Path.Combine(directory,id+".json");}
    public BoardBackup Load(string id)
    {
        var path=FilePath(id);if(!File.Exists(path))throw new ArgumentException("That backup is no longer available. Refresh backups.");
        if(new FileInfo(path).Length>2*1024*1024)throw new ArgumentException("Backup exceeds its size limit.");
        var backup=JsonSerializer.Deserialize<BoardBackup>(File.ReadAllText(path),DataStore.Json)??throw new ArgumentException("Invalid backup.");
        if(backup.Id!=id||backup.Config is null)throw new ArgumentException("Invalid backup contents.");
        DataStore.Validate(backup.Config);
        if(backup.Config.ObsPassword.Length>0)throw new ArgumentException("Backups must not contain credentials.");
        return backup;
    }
    private BoardBackup[] Valid()
    {
        var result=new List<BoardBackup>();
        foreach(var path in Directory.EnumerateFiles(directory,"*.json")){
            try{result.Add(Load(Path.GetFileNameWithoutExtension(path)));}catch(Exception ex) when(ex is JsonException or ArgumentException or IOException){/* A damaged snapshot cannot replace a valid board. */}
        }
        var sorted=result.OrderByDescending(b=>b.Created).ThenByDescending(b=>b.Id,StringComparer.Ordinal).ToArray();HasValidBackup=sorted.Length>0;return sorted;
    }
    public DeckConfig? Latest()=>Valid().FirstOrDefault()?.Config;
    public BackupSummary[] List()=>Valid().Select(Summary).ToArray();
    public static BackupSummary Summary(BoardBackup b)=>new(b.Id,b.Created,b.Reason,b.Config.ProfileName,b.Config.Profiles.Count+1,b.Config.Pages.Count+b.Config.Profiles.Sum(p=>p.Pages.Count),b.Config.Pages.Sum(p=>p.Buttons.Count)+b.Config.Profiles.Sum(p=>p.Pages.Sum(page=>page.Buttons.Count)),b.Config.Favourites.Count);
    public BackupSummary Capture(DeckConfig config,string reason)
    {
        var safe=DataStore.Copy(config);safe.ObsPassword="";DataStore.Validate(safe);
        var backup=new BoardBackup(Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,reason,safe);var path=FilePath(backup.Id);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(backup,DataStore.Json));File.Move(path+".tmp",path);
        foreach(var old in Valid().Skip(20))File.Delete(FilePath(old.Id));
        return Summary(backup);
    }
}
