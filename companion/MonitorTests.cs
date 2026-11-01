using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabletDeck;
public static class MonitorTests
{
    public static async Task Run(){
        var passed=0;void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);passed++;Console.WriteLine("PASS "+label);}
        void Reject(Action action,string label){try{action();throw new Exception("Accepted "+label);}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){Check(true,label);}}
        using var bitmap=new Bitmap(16,16);using var encoded=new MemoryStream();bitmap.Save(encoded,ImageFormat.Jpeg);var image="data:image/jpeg;base64,"+Convert.ToBase64String(encoded.ToArray());
        Check(LivePreview.ValidateJpeg(image)==(16,16),"preview accepts bounded generated JPEG");
        Reject(()=>LivePreview.ValidateJpeg("data:image/png;base64,AAAA"),"preview rejects non-JPEG data URLs");Reject(()=>LivePreview.ValidateJpeg("data:image/jpeg;base64,???"),"preview rejects invalid base64");Reject(()=>LivePreview.ValidateJpeg("data:image/jpeg;base64,"+Convert.ToBase64String(new byte[400*1024])),"preview rejects oversized image payloads");Reject(()=>LivePreview.ValidateJpeg("data:image/jpeg;base64,/9j/2Q=="),"preview rejects missing JPEG dimensions");
        Reject(()=>LivePreview.Validate(new("desktop",640)),"preview rejects arbitrary source mode");Reject(()=>LivePreview.Validate(new("program",4096)),"preview rejects oversized requested width");
        var calls=new List<(string Type,object? Data)>();var unavailable=false;TaskCompletionSource? wait=null;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<JsonObject> Call(string type,object? data,CancellationToken ct){calls.Add((type,data));if(type=="GetSceneList")return new JsonObject{["currentProgramSceneName"]="Program fixture",["currentPreviewSceneName"]=unavailable?null:"Preview fixture"};if(type=="GetVideoSettings")return new JsonObject{["baseWidth"]=1920,["baseHeight"]=1080};if(type=="GetSourceScreenshot"){entered.TrySetResult();if(wait is not null)await wait.Task.WaitAsync(ct);return new JsonObject{["imageData"]=image};}throw new Exception("Unexpected request "+type);}
        var preview=new LivePreview(Call);var first=await preview.Frame(new());Check(first.Source=="Program fixture"&&first.Image==image&&first.Mode=="program","preview reads OBS program without changing scenes");Check(calls.All(c=>c.Type.StartsWith("Get",StringComparison.Ordinal)),"preview never starts output or sends an OBS mutation");
        var args=JsonSerializer.SerializeToElement(calls.Single(c=>c.Type=="GetSourceScreenshot").Data);Check(args.GetProperty("imageWidth").GetInt32()==640&&args.GetProperty("imageHeight").GetInt32()==360&&args.GetProperty("imageCompressionQuality").GetInt32()==55,"preview requests bounded 16:9 JPEG quality");
        var cached=await preview.Frame(new());Check(cached.Sequence==first.Sequence&&calls.Count==3&&cached.AgeMs>=0,"matching requests share a recent captured frame");
        try{await preview.Frame(new("preview",640));throw new Exception("Rate limit absent");}catch(InvalidOperationException){Check(calls.Count==3,"different modes cannot bypass global capture ceiling");}
        await Task.Delay(270);var studio=await preview.Frame(new("preview",320));Check(studio.Source=="Preview fixture"&&studio.RequestedWidth==320&&studio.Sequence>first.Sequence,"Studio preview uses the actual preview scene and separate size key");
        await Task.Delay(270);unavailable=true;try{await preview.Frame(new("preview",320));throw new Exception("Missing scene accepted");}catch(InvalidOperationException ex){Check(ex.Message.Contains("Studio Mode"),"missing Studio preview reports how to enable it");}
        unavailable=false;await Task.Delay(270);wait=new(TaskCreationOptions.RunContinuationsAsynchronously);entered=new(TaskCreationOptions.RunContinuationsAsynchronously);var pending=preview.Frame(new());await entered.Task;
        var count=calls.Count;try{await preview.Frame(new());throw new Exception("Queued duplicate frame");}catch(InvalidOperationException){Check(calls.Count==count,"busy capture rejects another request without queuing work");}wait.SetResult();await pending;
        await Task.Delay(270);wait=new(TaskCreationOptions.RunContinuationsAsynchronously);using var cancel=new CancellationTokenSource(30);try{await preview.Frame(new(),cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"preview capture respects cancellation");}
        wait=null;await Task.Delay(270);Check((await preview.Frame(new())).Sequence>first.Sequence,"cancelled capture releases the gate for future frames");
        Console.WriteLine(passed+" monitor storage/protocol checks passed; generated image only, no OBS or app window.");
    }
}
