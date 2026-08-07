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
    /// <summary>Gets the current non-negative monotonic-clock reading in nanoseconds.</summary>
    long MonotonicNanoseconds { get; }

    /// <summary>Gets the positive monotonic-clock resolution in nanoseconds.</summary>
    long MonotonicResolutionNanoseconds => 1;

    /// <summary>Delays for at least the requested non-negative duration while honoring cancellation.</summary>
    ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}
