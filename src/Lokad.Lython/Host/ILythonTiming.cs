namespace Lokad.Lython;

/// <summary>
/// Optional host capability for monotonic-clock reads and cancellable delays.
/// </summary>
/// <remarks>
/// Values use nanoseconds so the Python <c>*_ns</c> APIs remain integral. The
/// monotonic epoch is host-defined; only differences between readings are
/// meaningful. Implementations must not return a negative reading or a
/// non-positive resolution.
/// </remarks>
public interface ILythonTiming
{
    long MonotonicNanoseconds { get; }

    long MonotonicResolutionNanoseconds => 1;

    ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}
