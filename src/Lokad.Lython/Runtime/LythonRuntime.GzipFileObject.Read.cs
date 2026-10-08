using System.Buffers.Binary;
using System.IO.Compression;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class GzipFileObject
    {
        private static LythonRuntimeException GzipEnd(LythonSourceSpan span)
            => new("EOFError", "Compressed file ended before the end-of-stream marker was reached", span);

        private async ValueTask<bool> ReadHeaderAsync(LythonSourceSpan span, bool asynchronous)
        {
            _cursor ??= new GzipStreamCursor(this);
            var magic = await _cursor.TakeAsync(2, exact: false, span, asynchronous).ConfigureAwait(false);
            if (magic.Length == 0) { _eof = true; return false; }
            if (magic.Length != 2 || magic[0] != 31 || magic[1] != 139)
                throw BadGzip("Not a gzipped file", span);
            var header = await _cursor.TakeAsync(8, exact: true, span, asynchronous).ConfigureAwait(false);
            if (header[0] != 8) throw BadGzip("Unknown compression method", span);
            var flags = header[1];
            var time = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(2, 4));
            if ((flags & 4) != 0)
            {
                var lengthBytes = await _cursor.TakeAsync(2, exact: true, span, asynchronous).ConfigureAwait(false);
                var length = BinaryPrimitives.ReadUInt16LittleEndian(lengthBytes);
                await _cursor.SkipAsync(length, span, asynchronous).ConfigureAwait(false);
            }
            if ((flags & 8) != 0) await _cursor.SkipStringAsync(span, asynchronous).ConfigureAwait(false);
            if ((flags & 16) != 0) await _cursor.SkipStringAsync(span, asynchronous).ConfigureAwait(false);
            if ((flags & 2) != 0) await _cursor.SkipAsync(2, span, asynchronous).ConfigureAwait(false);
            _mtime = time;
            _context.MemoryGovernor.Reserve(ZlibModule.InflationScratchBytes, span);
            try { _native = new DeflateStream(_cursor, CompressionMode.Decompress, true); }
            catch { _context.MemoryGovernor.ReleaseReserved(ZlibModule.InflationScratchBytes); throw; }
            _context.MemoryGovernor.Commit(ZlibModule.InflationScratchBytes);
            _nativeCharge = ZlibModule.InflationScratchBytes;
            _crc = uint.MaxValue; _memberSize = 0; _memberEnded = false;
            Changed();
            return true;
        }

        private async ValueTask ReadTrailerAsync(LythonSourceSpan span, bool asynchronous)
        {
            var trailer = await _cursor!.TakeAsync(8, exact: true, span, asynchronous).ConfigureAwait(false);
            if (BinaryPrimitives.ReadUInt32LittleEndian(trailer) != ~_crc)
                throw BadGzip("CRC check failed", span);
            if (BinaryPrimitives.ReadUInt32LittleEndian(trailer.AsSpan(4)) != _memberSize)
                throw BadGzip("Incorrect length of data produced", span);
            ReleaseNative();
            _memberEnded = false;
            // Padding is permitted after a validated member, before another
            // header or the end of the input. It is not an initial header.
            await _cursor.SkipPaddingAsync(span, asynchronous).ConfigureAwait(false);
        }

        private async ValueTask RefillAsync(LythonSourceSpan span, bool asynchronous)
        {
            if (_windowPosition < _windowLength || _eof) return;
            if (_window.Length == 0)
            {
                _context.MemoryGovernor.Reserve(ReadBufferBytes, span);
                try { _window = new byte[ReadBufferBytes]; }
                catch { _context.MemoryGovernor.ReleaseReserved(ReadBufferBytes); throw; }
                _context.MemoryGovernor.Commit(ReadBufferBytes); Changed();
            }
            _windowPosition = _windowLength = 0;
            while (!_eof)
            {
                _context.CheckExecution(span);
                if (_memberEnded) await ReadTrailerAsync(span, asynchronous).ConfigureAwait(false);
                if (_native is null && !await ReadHeaderAsync(span, asynchronous).ConfigureAwait(false)) return;
                while (_windowLength < _window.Length)
                {
                    if ((_windowLength & 255) == 0) _context.CheckExecution(span);
                    int count;
                    try
                    {
                        count = asynchronous
                            ? await _native!.ReadAsync(_window.AsMemory(_windowLength)).ConfigureAwait(false)
                            : _native!.Read(_window.AsSpan(_windowLength));
                    }
                    catch (InvalidDataException error)
                    {
                        throw new LythonRuntimeException(ModuleException("zlib", "error"), error.Message, span, error, null);
                    }
                    if (count == 0) { _memberEnded = true; break; }
                    _crc = Crc32.Update(_crc, _window.AsSpan(_windowLength, count));
                    _memberSize = unchecked(_memberSize + (uint)count);
                    _windowLength += count;
                }
                // Return member output before examining its trailer. A small
                // read may succeed even though a later read discovers damage.
                if (_windowLength > 0) return;
            }
        }

        private async ValueTask<PyBytes> ReadAsync(long size, bool line, LythonSourceSpan span, bool asynchronous)
        {
            EnsureReadable(span, bufferedBase: line);
            if (!line && size < -1) throw RuntimeErrors.Value("read length must be non-negative or -1", span);
            var output = new GovernedByteBuilder(_context.MemoryGovernor, span);
            try
            {
                while (size != 0)
                {
                    await RefillAsync(span, asynchronous).ConfigureAwait(false);
                    if (_eof) break;
                    _context.CheckExecution(span);
                    var count = _windowLength - _windowPosition;
                    if (size > 0) count = (int)Math.Min(size, count);
                    var ended = false;
                    if (line)
                    {
                        var found = _window.AsSpan(_windowPosition, count).IndexOf((byte)10);
                        if (found >= 0) { count = found + 1; ended = true; }
                    }
                    output.Append(_window.AsSpan(_windowPosition, count));
                    _windowPosition += count; _position += count;
                    if (size > 0) size -= count;
                    if (ended) break;
                }
                using var copy = _context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(output.Length), span);
                var bytes = output.WrittenSpan.ToArray();
                output.Release(); copy.Dispose();
                return CreateBytes(bytes, _context, span);
            }
            finally { output.Release(); }
        }

        private sealed class GzipStreamCursor(GzipFileObject owner) : Stream
        {
            private PyBytes? _input;
            private int _position;
            private long _consumed;
            private bool _ended;
            private int Remaining => _input is null ? 0 : _input.Length - _position;
            private async ValueTask<bool> AcquireAsync(int count, LythonSourceSpan span, bool asynchronous)
            {
                if (Remaining > 0) return true;
                if (_ended) return false;
                owner._context.CheckExecution(span);
                _input = await PyBinaryStream.ReadAsync(owner._stream, count, span, owner._context, asynchronous).ConfigureAwait(false);
                _position = 0; _ended = _input.Length == 0;
                return !_ended;
            }
            internal async ValueTask<byte[]> TakeAsync(int count, bool exact, LythonSourceSpan span, bool asynchronous)
            {
                // Fixed framing fields are at most eight bytes. FEXTRA/string
                // bodies are discarded through bounded loops, never materialized.
                var bytes = new byte[count];
                var written = 0;
                do
                {
                    if (!await AcquireAsync(count - written, span, asynchronous).ConfigureAwait(false)) break;
                    var take = Math.Min(count - written, Remaining);
                    _input!.Bytes.Slice(_position, take).CopyTo(bytes.AsSpan(written));
                    _position += take; _consumed += take; written += take;
                    if (!exact) break;
                } while (written < count);
                if (exact && written != count) throw GzipEnd(span);
                return written == count ? bytes : bytes[..written];
            }
            internal async ValueTask SkipAsync(int count, LythonSourceSpan span, bool asynchronous)
            {
                while (count > 0)
                {
                    owner._context.CheckExecution(span);
                    if (!await AcquireAsync(Math.Min(count, 8192), span, asynchronous).ConfigureAwait(false)) throw GzipEnd(span);
                    var take = Math.Min(count, Remaining);
                    _position += take; _consumed += take; count -= take;
                }
            }
            internal async ValueTask SkipStringAsync(LythonSourceSpan span, bool asynchronous)
            {
                while (true)
                {
                    owner._context.CheckExecution(span);
                    if (!await AcquireAsync(1, span, asynchronous).ConfigureAwait(false)) return;
                    var value = _input!.Bytes[_position++]; _consumed++;
                    if (value == 0) return;
                }
            }
            internal async ValueTask SkipPaddingAsync(LythonSourceSpan span, bool asynchronous)
            {
                while (await AcquireAsync(1, span, asynchronous).ConfigureAwait(false))
                {
                    if ((_consumed & 255) == 0) owner._context.CheckExecution(span);
                    if (_input!.Bytes[_position] != 0) return;
                    _position++; _consumed++;
                }
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => _consumed; set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
            public override int Read(Span<byte> buffer)
            {
                if (buffer.IsEmpty || !AcquireAsync(65536, owner._creationSpan, false).GetAwaiter().GetResult()) return 0;
                if ((_consumed & 255) == 0) owner._context.CheckExecution(owner._creationSpan);
                buffer[0] = _input!.Bytes[_position++]; _consumed++;
                return 1;
            }
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (buffer.IsEmpty || !await AcquireAsync(65536, owner._creationSpan, true).ConfigureAwait(false)) return 0;
                if ((_consumed & 255) == 0) owner._context.CheckExecution(owner._creationSpan);
                buffer.Span[0] = _input!.Bytes[_position++]; _consumed++;
                return 1;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
