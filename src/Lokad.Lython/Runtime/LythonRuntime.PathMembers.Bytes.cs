using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private static async ValueTask<object> ReadPathBytesAsync(string path, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            context.RegisterHostCall(span);
            var stat = asynchronous ? await context.HostStatAsync(path, span).ConfigureAwait(false) : context.HostStat(path, span);
            if (!stat.Exists) throw new LythonRuntimeException("FileNotFoundError", "No such file: " + path, span);
            if (stat.IsDir) throw new LythonRuntimeException("IsADirectoryError", "Is a directory: " + path, span);
            if (context.Limits.MaxHostReadBytes is { } maxRead && stat.Size > maxRead)
                throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maxRead})", span);
            // The acquired host buffer and the independently owned guest bytes
            // coexist during copying. Fund both before requesting a known file.
            if (stat.Size > 0)
            {
                var estimate = stat.Size * 2 + 64;
                context.MemoryGovernor.EnsureCanReserve(estimate >= long.MaxValue ? long.MaxValue - 1 : (long)estimate, span);
            }
            using var acquired = asynchronous
                ? await ReadGovernedHostBytesAfterStatAsync(path, stat, context, span).ConfigureAwait(false)
                : ReadGovernedHostBytesAfterStat(path, stat, context, span);
            return CopyPathBytes(acquired.Memory, span, context);
        }

        private static PyBytes CopyPathBytes(ReadOnlyMemory<byte> source, LythonSourceSpan span, ExecutionContext context)
        {
            context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(source.Length), span);
            var copy = new byte[source.Length];
            for (var offset = 0; offset < source.Length; offset += Math.Min(4096, source.Length - offset))
            {
                context.CheckExecution(span);
                var length = Math.Min(4096, source.Length - offset);
                source.Span.Slice(offset, length).CopyTo(copy.AsSpan(offset, length));
            }
            var result = new PyBytes(copy, context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            return result;
        }

        private static async ValueTask<object> WritePathBytesAsync(string path, object value,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (value is not PyBytes bytes)
                throw RuntimeErrors.Type("a bytes-like object is required", span);
            // The host receives a fresh staging array, never guest-owned storage.
            // Hold its charge until publication (or failure/cancellation) ends.
            using var reservation = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(bytes.Length), span);
            var payload = new byte[bytes.Length];
            for (var offset = 0; offset < bytes.Length; offset += Math.Min(4096, bytes.Length - offset))
            {
                context.CheckExecution(span);
                var length = Math.Min(4096, bytes.Length - offset);
                bytes.Bytes.Slice(offset, length).CopyTo(payload.AsSpan(offset, length));
            }
            context.RegisterHostCall(span);
            if (asynchronous) await context.WriteHostBytesAsync(path, payload, span).ConfigureAwait(false);
            else context.WriteHostBytes(path, payload, span);
            GC.KeepAlive(bytes);
            return new BigInteger(bytes.Length);
        }
    }
}
