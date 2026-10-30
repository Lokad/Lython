using System.Threading.Tasks.Sources;

namespace Lokad.Lython.Tests.Harness;

// A controlled producer must remain pending until the consumer actually observes
// that state. Signalling callback entry alone races with the consumer's status
// check, especially on a fast CI worker. This wraps real joined work, not a clock.
internal sealed class ObservedValueTask<T>(Task<T> completion, TaskCompletionSource observedPending, Action? beforeReturningPending = null)
    : IValueTaskSource<T>, IValueTaskSource
{
    public ValueTask<T> Value => new(this, 0);
    public ValueTask Untyped => new(this, 0);

    public ValueTaskSourceStatus GetStatus(short token)
    {
        ValidateToken(token);
        var status = completion.Status switch
        {
            TaskStatus.RanToCompletion => ValueTaskSourceStatus.Succeeded,
            TaskStatus.Faulted => ValueTaskSourceStatus.Faulted,
            TaskStatus.Canceled => ValueTaskSourceStatus.Canceled,
            _ => ValueTaskSourceStatus.Pending,
        };
        if (status == ValueTaskSourceStatus.Pending)
        {
            observedPending.TrySetResult();
            beforeReturningPending?.Invoke();
        }
        return status;
    }

    public T GetResult(short token)
    {
        ValidateToken(token);
        return completion.GetAwaiter().GetResult();
    }

    void IValueTaskSource.GetResult(short token) => _ = GetResult(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        ValidateToken(token);
        var awaiter = completion.ConfigureAwait((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0).GetAwaiter();
        awaiter.OnCompleted(() => continuation(state));
    }

    private static void ValidateToken(short token)
    {
        if (token != 0) throw new InvalidOperationException("Unexpected fixture operation token.");
    }
}
