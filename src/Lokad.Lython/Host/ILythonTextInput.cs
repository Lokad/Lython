namespace Lokad.Lython;

/// <summary>Optional host-mediated UTF-8 standard-input capability.</summary>
public interface ILythonTextInput
{
    /// <summary>Reads all remaining input bytes, returning an empty buffer at end of stream.</summary>
    /// <remarks>Implementations must honor cancellation and return well-formed UTF-8.</remarks>
    ValueTask<ReadOnlyMemory<byte>> ReadToEndUtf8Async(CancellationToken cancellationToken);

    /// <summary>Reads one line including its terminator, or <see langword="null"/> at end of stream.</summary>
    /// <remarks>Implementations must honor cancellation and return well-formed UTF-8.</remarks>
    ValueTask<ReadOnlyMemory<byte>?> ReadLineUtf8Async(CancellationToken cancellationToken);
}
