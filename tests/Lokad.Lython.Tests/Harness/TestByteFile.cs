namespace Lokad.Lython.Tests.Harness;

// Decorates an actual owned file at the operation being exercised. Test hooks
// cannot accidentally keep observing retired path-based transports instead.
internal sealed class TestByteFile(ILythonByteFile file) : ILythonByteFile, ILythonSynchronousHostCapability
{
    public bool CompletesSynchronously { get; init; }
        = file is ILythonSynchronousHostCapability { CompletesSynchronously: true };
    public Func<Memory<byte>, CancellationToken, ValueTask<int>>? Read { get; init; }
    public Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<LythonFileWriteResult>>? Write { get; init; }
    public Func<CancellationToken, ValueTask>? Flush { get; init; }
    public Func<CancellationToken, ValueTask>? Rewrite { get; init; }
    public Func<ValueTask>? Close { get; init; }
    public Func<long?>? SizeHint { get; init; }
    public Func<long>? Cursor { get; init; }
    public long Position => Cursor is { } cursor ? cursor() : file.Position;
    public long? LengthHint => SizeHint is { } hint ? hint() : file.LengthHint;
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => Read?.Invoke(buffer, token) ?? file.ReadAsync(buffer, token);
    public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token) => Write?.Invoke(buffer, token) ?? file.WriteAsync(buffer, token);
    public ValueTask RewindAndTruncateAsync(CancellationToken token) => Rewrite?.Invoke(token) ?? file.RewindAndTruncateAsync(token);
    public ValueTask FlushAsync(CancellationToken token) => Flush?.Invoke(token) ?? file.FlushAsync(token);
    public ValueTask DisposeAsync() => Close?.Invoke() ?? file.DisposeAsync();
}
