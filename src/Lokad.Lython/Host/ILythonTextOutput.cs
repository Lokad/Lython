namespace Lokad.Lython;

/// <summary>Optional host-mediated UTF-8 standard-output or standard-error capability.</summary>
public interface ILythonTextOutput
{
    /// <summary>Writes the complete UTF-8 payload exactly once.</summary>
    /// <remarks>Implementations must honor cancellation and preserve write ordering.</remarks>
    ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    /// <summary>Flushes all preceding writes to the host-defined destination.</summary>
    /// <remarks>Implementations must honor cancellation and may complete synchronously.</remarks>
    ValueTask FlushAsync(CancellationToken cancellationToken);
}
