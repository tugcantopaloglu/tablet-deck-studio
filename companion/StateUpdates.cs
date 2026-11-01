using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace TabletDeck;

public sealed class StateUpdates : IAsyncDisposable
{
    private readonly DeckEngine engine;
    private readonly ConcurrentDictionary<Guid,Channel<byte[]>> subscribers=new();
    private readonly Channel<byte> changes=Channel.CreateBounded<byte>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropOldest});
    private readonly CancellationTokenSource stop=new();
    private readonly Task producer,heartbeat;
    public StateUpdates(DeckEngine engine)
    {
        this.engine=engine;engine.Invalidated+=Signal;
        producer=Task.Run(async()=>{
            await foreach(var _ in changes.Reader.ReadAllAsync(stop.Token)) {
                if(subscribers.IsEmpty)continue;
                var state=await engine.State();state.Remove("scenes");
                var bytes=JsonSerializer.SerializeToUtf8Bytes(new{type="state",state});
                foreach(var subscriber in subscribers.Values)subscriber.Writer.TryWrite(bytes);
            }
        });
        heartbeat=Task.Run(async()=>{using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));while(await timer.WaitForNextTickAsync(stop.Token))Signal();});
    }
    public void Signal(){if(!subscribers.IsEmpty)changes.Writer.TryWrite(1);}
    public (Guid Id,ChannelReader<byte[]> Reader) Subscribe()
    {
        if(subscribers.Count>=32)throw new InvalidOperationException("Too many live state connections. Close another control panel.");
        var channel=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});
        var id=Guid.NewGuid();subscribers[id]=channel;Signal();return(id,channel.Reader);
    }
    public void Unsubscribe(Guid id){if(subscribers.TryRemove(id,out var channel))channel.Writer.TryComplete();}
    public async ValueTask DisposeAsync()
    {
        engine.Invalidated-=Signal;stop.Cancel();
        foreach(var id in subscribers.Keys)Unsubscribe(id);
        try{await Task.WhenAll(producer,heartbeat);}catch(OperationCanceledException){}stop.Dispose();
    }
}
