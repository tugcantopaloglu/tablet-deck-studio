using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace TabletDeck;
public sealed record PackSound(string Id,string File,string Name,long Length,string Sha256);
public sealed record BoardPack(int Version,string Name,List<DeckPage> Pages,List<PackSound> Sounds);
public sealed class BoardPacks(DataStore store)
{
    public const int MaxBytes=64*1024*1024;
    private const int MaxSound=16*1024*1024;
    private sealed record Pending(BoardPack Pack,Dictionary<string,byte[]> Sounds,DateTimeOffset Expires);
    private readonly Dictionary<string,Pending> pending=new();private readonly object gate=new();
    internal static IEnumerable<DeckAction> Actions(IEnumerable<DeckPage> pages){foreach(var page in pages)foreach(var action in page.Buttons)foreach(var item in Walk(action))yield return item;}
    private static IEnumerable<DeckAction> Walk(DeckAction a){yield return a;foreach(var step in a.Steps)foreach(var item in Walk(step))yield return item;}
    private static string Identity(string path)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
    private BoardProfile Profile(string id){var c=DataStore.Copy(store.Config);return id==c.ProfileId?new(){Id=c.ProfileId,Name=c.ProfileName,Pages=c.Pages}:c.Profiles.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("Choose an existing profile.");}
    public object Manifest(string id){var p=Profile(id);return new{profile=p.Name,sounds=Actions(p.Pages).Where(a=>a.Kind=="sound"&&a.Target.Length>0).Select(a=>a.Target).Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>new{id=Identity(path),name=Path.GetFileName(path),path,exists=File.Exists(path),bytes=File.Exists(path)?new FileInfo(path).Length:0}),maxBytes=MaxBytes,maxSoundBytes=MaxSound,note="Only selected local sound files are copied. App paths are cleared. Credentials, inactive profiles and Soundpad's native library are excluded."};}
    public byte[] Export(string id,string[] selected){
        if(selected is null||selected.Length>128)throw new ArgumentException("Choose at most 128 referenced sound files.");
        var p=Profile(id);var paths=Actions(p.Pages).Where(a=>a.Kind=="sound"&&a.Target.Length>0).Select(a=>a.Target).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(Identity,StringComparer.Ordinal);
        if(selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length||selected.Any(key=>!paths.ContainsKey(key)))throw new ArgumentException("Select sound files referenced by this profile.");
        var assets=new Dictionary<string,byte[]>();var byPath=new Dictionary<string,PackSound>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var key in selected){var path=paths[key];var info=new FileInfo(path);if(!info.Exists||info.Length>MaxSound)throw new ArgumentException("Each selected sound must exist and be at most 16 MB.");total+=info.Length;if(total>MaxBytes-1024*1024)throw new ArgumentException("Keep the pack below 64 MB.");
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(stream.Length>MaxSound)throw new ArgumentException("Sound grew beyond its limit.");var bytes=ReadLimited(stream,MaxSound);var hash=Convert.ToHexString(SHA256.HashData(bytes));var extension=Path.GetExtension(path).ToLowerInvariant();if(extension is not (".wav" or ".mp3" or ".wma"))throw new ArgumentException("Only WAV, MP3 and WMA files can be included.");
            var file="sounds/"+hash+extension;assets.TryAdd(file,bytes);byPath[path]=new(hash,file,Path.GetFileName(path),bytes.Length,hash);
        }
        foreach(var a in Actions(p.Pages)){if(a.Kind=="sound")a.Target=byPath.TryGetValue(a.Target,out var asset)?"pack:"+asset.Id:"";if(a.Kind=="app")a.Target="";}
        var pack=new BoardPack(1,p.Name,p.Pages,byPath.Values.DistinctBy(s=>s.Id).ToList());var manifest=JsonSerializer.SerializeToUtf8Bytes(pack,DataStore.Json);if(manifest.Length>1024*1024)throw new ArgumentException("Profile metadata exceeds 1 MB.");
        using var output=new MemoryStream();using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){void Add(string name,byte[] bytes){using var target=zip.CreateEntry(name,CompressionLevel.Fastest).Open();target.Write(bytes);}Add("manifest.json",manifest);foreach(var sound in pack.Sounds)Add(sound.File,assets[sound.File]);}
        if(output.Length>MaxBytes)throw new ArgumentException("Keep the pack below 64 MB.");return output.ToArray();
    }
    internal static byte[] ReadLimited(Stream stream,int max){using var output=new MemoryStream();var buffer=new byte[81920];while(true){var count=stream.Read(buffer,0,buffer.Length);if(count==0)break;if(output.Length+count>max)throw new ArgumentException("File exceeds its permitted size.");output.Write(buffer,0,count);}return output.ToArray();}
    public object Preview(byte[] bytes){
        if(bytes.Length>MaxBytes)throw new ArgumentException("Keep the pack below 64 MB.");
        using var input=new MemoryStream(bytes);using var zip=new ZipArchive(input,ZipArchiveMode.Read);if(zip.Entries.Count is <1 or >129||zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.Ordinal).Count()!=zip.Entries.Count||zip.Entries.Any(e=>(e.ExternalAttributes>>16&0xF000)==0xA000))throw new ArgumentException("Invalid pack entries.");
        var manifest=zip.GetEntry("manifest.json")??throw new ArgumentException("Pack manifest is missing.");if(manifest.Length>1024*1024)throw new ArgumentException("Pack metadata exceeds 1 MB.");
        BoardPack pack;using(var stream=manifest.Open())pack=JsonSerializer.Deserialize<BoardPack>(ReadLimited(stream,1024*1024),DataStore.Json)??throw new ArgumentException("Empty pack.");
        if(pack.Version!=1||pack.Pages is null||pack.Sounds is null||pack.Sounds.Count>128||string.IsNullOrWhiteSpace(pack.Name)||pack.Name.Length>30)throw new ArgumentException("Unsupported board pack.");
        // Validate configuration before walking nested actions. No user-provided paths are extracted.
        var config=new DeckConfig{Pages=DataStore.Copy(pack.Pages)};
        void Prepare(DeckAction a,int depth){if(a is null||a.Steps is null||depth>3)throw new ArgumentException("Invalid pack actions.");if(a.Kind=="sound"&&a.Target?.StartsWith("pack:",StringComparison.Ordinal)==true)a.Target="";foreach(var step in a.Steps)Prepare(step,depth+1);}
        foreach(var page in config.Pages){if(page is null||page.Buttons is null)throw new ArgumentException("Invalid pack page.");foreach(var action in page.Buttons)Prepare(action,0);}DataStore.Validate(config);
        var sounds=new Dictionary<string,byte[]>();long total=0;var allowed=new HashSet<string>(StringComparer.Ordinal){"manifest.json"};
        foreach(var sound in pack.Sounds){
            if(sound is null||!DataStore.ValidSoundKey(sound.Id)||sound.Id!=sound.Sha256||sound.Length is <0 or >MaxSound||sound.Name is null||sound.Name.Length>160||sound.File is null||!(sound.File=="sounds/"+sound.Id+".wav"||sound.File=="sounds/"+sound.Id+".mp3"||sound.File=="sounds/"+sound.Id+".wma")||!allowed.Add(sound.File)||sounds.ContainsKey(sound.Id))throw new ArgumentException("Invalid sound manifest.");
            total+=sound.Length;if(total>MaxBytes-1024*1024)throw new ArgumentException("Pack sound data exceeds its limit.");var entry=zip.GetEntry(sound.File)??throw new ArgumentException("A sound file is missing.");if(entry.Length!=sound.Length)throw new ArgumentException("Sound length mismatch.");
            using var stream=entry.Open();var content=ReadLimited(stream,MaxSound);if(Convert.ToHexString(SHA256.HashData(content))!=sound.Sha256)throw new ArgumentException("Sound checksum mismatch.");sounds.Add(sound.Id,content);
        }
        if(zip.Entries.Any(entry=>!allowed.Contains(entry.FullName)))throw new ArgumentException("Unexpected file in pack; only listed sounds are accepted.");
        var actions=Actions(pack.Pages).ToArray();foreach(var a in actions){if(a.Kind=="app")a.Target="";if(a.Kind=="sound"&&a.Target.Length>0){if(!a.Target.StartsWith("pack:",StringComparison.Ordinal)||!sounds.ContainsKey(a.Target[5..]))throw new ArgumentException("Pack contains an unresolved sound reference.");}}
        var id=Guid.NewGuid().ToString("N");lock(gate){Prune();if(pending.Count>=2)throw new InvalidOperationException("Two packs are already awaiting review. Import one or wait ten minutes.");pending.Add(id,new(pack,sounds,DateTimeOffset.UtcNow.AddMinutes(10)));}
        return new{id,name=pack.Name,pages=pack.Pages.Count,buttons=pack.Pages.Sum(p=>p.Buttons.Count),sounds=pack.Sounds.Select(s=>new{s.Name,s.Length,s.Sha256}),actionKinds=actions.GroupBy(a=>a.Kind).Select(g=>new{kind=g.Key,count=g.Count()}),warning="Import only packs you trust. Imported buttons may send hotkeys, open websites, or control OBS. App paths are cleared, unbundled sounds need configuring, and Soundpad sounds must exist locally. No action runs during import."};
    }
    private void Prune(){foreach(var id in pending.Where(p=>p.Value.Expires<DateTimeOffset.UtcNow).Select(p=>p.Key).ToArray())pending.Remove(id);}
    public string Import(string id,string name){lock(gate){
        Prune();if(id is null||!pending.TryGetValue(id,out var item))throw new ArgumentException("That preview expired. Choose the pack again.");if(store.ProfileList().Length>=10)throw new ArgumentException("Delete a profile before importing; the limit is 10.");
        var root=Path.GetFullPath(Path.Combine(store.DirectoryPath,"pack-assets"));var target=Path.GetFullPath(Path.Combine(root,Guid.NewGuid().ToString("N")));if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid pack asset directory.");Directory.CreateDirectory(target);
        try{var pages=DataStore.Copy(item.Pack.Pages);foreach(var sound in item.Pack.Sounds)File.WriteAllBytes(Path.Combine(target,Path.GetFileName(sound.File)),item.Sounds[sound.Id]);foreach(var a in Actions(pages))if(a.Kind=="sound"&&a.Target.StartsWith("pack:",StringComparison.Ordinal)){var sound=item.Pack.Sounds.Single(s=>s.Id==a.Target[5..]);a.Target=Path.Combine(target,Path.GetFileName(sound.File));}
            var profile=store.ImportProfile(name,pages);pending.Remove(id);return profile;
        }catch{foreach(var file in Directory.EnumerateFiles(target))File.Delete(file);Directory.Delete(target);throw;}
    }}
}
