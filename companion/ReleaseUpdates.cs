using System.Net.Http.Headers;
using System.Text.Json;

namespace TabletDeck;

public sealed record ReleaseStatus(string CurrentVersion,string? LatestVersion,bool Available,string Url,string? Error=null);
public sealed class ReleaseUpdates : IDisposable
{
    public const string ReleasesUrl="https://github.com/tugcantopaloglu/tablet-deck-studio/releases";
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly string version;
    private ReleaseStatus status;
    private DateTimeOffset checkedAt;
    public event Action<ReleaseStatus>? Found;
    public ReleaseUpdates():this(new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}),typeof(ReleaseUpdates).Assembly.GetName().Version?.ToString(3)??"1.3.0") { }
    internal ReleaseUpdates(HttpClient client,string version){this.client=client;this.version=version;status=new(version,null,false,ReleasesUrl);client.Timeout=TimeSpan.FromSeconds(8);client.DefaultRequestHeaders.UserAgent.ParseAdd("TabletDeck-Studio/"+version);client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));}
    public ReleaseStatus Status=>Volatile.Read(ref status);
    internal static ReleaseStatus Parse(string json,string current)
    {
        using var document=JsonDocument.Parse(json);var release=document.RootElement;
        var tag=release.GetProperty("tag_name").GetString()??"";
        if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean()||!Version.TryParse(tag.TrimStart('v'),out var latest))throw new IOException("No supported stable release was returned.");
        return new(current,latest.ToString(),latest>Version.Parse(current),ReleasesUrl+"/tag/"+Uri.EscapeDataString(tag));
    }
    public async Task<ReleaseStatus> Check(bool force=false,CancellationToken ct=default)
    {
        await gate.WaitAsync(ct);
        try {
            if(!force&&DateTimeOffset.UtcNow-checkedAt<TimeSpan.FromMinutes(15))return Status;
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(8));ct=deadline.Token;
            try {
                using var response=await client.GetAsync("https://api.github.com/repos/tugcantopaloglu/tablet-deck-studio/releases/latest",HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
                await using var stream=await response.Content.ReadAsStreamAsync(ct);using var bytes=new MemoryStream();var buffer=new byte[8192];int count;
                while((count=await stream.ReadAsync(buffer,ct))>0){bytes.Write(buffer,0,count);if(bytes.Length>256*1024)throw new IOException("Release response exceeds the size limit.");}
                var next=Parse(System.Text.Encoding.UTF8.GetString(bytes.ToArray()),version);var notify=next.Available&&next.LatestVersion!=Status.LatestVersion;
                Volatile.Write(ref status,next);if(notify)Found?.Invoke(next);
            }catch(Exception ex) when(ex is HttpRequestException or IOException or JsonException or OperationCanceledException or KeyNotFoundException or InvalidOperationException){Volatile.Write(ref status,new(version,null,false,ReleasesUrl,"Update check failed. Check your network and try again."));}
            checkedAt=DateTimeOffset.UtcNow;return Status;
        }finally{gate.Release();}
    }
    public void Dispose(){client.Dispose();gate.Dispose();}
}
