namespace Lokad.Lython;

/// <summary>
/// Reports that a host produced more captured subprocess output than a request permits.
/// </summary>
public sealed class LythonSubprocessOutputLimitException : Exception
{
    /// <summary>Creates an output-limit failure for one captured subprocess stream.</summary>
    public LythonSubprocessOutputLimitException(string streamName, long actualBytes, long maximumBytes)
        : base($"subprocess {streamName} exceeded maximum captured output bytes ({maximumBytes}); received {actualBytes} bytes.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentOutOfRangeException.ThrowIfNegative(actualBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        StreamName = streamName;
        ActualBytes = actualBytes;
        MaximumBytes = maximumBytes;
    }

    /// <summary>Gets the captured stream whose output exceeded the limit.</summary>
    public string StreamName { get; }

    /// <summary>Gets the number of bytes supplied by the host.</summary>
    public long ActualBytes { get; }

    /// <summary>Gets the maximum permitted number of captured bytes.</summary>
    public long MaximumBytes { get; }
}
