namespace Lokad.Lython;

public interface ILythonTextOutput
{
    ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    ValueTask FlushAsync(CancellationToken cancellationToken);
}
