using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TabletDeck;

public sealed record MediaSessionInfo(string Id,string Source,string Kind,string Title,string Artist,string Album,string Artwork,double PositionMs,double DurationMs,bool Playing,bool CanPlay,bool CanPause,bool CanNext,bool CanPrevious,bool CanSeek,string ContextToken);
public sealed record MediaSessionsSnapshot(MediaSessionInfo[] Sessions,string? Error=null,string? Warning=null);
public sealed record MediaSessionActionRequest(string PresetId,string SessionId,string ContextToken,string Operation,double? PositionMs=null);

internal sealed record MediaSessionData(string Title,string Artist,string Album,long TrackRevision,long StartTicks,long EndTicks,long PositionTicks,long MinSeekTicks,long MaxSeekTicks,bool Playing,bool CanPlay,bool CanPause,bool CanNext,bool CanPrevious,bool CanSeek,byte[]? Artwork=null);
internal interface IMediaSessionHandle
{
    object Identity {get;}
    string Source {get;}
    Task<MediaSessionData> Read(bool artwork,CancellationToken ct);
    Task<bool> Operate(string operation,long absoluteTicks,CancellationToken ct);
}
internal interface IMediaSessionPlatform : IDisposable
{
    Task<IReadOnlyList<IMediaSessionHandle>> Sessions(CancellationToken ct);
}

// Every write resolves a selected live session object. There is deliberately no GetCurrentSession,
// foreground shortcut, global media-key, or "try another player" fallback in this component.
public sealed class MediaSessions : IDisposable
{
    internal const int MaxArtworkBytes=96*1024;
    private readonly IMediaSessionPlatform platform;
    private readonly object gate=new();
    private readonly SemaphoreSlim writes=new(1,1);
    private readonly Dictionary<object,Entry> entries=new();
    private MediaSessionsSnapshot? cached;
    private Task<MediaSessionsSnapshot>? inFlight;
    private long captured;
    private bool disposed;
    private sealed class Entry(string source)
    {
        public readonly string Id=Guid.NewGuid().ToString("N"),Source=source;
        public string Fingerprint="",Token=Guid.NewGuid().ToString("N");
    }
    public MediaSessions():this(new WindowsMediaSessionPlatform()){}
    internal MediaSessions(IMediaSessionPlatform platform){this.platform=platform;}
    private static string Text(string? value,int maximum)=>new((value??"").Where(c=>!char.IsControl(c)).Take(maximum).ToArray());
    internal static string Kind(string source)=>source.Equals("Spotify.exe",StringComparison.OrdinalIgnoreCase)||source.Equals("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",StringComparison.OrdinalIgnoreCase)?"spotify":new[]{"Chrome","chrome.exe","msedge.exe","firefox.exe","brave.exe","opera.exe","vivaldi.exe"}.Contains(source,StringComparer.OrdinalIgnoreCase)?"browser":"other";
    private Entry Track(IMediaSessionHandle handle,MediaSessionData data)
    {
        // WinRT projections use native object equality; source identity alone is never a session ID.
        if(!entries.TryGetValue(handle.Identity,out var entry)||entry.Source!=handle.Source)entries[handle.Identity]=entry=new(handle.Source);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new{data.TrackRevision,Title=Text(data.Title,512),Artist=Text(data.Artist,256),Album=Text(data.Album,256),data.StartTicks,data.EndTicks}))));
        if(entry.Fingerprint!=fingerprint){entry.Fingerprint=fingerprint;entry.Token=Guid.NewGuid().ToString("N");cached=null;}
        return entry;
    }
    private static double Duration(MediaSessionData data)=>Math.Max(0,((double)data.EndTicks-data.StartTicks)/TimeSpan.TicksPerMillisecond);
    private static MediaSessionsSnapshot Copy(MediaSessionsSnapshot snapshot)=>snapshot with{Sessions=snapshot.Sessions.ToArray()};
    public Task<MediaSessionsSnapshot> Snapshot(CancellationToken ct=default)
    {
        Task<MediaSessionsSnapshot> task;
        lock(gate){
            ObjectDisposedException.ThrowIf(disposed,this);
            if(cached is not null&&Stopwatch.GetElapsedTime(captured).TotalMilliseconds<500)return Task.FromResult(Copy(cached));
            task=inFlight??=Task.Run(ReadShared);
        }
        return AwaitCopy(task,ct);
    }
    private static async Task<MediaSessionsSnapshot> AwaitCopy(Task<MediaSessionsSnapshot> task,CancellationToken ct)=>Copy(await task.WaitAsync(ct));
    private async Task<MediaSessionsSnapshot> ReadShared()
    {
        try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var handles=(await platform.Sessions(timeout.Token)).Take(16).ToArray();
            var result=new List<MediaSessionInfo>();var live=new HashSet<object>();var failed=false;
            foreach(var handle in handles){
                try{
                    var data=await handle.Read(true,timeout.Token);live.Add(handle.Identity);
                    lock(gate){
                        var entry=Track(handle,data);var duration=Duration(data);
                        result.Add(new(entry.Id,Text(handle.Source,240),Kind(handle.Source),Text(data.Title,512),Text(data.Artist,256),Text(data.Album,256),Artwork(data.Artwork),Math.Clamp(((double)data.PositionTicks-data.StartTicks)/TimeSpan.TicksPerMillisecond,0,duration),duration,data.Playing,data.CanPlay,data.CanPause,data.CanNext,data.CanPrevious,data.CanSeek&&duration>0,entry.Token));
                    }
                }catch(Exception ex) when(ex is not OutOfMemoryException){failed=true;}
            }
            lock(gate){
                foreach(var key in entries.Keys.Where(key=>!live.Contains(key)).ToArray())entries.Remove(key);
                var message=failed?"Some Windows players did not provide media details. Refresh to retry.":null;
                cached=new(result.OrderBy(s=>s.Kind=="spotify"?0:s.Kind=="browser"?1:2).ThenBy(s=>s.Source,StringComparer.OrdinalIgnoreCase).ToArray(),failed&&result.Count==0?message:null,failed&&result.Count>0?message:null);
                captured=Stopwatch.GetTimestamp();return cached;
            }
        }catch(Exception ex) when(ex is not OutOfMemoryException){
            lock(gate){entries.Clear();cached=new([],"Windows media sessions are unavailable. Start playback in a supported app and refresh.");captured=Stopwatch.GetTimestamp();return cached;}
        }finally{lock(gate)inFlight=null;}
    }
    public async Task<object> Execute(MediaSessionActionRequest request,CancellationToken ct=default)
    {
        if(request is null||request.PresetId is not ("spotify" or "youtube")||!Guid.TryParseExact(request.SessionId,"N",out _)||!Guid.TryParseExact(request.ContextToken,"N",out _)||request.Operation is not ("toggle" or "previous" or "next" or "seek"))throw new ArgumentException("Choose a current media player and a supported media control.");
        if(request.Operation=="seek"&&(request.PositionMs is not double value||!double.IsFinite(value)||value<0)||request.Operation!="seek"&&request.PositionMs is not null)throw new ArgumentException("Choose a valid playback position for Seek only.");
        if(!await writes.WaitAsync(0,ct))throw new InvalidOperationException("A media control is still running. Try again after it finishes.");
        try{
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var handles=await platform.Sessions(timeout.Token);IMediaSessionHandle? target=null;
            lock(gate){
                ObjectDisposedException.ThrowIf(disposed,this);
                target=handles.FirstOrDefault(handle=>entries.TryGetValue(handle.Identity,out var known)&&known.Id==request.SessionId&&known.Source==handle.Source);
            }
            if(target is null)throw new InvalidOperationException("This player disconnected or was replaced. Select it again.");
            var kind=Kind(target.Source);
            if(request.PresetId=="spotify"&&kind!="spotify"||request.PresetId=="youtube"&&kind!="browser")throw new ArgumentException("Select a matching player. Browser sessions need an explicit selection and do not identify a website.");
            var data=await target.Read(false,timeout.Token);
            lock(gate){if(Track(target,data).Token!=request.ContextToken)throw new InvalidOperationException("The track changed. Refresh before using this control.");}
            var operation=request.Operation=="toggle"?(data.Playing?"pause":"play"):request.Operation;
            var allowed=operation switch{"play"=>data.CanPlay,"pause"=>data.CanPause,"previous"=>data.CanPrevious,"next"=>data.CanNext,"seek"=>data.CanSeek&&Duration(data)>0,_=>false};
            if(!allowed)throw new InvalidOperationException("This player does not support that control right now.");
            long ticks=0;
            if(operation=="seek"){
                var relative=request.PositionMs!.Value;
                if(relative>Duration(data)||relative>TimeSpan.MaxValue.TotalMilliseconds)throw new ArgumentException("Choose a position inside this track.");
                try{ticks=checked(data.StartTicks+(long)Math.Round(relative*TimeSpan.TicksPerMillisecond));}catch(OverflowException){throw new ArgumentException("Choose a position inside this track.");}
                if(ticks<Math.Max(data.StartTicks,data.MinSeekTicks)||ticks>Math.Min(data.EndTicks,data.MaxSeekTicks))throw new ArgumentException("This player cannot seek to that position.");
            }
            if(!await target.Operate(operation,ticks,timeout.Token))throw new InvalidOperationException("The selected player declined the control. Its playback was not redirected to another app.");
            lock(gate){cached=null;if(operation is "next" or "previous"&&entries.TryGetValue(target.Identity,out var entry)){entry.Fingerprint="";entry.Token=Guid.NewGuid().ToString("N");}}
            return new{ok=true};
        }finally{writes.Release();}
    }
    internal static string Artwork(byte[]? data)
    {
        if(data is null||data.Length is <24 or >MaxArtworkBytes)return "";
        if(data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&data.AsSpan(12,4).SequenceEqual("IHDR"u8)){
            var width=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16,4));var height=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20,4));
            return width is >0 and <=2048&&height is >0 and <=2048?"data:image/png;base64,"+Convert.ToBase64String(data):"";
        }
        if(data[0]!=255||data[1]!=216)return "";
        for(var i=2;i+4<data.Length;){
            if(data[i++]!=255)break;while(i<data.Length&&data[i]==255)i++;if(i>=data.Length)break;var marker=data[i++];
            if(marker is 0xD8 or 0x01)continue;if(marker is 0xD9 or 0xDA||i+2>data.Length)break;
            var length=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i,2));if(length<2||i+length>data.Length)break;
            if(marker is 0xC0 or 0xC1 or 0xC2){if(length<8)break;var height=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i+3,2));var width=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i+5,2));return width is >0 and <=2048&&height is >0 and <=2048?"data:image/jpeg;base64,"+Convert.ToBase64String(data):"";}i+=length;
        }
        return "";
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;cached=null;entries.Clear();}platform.Dispose();}
}

internal sealed class WindowsMediaSessionPlatform : IMediaSessionPlatform
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly SemaphoreSlim initialization=new(1,1);
    private readonly object gate=new();
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession,WindowsMediaSession> sessions=new();
    public async Task<IReadOnlyList<IMediaSessionHandle>> Sessions(CancellationToken ct)
    {
        if(manager is null){await initialization.WaitAsync(ct);try{manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(ct);}finally{initialization.Release();}}
        var current=manager.GetSessions();
        lock(gate){
            var live=new HashSet<GlobalSystemMediaTransportControlsSession>(current);
            foreach(var old in sessions.Keys.Where(key=>!live.Contains(key)).ToArray()){sessions[old].Dispose();sessions.Remove(old);}
            return current.Select(session=>{if(!sessions.TryGetValue(session,out var handle))sessions[session]=handle=new(session);return (IMediaSessionHandle)handle;}).ToArray();
        }
    }
    public void Dispose(){lock(gate){foreach(var session in sessions.Values)session.Dispose();sessions.Clear();manager=null;}}
}
internal sealed class WindowsMediaSession : IMediaSessionHandle,IDisposable
{
    private readonly GlobalSystemMediaTransportControlsSession session;
    private long revision;
    public WindowsMediaSession(GlobalSystemMediaTransportControlsSession session){this.session=session;session.MediaPropertiesChanged+=Changed;}
    public object Identity=>session;
    public string Source=>session.SourceAppUserModelId;
    private void Changed(GlobalSystemMediaTransportControlsSession sender,MediaPropertiesChangedEventArgs args)=>Interlocked.Increment(ref revision);
    // Windows reports Position as of LastUpdatedTime, not necessarily as of this read.
    // Estimate only a known track's playing timeline, retaining raw data when its clock/rate is unknown.
    internal static long EstimatePosition(long positionTicks,long startTicks,long endTicks,bool playing,DateTimeOffset updatedAt,double? rate,DateTimeOffset now)
    {
        if(!playing||endTicks<=startTicks||rate is not double speed||!double.IsFinite(speed)||speed==0||updatedAt<DateTimeOffset.UnixEpoch||updatedAt>now)return positionTicks;
        var advance=(now-updatedAt).Ticks*speed;
        var estimate=positionTicks+advance;
        if(!double.IsFinite(advance)||!double.IsFinite(estimate))return positionTicks;
        if(estimate<=startTicks)return startTicks;
        if(estimate>=endTicks)return endTicks;
        return Math.Clamp((long)Math.Round(estimate),startTicks,endTicks);
    }
    public async Task<MediaSessionData> Read(bool artwork,CancellationToken ct)
    {
        var observedRevision=Volatile.Read(ref revision);
        var metadata=await session.TryGetMediaPropertiesAsync().AsTask(ct);var playback=session.GetPlaybackInfo();var timeline=session.GetTimelineProperties();var controls=playback.Controls;
        byte[]? image=null;
        if(artwork&&metadata.Thumbnail is not null){
            try{using var stream=await metadata.Thumbnail.OpenReadAsync().AsTask(ct);if(stream.Size is >0 and <=MediaSessions.MaxArtworkBytes){using var reader=new DataReader(stream.GetInputStreamAt(0));var count=await reader.LoadAsync((uint)stream.Size).AsTask(ct);if(count==stream.Size){image=new byte[count];reader.ReadBytes(image);}}}catch(Exception ex) when(ex is not OutOfMemoryException&&ex is not OperationCanceledException){/* Artwork is optional; controls and metadata remain usable. */}
        }
        if(observedRevision!=Volatile.Read(ref revision))throw new InvalidOperationException("The selected player's track changed while reading its media details. Refresh before using a control.");
        var playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var position=EstimatePosition(timeline.Position.Ticks,timeline.StartTime.Ticks,timeline.EndTime.Ticks,playing,timeline.LastUpdatedTime,playback.PlaybackRate,DateTimeOffset.UtcNow);
        return new(metadata.Title,metadata.Artist,metadata.AlbumTitle,observedRevision,timeline.StartTime.Ticks,timeline.EndTime.Ticks,position,timeline.MinSeekTime.Ticks,timeline.MaxSeekTime.Ticks,playing,controls.IsPlayEnabled,controls.IsPauseEnabled,controls.IsNextEnabled,controls.IsPreviousEnabled,controls.IsPlaybackPositionEnabled,image);
    }
    public Task<bool> Operate(string operation,long absoluteTicks,CancellationToken ct)=>operation switch{
        "play"=>session.TryPlayAsync().AsTask(ct),"pause"=>session.TryPauseAsync().AsTask(ct),"next"=>session.TrySkipNextAsync().AsTask(ct),"previous"=>session.TrySkipPreviousAsync().AsTask(ct),"seek"=>session.TryChangePlaybackPositionAsync(absoluteTicks).AsTask(ct),_=>throw new ArgumentException("Unsupported media operation.")
    };
    public void Dispose()=>session.MediaPropertiesChanged-=Changed;
}
