using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace TabletDeck;

public sealed class ObsClient : IAsyncDisposable
{
    private ClientWebSocket? socket;
    private readonly SemaphoreSlim connectGate=new(1,1), sendGate=new(1,1);
    private sealed record Pending(ClientWebSocket Socket,TaskCompletionSource<JsonObject> Completion);
    private readonly ConcurrentDictionary<string,Pending> pending=new();
    public event Action? Changed;
    private readonly Func<DeckConfig> config;
    private CancellationTokenSource lifetime=new();
    private int port;
    private string password="";
    private DateTime nextConnectAt;
    public ObsClient(Func<DeckConfig> config) { this.config=config; }
    public static string Auth(string password,string salt,string challenge)
    {
        var secret=Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password+salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret+challenge)));
    }
    private async Task<JsonObject> Read(ClientWebSocket ws,CancellationToken ct)
    {
        using var data=new MemoryStream();
        var buffer=new byte[8192];
        WebSocketReceiveResult result;
        do {
            result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),ct);
            if(result.MessageType==WebSocketMessageType.Close) throw new IOException("OBS closed the connection. Check the WebSocket password.");
            data.Write(buffer,0,result.Count);
            if(data.Length>4*1024*1024) throw new IOException("OBS response exceeds limit.");
        } while(!result.EndOfMessage);
        return JsonNode.Parse(data.ToArray())?.AsObject() ?? throw new IOException("Invalid OBS response.");
    }
    private async Task Send(ClientWebSocket ws,JsonObject value,CancellationToken ct)
    {
        await sendGate.WaitAsync(ct);
        try { await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(value.ToJsonString())),WebSocketMessageType.Text,true,ct); }
        finally { sendGate.Release(); }
    }
    private async Task Ensure()
    {
        var active=config();if(socket?.State==WebSocketState.Open&&port==active.ObsPort&&password==active.ObsPassword)return;
        await connectGate.WaitAsync();
        try {
            var c=config();
            if(socket?.State==WebSocketState.Open&&port==c.ObsPort&&password==c.ObsPassword) return;
            if(port==c.ObsPort&&password==c.ObsPassword&&DateTime.UtcNow<nextConnectAt)throw new IOException("OBS is reconnecting. Check that its WebSocket server is running.");
            lifetime.Cancel();socket?.Abort();socket?.Dispose();lifetime.Dispose();lifetime=new();
            foreach(var task in pending.Values) task.Completion.TrySetException(new IOException("OBS connection changed."));pending.Clear();
            port=c.ObsPort;password=c.ObsPassword;
            var ws=new ClientWebSocket();socket=ws;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
            try {
                await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"),timeout.Token);
                var hello=await Read(ws,timeout.Token);
                if(hello["op"]?.GetValue<int>()!=0) throw new IOException("OBS WebSocket v5 is required.");
                // Scenes, input changes, and output state; no high-volume meter events.
                var identify=new JsonObject { ["rpcVersion"]=1,["eventSubscriptions"]=4|8|64 };
                if(hello["d"]?["authentication"] is JsonObject auth)
                    identify["authentication"]=Auth(password,auth["salt"]!.GetValue<string>(),auth["challenge"]!.GetValue<string>());
                await Send(ws,new JsonObject{["op"]=1,["d"]=identify},timeout.Token);
                var identified=await Read(ws,timeout.Token);
                if(identified["op"]?.GetValue<int>()!=2) throw new IOException("OBS authentication failed.");
                _=ReceiveLoop(ws,lifetime.Token);
                nextConnectAt=DateTime.MinValue;
            } catch {nextConnectAt=DateTime.UtcNow.AddMilliseconds(500);ws.Abort();throw;}
        } finally { connectGate.Release(); }
    }
    private async Task ReceiveLoop(ClientWebSocket ws,CancellationToken ct)
    {
        try {
            while(!ct.IsCancellationRequested) {
                var msg=await Read(ws,ct);
                if(msg["op"]?.GetValue<int>()==7 && msg["d"] is JsonObject d && pending.TryRemove(d["requestId"]!.GetValue<string>(),out var task)) task.Completion.TrySetResult(d);
                else if(msg["op"]?.GetValue<int>()==5)Changed?.Invoke();
            }
        } catch(Exception ex) {
            ws.Abort();
            foreach(var pair in pending.ToArray()) if(ReferenceEquals(pair.Value.Socket,ws)&&pending.TryRemove(pair.Key,out var task)) task.Completion.TrySetException(new IOException("OBS disconnected: "+ex.Message));
            if(ReferenceEquals(socket,ws))Changed?.Invoke();
        }
    }
    public async Task<JsonObject> Call(string type,object? data=null)
    {
        await Ensure();
        var ws=socket ?? throw new IOException("OBS is not connected.");
        var id=Guid.NewGuid().ToString("N");
        var tcs=new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id]=new(ws,tcs);
        try {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Send(ws,new JsonObject{["op"]=6,["d"]=new JsonObject{["requestType"]=type,["requestId"]=id,["requestData"]=data is null ? new JsonObject():JsonSerializer.SerializeToNode(data,DataStore.Json)}},timeout.Token);
            var response=await tcs.Task.WaitAsync(timeout.Token);
            if(response["requestStatus"]?["result"]?.GetValue<bool>()!=true)
                throw new InvalidOperationException(response["requestStatus"]?["comment"]?.GetValue<string>()??$"OBS rejected {type}.");
            return response["responseData"]?.DeepClone().AsObject()??new JsonObject();
        } finally { pending.TryRemove(id,out _); }
    }
    public ValueTask DisposeAsync() { lifetime.Cancel();socket?.Abort();socket?.Dispose();return ValueTask.CompletedTask; }
}
