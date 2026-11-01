using System.Buffers.Binary;
using System.Text.Json.Nodes;

namespace TabletDeck;
public sealed record PreviewRequest(string Mode="program",int Width=640);
public sealed record PreviewFrame(long Sequence,string Source,string Mode,int RequestedWidth,int Width,int Height,string Image,DateTimeOffset CapturedAt,double AgeMs);

// A separate OBS client and a global capture ceiling keep preview traffic off the control connection.
public sealed class LivePreview(Func<string,object?,CancellationToken,Task<JsonObject>> call)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private PreviewFrame? latest;
    private long capturedTicks,attemptTicks,sequence;
    private const int MaxImageBytes=384*1024;
    public static void Validate(PreviewRequest req){if(req.Mode is not ("program" or "preview")||req.Width is not (320 or 640 or 960))throw new ArgumentException("Choose program or Studio preview, and a supported preview size.");}
    private static double Elapsed(long then)=>then==0?double.MaxValue:System.Diagnostics.Stopwatch.GetElapsedTime(then).TotalMilliseconds;
    private PreviewFrame? Cached(PreviewRequest request){var frame=Volatile.Read(ref latest);return frame is not null&&frame.Mode==request.Mode&&frame.RequestedWidth==request.Width&&Elapsed(Volatile.Read(ref capturedTicks))<250?frame with{AgeMs=Elapsed(Volatile.Read(ref capturedTicks))}:null;}
    public async Task<PreviewFrame> Frame(PreviewRequest request,CancellationToken ct=default){
        Validate(request);var cached=Cached(request);if(cached is not null)return cached;
        if(!await gate.WaitAsync(0,ct))throw new InvalidOperationException("Preview capture is busy. It will retry without queuing frames.");
        try{
            cached=Cached(request);if(cached is not null)return cached;
            if(Elapsed(attemptTicks)<250)throw new InvalidOperationException("Preview capture is rate limited. It will retry shortly.");
            attemptTicks=System.Diagnostics.Stopwatch.GetTimestamp();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(4));
            var scenes=await call("GetSceneList",null,timeout.Token);
            var name=scenes[request.Mode=="program"?"currentProgramSceneName":"currentPreviewSceneName"]?.GetValue<string>();
            if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException(request.Mode=="preview"?"Enable Studio Mode in OBS and choose a preview scene.":"Choose a program scene in OBS.");
            var video=await call("GetVideoSettings",null,timeout.Token);var baseWidth=video["baseWidth"]?.GetValue<int>()??1920;var baseHeight=video["baseHeight"]?.GetValue<int>()??1080;
            if(baseWidth is <8 or >16384||baseHeight is <8 or >16384)throw new InvalidOperationException("OBS returned unsupported canvas dimensions.");
            var width=request.Width;var height=Math.Max(8,(int)Math.Round((double)width*baseHeight/baseWidth));if(height>1280){height=1280;width=Math.Max(8,(int)Math.Round((double)height*baseWidth/baseHeight));}
            var shot=await call("GetSourceScreenshot",new{sourceName=name,imageFormat="jpg",imageWidth=width,imageHeight=height,imageCompressionQuality=55},timeout.Token);
            var image=shot["imageData"]?.GetValue<string>()??"";var dimensions=ValidateJpeg(image);
            var frame=new PreviewFrame(++sequence,name,request.Mode,request.Width,dimensions.Width,dimensions.Height,image,DateTimeOffset.UtcNow,0);
            Volatile.Write(ref latest,frame);Volatile.Write(ref capturedTicks,System.Diagnostics.Stopwatch.GetTimestamp());return frame;
        }finally{gate.Release();}
    }
    internal static (int Width,int Height) ValidateJpeg(string image){
        const string prefix="data:image/jpeg;base64,";
        if(!image.StartsWith(prefix,StringComparison.Ordinal)||image.Length>MaxImageBytes*4/3+64)throw new InvalidOperationException("OBS preview image has an unsupported format or exceeds its size limit.");
        byte[] data;try{data=Convert.FromBase64String(image[prefix.Length..]);}catch(FormatException){throw new InvalidOperationException("OBS returned an invalid preview image.");}
        if(data.Length is <4 or >MaxImageBytes||data[0]!=255||data[1]!=216)throw new InvalidOperationException("OBS returned an invalid JPEG preview.");
        for(var i=2;i+4<data.Length;){
            if(data[i++]!=255)break;while(i<data.Length&&data[i]==255)i++;if(i>=data.Length)break;var marker=data[i++];
            if(marker is 0xD8 or 0x01)continue;if(marker is 0xD9 or 0xDA||i+2>data.Length)break;
            var length=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i,2));if(length<2||i+length>data.Length)break;
            if(marker is 0xC0 or 0xC1 or 0xC2){if(length<8)break;var height=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i+3,2));var width=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i+5,2));if(width is <8 or >1280||height is <8 or >1280)break;return(width,height);}i+=length;
        }
        throw new InvalidOperationException("Preview JPEG dimensions are invalid or exceed 1280 pixels.");
    }
}
