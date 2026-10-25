namespace TabletDeck;

public sealed record RunningAction(string Id,string Label,string Kind,int Step,int Total,string StepLabel,bool Cancelling);
public sealed record ExecutionStatus(string Session,long Revision,RunningAction? Active);

// Cancellation targets a particular run, so a delayed tap cannot cancel the next one.
public sealed class ActionExecution
{
    private readonly object gate=new();
    private readonly string session=Guid.NewGuid().ToString("N");
    private long revision;
    private RunningAction? active;
    private CancellationTokenSource? cancellation;
    public event Action? Changed;
    public ExecutionStatus Status {get{lock(gate)return new(session,revision,active);}}
    private static int Count(DeckAction action)=>action.Kind=="macro"?action.Steps.Sum(Count):1;
    public async Task Run(DeckAction action,Func<CancellationToken,Task> run)
    {
        CancellationTokenSource source;
        lock(gate) {
            if(active is not null)throw new InvalidOperationException("An action is still running. Wait for it to finish or cancel it.");
            source=new(TimeSpan.FromSeconds(45));cancellation=source;
            active=new(Guid.NewGuid().ToString("N"),action.Label,action.Kind,0,Count(action),"Starting",false);revision++;
        }
        Changed?.Invoke();
        try {await run(source.Token);source.Token.ThrowIfCancellationRequested();}
        catch(OperationCanceledException) when(source.IsCancellationRequested) {
            bool cancelled;lock(gate)cancelled=active?.Cancelling==true;
            throw new InvalidOperationException(cancelled?"Action cancelled. Completed steps were kept.":"The action timed out. Completed steps were kept.");
        }
        finally {
            lock(gate){active=null;cancellation=null;revision++;source.Dispose();}
            Changed?.Invoke();
        }
    }
    public void Step(DeckAction action)
    {
        lock(gate) {
            cancellation?.Token.ThrowIfCancellationRequested();
            if(active is null)return;
            active=active with{Step=active.Step+1,StepLabel=action.Label};revision++;
        }
        Changed?.Invoke();
    }
    public bool Cancel(string id)
    {
        if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Choose the current running action to cancel.");
        lock(gate) {
            if(active?.Id!=id||active.Cancelling)return false;
            active=active with{Cancelling=true};revision++;
            cancellation!.Cancel();
        }
        Changed?.Invoke();return true;
    }
}
