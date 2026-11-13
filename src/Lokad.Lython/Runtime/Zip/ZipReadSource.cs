using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Zip;

// One opened identity or already-owned archive buffer. Regions borrow buffered
// input, or own only the positional bytes actually requested. The ZIP handle
// closes the file; a region keeps its admitted destination through real I/O.
internal sealed class ZipReadSource
{
    private readonly ReadOnlyMemory<byte> _bytes;
    private readonly OpenedByteFile? _file;

    internal ZipReadSource(ReadOnlyMemory<byte> bytes) { _bytes = bytes; Length = (ulong)bytes.Length; }
    internal ZipReadSource(OpenedByteFile file, long length) { _file = file; Length = checked((ulong)length); }
    internal ulong Length { get; }

    internal async ValueTask<Region> ReadAsync(ulong offset, int count,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span, bool asynchronous)
    {
        context.CheckExecution(span);
        if (count < 0 || offset > Length || (ulong)count > Length - offset)
            throw new InvalidDataException("Invalid ZIP archive: requested record extends beyond the end of data.");
        if (_file is null) return new(_bytes.Slice(checked((int)offset), count), null);

        var builder = new GovernedByteBuilder(context.MemoryGovernor, span, count, context.State.CancellationToken);
        try
        {
            while (builder.Length < count)
            {
                var length = Math.Min(16384, count - builder.Length);
                var read = await _file.ReadAtAsync(checked((long)offset + builder.Length),
                    builder.GetWriteWindow(length), span, asynchronous).ConfigureAwait(false);
                if (read == 0) throw new InvalidDataException("Invalid ZIP archive: unexpected end of opened data.");
                builder.Advance(read);
            }
            var owner = GovernedHostBytes.FromBuilder(builder);
            return new(owner.Memory, owner);
        }
        finally { builder.Release(); }
    }

    internal readonly struct Region(ReadOnlyMemory<byte> memory, GovernedHostBytes? owner) : IDisposable
    {
        internal ReadOnlyMemory<byte> Memory => memory;
        public void Dispose() => owner?.Dispose();
    }
}
