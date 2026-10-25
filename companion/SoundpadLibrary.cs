using System.Globalization;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TabletDeck;

public sealed record LibrarySound(int Index,string Name,string Artist,string Duration,string[] Categories,string Key="");
public sealed record SoundpadLibraryResult(IReadOnlyList<LibrarySound> Sounds,string? Warning);
public static class SoundpadLibrary
{
    private static readonly SemaphoreSlim cacheGate=new(1,1);
    private static SoundpadLibraryResult? cached;
    private static DateTime cacheAt;
    internal static XmlReaderSettings ReaderSettings()=>new() {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024,IgnoreComments=true};
    internal static XDocument ParseXml(string xml)
    {
        using var reader=XmlReader.Create(new StringReader(xml.TrimStart('\uFEFF')),ReaderSettings());
        return XDocument.Load(reader);
    }
    internal static SoundpadLibraryResult Parse(string soundXml,string? categoryXml,string? warning=null)
    {
        var sounds=ParseXml(soundXml);
        if(sounds.Root?.Name.LocalName!="Soundlist")throw new InvalidOperationException("Soundpad returned an unsupported sound-list format.");
        var entries=sounds.Root.Elements().Where(e=>e.Name.LocalName=="Sound").Take(5001).ToArray();
        if(entries.Length>5000)throw new InvalidOperationException("This library has more than 5000 sounds. Reduce it before browsing from the board.");
        var categories=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if(categoryXml is not null) {
            var tree=ParseXml(categoryXml);
            foreach(var category in tree.Descendants().Where(e=>e.Name.LocalName=="Category")) {
                var name=Limit((string?)category.Attribute("name")??"",60);if(name.Length==0)continue;
                foreach(var entry in category.Elements().Where(e=>e.Name.LocalName=="Sound")) {
                    var key=(string?)entry.Attribute("url");if(string.IsNullOrEmpty(key))continue;
                    if(!categories.TryGetValue(key,out var values))categories[key]=values=new(StringComparer.OrdinalIgnoreCase);
                    if(values.Count<6)values.Add(name);
                }
            }
        }
        var result=new List<LibrarySound>();var ids=new HashSet<int>();
        foreach(var entry in entries) {
            if(!int.TryParse((string?)entry.Attribute("index"),NumberStyles.None,CultureInfo.InvariantCulture,out var index)||index<1||!ids.Add(index))throw new InvalidOperationException("Soundpad returned invalid or duplicate sound indices. Refresh its library.");
            var url=(string?)entry.Attribute("url")??"";
            var title=((string?)entry.Attribute("title")??"").Trim();
            if(title.Length==0)title=Path.GetFileNameWithoutExtension(url.Replace('/',Path.DirectorySeparatorChar));
            if(title.Length==0)title="Sound "+index.ToString(CultureInfo.InvariantCulture);
            var key=url.Length==0?"":Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(url.Replace('/','\\').ToUpperInvariant())));
            result.Add(new(index,Limit(title,120),Limit((string?)entry.Attribute("artist")??"",80),Limit((string?)entry.Attribute("duration")??"",24),categories.TryGetValue(url,out var tags)?tags.OrderBy(x=>x).ToArray():[],key));
        }
        return new(result,warning);
    }
    private static string Limit(string value,int length)=>value.Length<=length?value:value[..length];
    public static async Task<SoundpadLibraryResult> Get(bool refresh=false,CancellationToken ct=default)
    {
        await cacheGate.WaitAsync(ct);
        try {
            if(!refresh&&cached is not null&&(DateTime.UtcNow-cacheAt).TotalSeconds<30)return cached;
            var soundXml=await Query("GetSoundlist()","sp_remote_control",ct);
            string? categoryXml=null,warning=null;
            try {categoryXml=await Query("GetCategories(true,false)","sp_remote_control",ct);}
            catch(Exception ex) when(ex is IOException or InvalidOperationException){warning="Category browsing is unavailable in this Soundpad session; search still covers all sounds.";}
            cached=Parse(soundXml,categoryXml,warning);cacheAt=DateTime.UtcNow;return cached;
        } finally {cacheGate.Release();}
    }
    internal static async Task<string> Query(string command,string pipeName,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await SoundpadClient.ChannelGate.WaitAsync(timeout.Token);
        try {
            using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(timeout.Token);await pipe.WriteAsync(Encoding.UTF8.GetBytes(command),timeout.Token);await pipe.FlushAsync(timeout.Token);
            using var data=new MemoryStream();var buffer=new byte[8192];
            while(true) {
                var count=await pipe.ReadAsync(buffer,timeout.Token);if(count==0)throw new IOException("Soundpad closed its library connection.");
                data.Write(buffer,0,count);if(data.Length>8*1024*1024)throw new IOException("Soundpad's library response exceeds the 8 MB limit.");
                var text=Encoding.UTF8.GetString(data.GetBuffer(),0,checked((int)data.Length)).TrimEnd('\0');
                if(text.StartsWith('R')&&text.Length>=5)throw new InvalidOperationException("Soundpad library request was rejected ("+text[..5]+").");
                try {ParseXml(text);return text;}catch(XmlException) { /* A complete XML document is its own response boundary. */ }
            }
        } catch(OperationCanceledException) when(!ct.IsCancellationRequested) {throw new IOException("Soundpad's library did not respond. Keep Soundpad running and refresh.");}
        finally {SoundpadClient.ChannelGate.Release();}
    }
}
