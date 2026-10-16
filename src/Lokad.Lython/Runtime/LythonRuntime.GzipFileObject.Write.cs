using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class GzipFileObject
    {
        internal async ValueTask WriteHeaderAsync(object originalLevel, object mtime, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            BeginOperation(context, span);
            try { await WriteHeaderCoreAsync(originalLevel, mtime, span, asynchronous).ConfigureAwait(false); }
            finally { _operationContext = null; }
        }

        private async ValueTask WriteHeaderCoreAsync(object originalLevel, object mtime, LythonSourceSpan span, bool asynchronous)
        {
            await WriteBytesAsync([31,139], span, asynchronous).ConfigureAwait(false);
            await WriteBytesAsync([8], span, asynchronous).ConfigureAwait(false);
            // Header names follow Lython's POSIX path convention, independently
            // of the normalized path used for contained host acquisition.
            using var nameStaging = _services.MemoryGovernor.ReserveTemporary(256L + 8L * _name.Utf8Bytes.Length, span);
            var name = _name.AsString();
            name = name[(name.LastIndexOf('/') + 1)..];
            byte[] filename;
            if (name.Any(character => character > 255)) filename = [];
            else
            {
                if (name.EndsWith(".gz", StringComparison.Ordinal)) name = name[..^3];
                filename = Encoding.Latin1.GetBytes(name);
            }
            await WriteBytesAsync([(byte)(filename.Length == 0 ? 0 : 8)], span, asynchronous).ConfigureAwait(false);
            var seconds = mtime is PyNone ? new BigInteger(_services.Host.UtcNow.ToUnixTimeSeconds())
                : await GzipFileTimeAsync(mtime, span, asynchronous).ConfigureAwait(false);
            if (seconds < 0 || seconds > uint.MaxValue)
                throw new LythonRuntimeException(ModuleException("struct", "error"), "'I' format requires 0 <= number <= 4294967295", span);
            var time = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(time, (uint)seconds);
            await WriteBytesAsync(time, span, asynchronous).ConfigureAwait(false);
            var best = asynchronous ? await ElementEqualsAsync(originalLevel, new BigInteger(9), CurrentContext, span).ConfigureAwait(false)
                : ElementEquals(originalLevel, new BigInteger(9), CurrentContext, span);
            var fast = !best && (asynchronous ? await ElementEqualsAsync(originalLevel, BigInteger.One, CurrentContext, span).ConfigureAwait(false)
                : ElementEquals(originalLevel, BigInteger.One, CurrentContext, span));
            await WriteBytesAsync([(byte)(best ? 2 : fast ? 4 : 0)], span, asynchronous).ConfigureAwait(false);
            await WriteBytesAsync([255], span, asynchronous).ConfigureAwait(false);
            if (filename.Length > 0)
            {
                using var staging = _services.MemoryGovernor.ReserveTemporary(33L + filename.Length, span);
                var terminated = new byte[filename.Length + 1];
                filename.CopyTo(terminated, 0);
                await WriteBytesAsync(terminated, span, asynchronous).ConfigureAwait(false);
            }
        }

        private async ValueTask<BigInteger> GzipFileTimeAsync(object value, LythonSourceSpan span, bool asynchronous)
        {
            if (value is PyInstance instance)
            {
                foreach (var slot in new[] { "__int__", "__index__" })
                {
                    if (!instance.Type.TryLookupInMro(slot, 0, out var raw, out _)) continue;
                    var method = asynchronous ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, CurrentContext, span).ConfigureAwait(false)
                        : PyAttributeLookup.BindForInstance(instance, raw, CurrentContext, span);
                    var result = asynchronous
                        ? await InvokeCallableTargetAsync(method, span, span, CurrentContext, () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false)
                        : InvokeCallableTarget(method, span, span, CurrentContext, static () => []);
                    if (PyNumberOps.TryAsInteger(result, out var integer)) return integer;
                    throw RuntimeErrors.Type(slot + " returned non-int", span);
                }
            }
            return (BigInteger)Int([value], span, CurrentContext);
        }

        private async ValueTask WriteBytesAsync(byte[] bytes, LythonSourceSpan span, bool asynchronous)
        {
            var value = CreateBytes(bytes, CurrentContext, span);
            await PyBinaryStream.CallAsync(_stream, "write", [value], span, CurrentContext, asynchronous).ConfigureAwait(false);
        }

        private async ValueTask PublishNativeOutputAsync(LythonSourceSpan span, bool asynchronous)
        {
            using var copy = _services.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(_output.Length), span);
            var bytes = _output.WrittenSpan.ToArray();
            _output.Release();
            copy.Dispose();
            await WriteBytesAsync(bytes, span, asynchronous).ConfigureAwait(false);
        }

        private async ValueTask CompressAsync(ReadOnlyMemory<byte> data, LythonSourceSpan span, bool asynchronous)
        {
            var crc = _crc;
            for (var offset = 0; offset < data.Length;)
            {
                _services.CheckExecution(span);
                var count = Math.Min(65536, data.Length - offset);
                _native!.Write(data.Span.Slice(offset, count));
                crc = Crc32.Update(crc, data.Span.Slice(offset, count));
                offset += count;
            }
            _nativeHasInput |= data.Length > 0;
            await PublishNativeOutputAsync(span, asynchronous).ConfigureAwait(false);
            _crc = crc;
            _memberSize = unchecked(_memberSize + (uint)data.Length);
        }

        private async ValueTask DrainPendingAsync(LythonSourceSpan span, bool asynchronous)
        {
            if (_pending.Length == 0) return;
            await CompressAsync(_pending.WrittenMemory, span, asynchronous).ConfigureAwait(false);
            _pending.Release();
        }

        private async ValueTask<BigInteger> WriteAsync(object value, LythonSourceSpan span, bool asynchronous)
        {
            EnsureWritable(span);
            if (value is not PyBytes bytes) throw RuntimeErrors.Type("a bytes-like object is required", span);
            if (bytes.Length == 0) return BigInteger.Zero;
            if (_pending.Length == 0 && bytes.Length >= WriteBufferBytes)
            {
                await CompressAsync(bytes.Memory, span, asynchronous).ConfigureAwait(false);
                _position += bytes.Length;
                return new BigInteger(bytes.Length);
            }
            for (var offset = 0; offset < bytes.Length;)
            {
                _services.CheckExecution(span);
                var count = Math.Min(WriteBufferBytes - _pending.Length, bytes.Length - offset);
                _pending.Append(bytes.Bytes.Slice(offset, count));
                _position += count;
                offset += count;
                if (_pending.Length == WriteBufferBytes)
                    await DrainPendingAsync(span, asynchronous).ConfigureAwait(false);
            }
            return new BigInteger(bytes.Length);
        }

        private async ValueTask FlushAsync(LythonSourceSpan span, bool asynchronous)
        {
            EnsureOpen(span);
            if (_reading) return;
            await DrainPendingAsync(span, asynchronous).ConfigureAwait(false);
            _services.CheckExecution(span);
            if (!_nativeHasInput && !_emptySyncWritten)
            {
                _output.Append([0,0,0,255,255]);
                _emptySyncWritten = true;
            }
            else _native!.Flush();
            await PublishNativeOutputAsync(span, asynchronous).ConfigureAwait(false);
            await PyBinaryStream.CallAsync(_stream, "flush", [], span, CurrentContext, asynchronous).ConfigureAwait(false);
        }

        private async ValueTask CloseAsync(LythonSourceSpan span, bool asynchronous)
        {
            if (_closed) return;
            var stream = _stream;
            try
            {
                if (!_reading && _services.MemoryGovernor.AdmissionFailure is null)
                {
                    await DrainPendingAsync(span, asynchronous).ConfigureAwait(false);
                    _services.CheckExecution(span);
                    if (!_nativeHasInput) WriteEmptyDeflate(_sink!, _level);
                    _native!.Dispose();
                    _native = null;
                    _services.MemoryGovernor.Release(_nativeCharge); _nativeCharge = 0;
                    await PublishNativeOutputAsync(span, asynchronous).ConfigureAwait(false);
                    var trailer = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(trailer, ~_crc);
                    await WriteBytesAsync(trailer, span, asynchronous).ConfigureAwait(false);
                    trailer = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(trailer, _memberSize);
                    await WriteBytesAsync(trailer, span, asynchronous).ConfigureAwait(false);
                }
            }
            finally
            {
                try { RetireLocalStorage(); }
                finally
                {
                    if (_ownsStream)
                        await PyBinaryStream.CallAsync(stream, "close", [], span, CurrentContext, asynchronous).ConfigureAwait(false);
                }
            }
        }
    }
}
