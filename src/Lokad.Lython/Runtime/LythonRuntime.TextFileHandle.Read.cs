using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Streams host text in fixed byte windows instead of loading the whole file
        // at open: only the current decoded window, a small raw carry and emitted
        // values stay charged, so full scans no longer scale with discarded content.
        // Windows decode through the same helpers as buffered reads and emit values
        // through the same governed slicing, so values and per-value charges match;
        // window charges release on replacement and the carry releases at close.
        // Window scratch (decoded strings, transient byte arrays) stays bounded by
        // the window, so it needs no reservation beyond the held infrastructure.
        internal sealed class ChunkedTextFileReadState : TextFileState
        {
            internal const int DefaultWindowBytes = 16 * 1024;
            internal const int MinimumWindowBytes = 4 * 1024;

            // Raw carry past one window: an incomplete UTF-8 tail (3 bytes), the
            // universal-newline carriage-return holdback (1 byte), plus slack.
            private const int CarrySlackBytes = 8;

            private readonly string _path;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly int _windowBytes;
            private byte[]? _raw;
            private int _rawLength;
            private PyString _window = PyString.Empty;
            private string _decoded = string.Empty;
            private long _windowCharge;
            private int _windowConsumed;
            private int _charConsumed;
            private long _position;
            private long _nextOffset;
            private long _cumulativeBytes;
            private bool _eof;
            private bool _bomChecked;
            private bool _released;
            private readonly ChargeReclamationPool _pool;
            private int _emissions;

            internal ChunkedTextFileReadState(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                int windowBytes)
                : base(TextFileMode.Read)
            {
                _path = path;
                _context = context;
                _encoding = encoding;
                _errors = errors;
                _newline = newline;
                _windowBytes = windowBytes;
                _pool = new ChargeReclamationPool(context.MemoryGovernor);
                context.State.RegisterPool(this, _pool);
            }

            public BigInteger Position => new(_position);

            // Warms the first window so open() surfaces missing files and leading
            // decoding failures exactly like buffered reads instead of going lazy.
            public void Prime() => Refill();

            public async ValueTask PrimeAsync() => await RefillAsync().ConfigureAwait(false);

            public PyString Read(int size)
            {
                if (size == 0)
                {
                    return PyString.Empty;
                }

                return size < 0 ? ReadAll() : ReadBounded(size);
            }

            public ValueTask<PyString> ReadAsync(int size)
            {
                if (size == 0)
                {
                    return ValueTask.FromResult(PyString.Empty);
                }

                return size < 0 ? ReadAllAsync() : ReadBoundedAsync(size);
            }

            public PyString ReadLine(int size)
            {
                if (size == 0)
                {
                    return PyString.Empty;
                }

                StringBuilder? builder = null;
                while (true)
                {
                    Refill();
                    if (Drained())
                    {
                        return EmitAccumulated(builder);
                    }

                    var bytes = _window.Utf8Bytes.Span;
                    var end = TextLineScanning.FindLineEndByte(bytes, _windowConsumed, _newline);
                    if (size >= 0)
                    {
                        end = Math.Min(end, _window.GetByteIndexAfterRunes(_windowConsumed, size));
                    }

                    // A line ending exactly at the window edge may continue past it;
                    // appending and pulling more stays correct, costing one extra
                    // pull in a rare alignment.
                    if (end < bytes.Length || EndsWithTerminator())
                    {
                        if (builder is null)
                        {
                            var line = _window.SliceByByteRange(_windowConsumed, end);
                            Advance(end);
                            _context.ObserveString(line, null); TrackEmission(line);
                            return line;
                        }

                        AppendRange(builder, _windowConsumed, end);
                        return EmitAccumulated(builder);
                    }

                    builder ??= new StringBuilder();
                    AppendRange(builder, _windowConsumed, bytes.Length);
                }
            }

            public async ValueTask<PyString> ReadLineAsync(int size)
            {
                if (size == 0)
                {
                    return PyString.Empty;
                }

                StringBuilder? builder = null;
                while (true)
                {
                    await RefillAsync().ConfigureAwait(false);
                    if (Drained())
                    {
                        return EmitAccumulated(builder);
                    }

                    var bytes = _window.Utf8Bytes.Span;
                    var end = TextLineScanning.FindLineEndByte(bytes, _windowConsumed, _newline);
                    if (size >= 0)
                    {
                        end = Math.Min(end, _window.GetByteIndexAfterRunes(_windowConsumed, size));
                    }

                    if (end < bytes.Length || EndsWithTerminator())
                    {
                        if (builder is null)
                        {
                            var line = _window.SliceByByteRange(_windowConsumed, end);
                            Advance(end);
                            _context.ObserveString(line, null); TrackEmission(line);
                            return line;
                        }

                        AppendRange(builder, _windowConsumed, end);
                        return EmitAccumulated(builder);
                    }

                    builder ??= new StringBuilder();
                    AppendRange(builder, _windowConsumed, bytes.Length);
                }
            }

            public void Release()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _pool.Sweep(full: true);
                ReleaseWindow();
                if (_raw is not null)
                {
                    _context.MemoryGovernor.Release(_raw.Length);
                    _raw = null;
                }
            }

            private PyString ReadBounded(int size)
            {
                StringBuilder? builder = null;
                var remaining = size;
                while (true)
                {
                    Refill();
                    if (Drained())
                    {
                        return EmitAccumulated(builder);
                    }

                    var take = _window.GetByteIndexAfterRunes(_windowConsumed, remaining);
                    if (take < _window.Utf8Bytes.Length)
                    {
                        if (builder is null)
                        {
                            var piece = _window.SliceByByteRange(_windowConsumed, take);
                            Advance(take);
                            _context.ObserveString(piece, null); TrackEmission(piece);
                            return piece;
                        }

                        AppendRange(builder, _windowConsumed, take);
                        return EmitAccumulated(builder);
                    }

                    // Reaching the window end may mean an exact fit or a shortfall;
                    // counting runes disambiguates without an extra pull.
                    var got = CountRunes(_windowConsumed, take);
                    builder ??= new StringBuilder();
                    AppendRange(builder, _windowConsumed, take);
                    if (got >= remaining)
                    {
                        return EmitAccumulated(builder);
                    }

                    remaining -= got;
                }
            }

            private async ValueTask<PyString> ReadBoundedAsync(int size)
            {
                StringBuilder? builder = null;
                var remaining = size;
                while (true)
                {
                    await RefillAsync().ConfigureAwait(false);
                    if (Drained())
                    {
                        return EmitAccumulated(builder);
                    }

                    var take = _window.GetByteIndexAfterRunes(_windowConsumed, remaining);
                    if (take < _window.Utf8Bytes.Length)
                    {
                        if (builder is null)
                        {
                            var piece = _window.SliceByByteRange(_windowConsumed, take);
                            Advance(take);
                            _context.ObserveString(piece, null); TrackEmission(piece);
                            return piece;
                        }

                        AppendRange(builder, _windowConsumed, take);
                        return EmitAccumulated(builder);
                    }

                    var got = CountRunes(_windowConsumed, take);
                    builder ??= new StringBuilder();
                    AppendRange(builder, _windowConsumed, take);
                    if (got >= remaining)
                    {
                        return EmitAccumulated(builder);
                    }

                    remaining -= got;
                }
            }

            private PyString ReadAll()
            {
                StringBuilder? builder = null;
                while (true)
                {
                    Refill();
                    if (Drained())
                    {
                        return EmitAccumulated(builder);
                    }

                    builder ??= new StringBuilder();
                    var bytes = _window.Utf8Bytes.Span;
                    AppendRange(builder, _windowConsumed, bytes.Length);
                }
            }

            private async ValueTask<PyString> ReadAllAsync()
            {
                StringBuilder? builder = null;
                while (true)
                {
                    await RefillAsync().ConfigureAwait(false);
                    if (Drained())
                    {
                        return EmitAccumulated(builder);
                    }

                    builder ??= new StringBuilder();
                    var bytes = _window.Utf8Bytes.Span;
                    AppendRange(builder, _windowConsumed, bytes.Length);
                }
            }

            private PyString EmitAccumulated(StringBuilder? builder)
            {
                if (builder is null || builder.Length == 0)
                {
                    return PyString.Empty;
                }

                // Multi-window emissions carry the same 128-plus-length charge as
                // whole-buffer slices of the same content, then observe the same
                // string-length limit that open-time observation enforced before.
                // Growth reserves up front like materializing drains elsewhere.
                using var reservation = _context.MemoryGovernor.ReserveTemporary(0, null);
                reservation.Grow(32L + (4L * builder.Length), null);
                var text = PyString.FromString(builder.ToString(), _context.MemoryGovernor, null);
                _context.ObserveString(text, null);
                TrackEmission(text);
                return text;
            }

            // Registers emitted values so sweeps release charges for lines the
            // guest dropped while keeping every retained alias charged, exactly
            // like CSV field payloads. Sweeps run every 256 emissions so the
            // registry itself stays a bounded 16KB of young-tier entries.
            private void TrackEmission(PyString value)
            {
                _pool.TrackString(value);
                if ((++_emissions & 255) == 0)
                {
                    _pool.Sweep();
                }
            }

            private bool Drained()
                => _eof && _rawLength == 0 && _windowConsumed >= _window.Utf8Bytes.Length;

            // A terminator ending exactly at the window edge is complete: the
            // carriage-return holdback reunites split CRLF pairs, so a trailing
            // newline can never await bytes from the next window. Without this,
            // boundary-aligned lines would swallow their terminator as content
            // and merge with the following line.
            private bool EndsWithTerminator()
            {
                var bytes = _window.Utf8Bytes.Span;
                var length = bytes.Length;
                if (length == _windowConsumed || bytes[length - 1] != (byte)'\n')
                {
                    return false;
                }

                return _newline switch
                {
                    TextNewlineMode.TranslateUniversal or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed => true,
                    TextNewlineMode.PreserveCarriageReturnLineFeed => length - _windowConsumed >= 2 && bytes[length - 2] == (byte)'\r',
                    _ => false,
                };
            }

            // Advances both cursors over a rune-aligned byte range. Callers derive
            // ends from line scanning or rune indexing, which always land on
            // boundaries; the assertion pins that contract during development.
            private int Advance(int byteEnd)
            {
                var span = _decoded.AsSpan();
                var ci = _charConsumed;
                var bi = _windowConsumed;
                var start = bi;
                while (bi < byteEnd)
                {
                    if (Rune.DecodeFromUtf16(span[ci..], out var rune, out var consumed) != OperationStatus.Done)
                    {
                        ci++;
                        bi++;
                        continue;
                    }

                    ci += consumed;
                    bi += rune.Utf8SequenceLength;
                }

                Debug.Assert(bi == byteEnd, "chunked reader crossed a rune boundary");
                _windowConsumed = bi;
                _charConsumed = ci;
                _position += bi - start;
                return ci;
            }

            private void AppendRange(StringBuilder builder, int byteStart, int byteEnd)
            {
                Debug.Assert(byteStart == _windowConsumed, "chunked reader appends out of order");
                var charStart = _charConsumed;
                var charEnd = Advance(byteEnd);
                builder.Append(_decoded, charStart, charEnd - charStart);
            }

            private int CountRunes(int byteStart, int byteEnd)
            {
                // Window content is decoded and valid by construction, so every
                // unit decodes; the fallback only guards the impossible.
                var count = 0;
                var span = _window.Utf8Bytes.Span[byteStart..byteEnd];
                while (!span.IsEmpty)
                {
                    if (Rune.DecodeFromUtf8(span, out _, out var consumed) != OperationStatus.Done)
                    {
                        consumed = 1;
                    }

                    count++;
                    span = span[consumed..];
                }

                return count;
            }

            private void Refill()
            {
                while (_windowConsumed >= _window.Utf8Bytes.Length)
                {
                    ReleaseWindow();
                    if (_eof && _rawLength == 0)
                    {
                        return;
                    }

                    DecodeNextWindow();
                }
            }

            private async ValueTask RefillAsync()
            {
                while (_windowConsumed >= _window.Utf8Bytes.Length)
                {
                    ReleaseWindow();
                    if (_eof && _rawLength == 0)
                    {
                        return;
                    }

                    await DecodeNextWindowAsync().ConfigureAwait(false);
                }
            }

            private void DecodeNextWindow()
            {
                while (true)
                {
                    if (!EnsureBomDecided())
                    {
                        Pull();
                        continue;
                    }

                    if (_rawLength == 0)
                    {
                        if (_eof || !Pull())
                        {
                            InstallWindow(0);
                            return;
                        }

                        continue;
                    }

                    var head = DecodeHeadLength();
                    if (head == 0)
                    {
                        if (_eof || !Pull())
                        {
                            head = _rawLength;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    InstallWindow(head);
                    return;
                }
            }

            private async ValueTask DecodeNextWindowAsync()
            {
                while (true)
                {
                    if (!EnsureBomDecided())
                    {
                        await PullAsync().ConfigureAwait(false);
                        continue;
                    }

                    if (_rawLength == 0)
                    {
                        if (_eof || !await PullAsync().ConfigureAwait(false))
                        {
                            InstallWindow(0);
                            return;
                        }

                        continue;
                    }

                    var head = DecodeHeadLength();
                    if (head == 0)
                    {
                        if (_eof || !await PullAsync().ConfigureAwait(false))
                        {
                            head = _rawLength;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    InstallWindow(head);
                    return;
                }
            }

            // Defers the UTF-8-sig preamble decision until three bytes buffer or the
            // file ends, mirroring whole-buffer stripping (which needs three bytes).
            private bool EnsureBomDecided()
            {
                if (_bomChecked || _encoding != TextEncodingMode.Utf8Bom)
                {
                    _bomChecked = true;
                    return true;
                }

                if (_rawLength < 3 && !_eof)
                {
                    return false;
                }

                _bomChecked = true;
                Debug.Assert(_raw is not null, "BOM check runs on buffered bytes");
                var preamble = _raw!;
                if (_rawLength >= 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF)
                {
                    Buffer.BlockCopy(preamble, 3, preamble, 0, _rawLength - 3);
                    _rawLength -= 3;
                }

                return true;
            }

            // Splits decodable bytes from the carry: an incomplete UTF-8 tail stays
            // raw so validation sees whole units exactly like whole-buffer decoding,
            // and a trailing carriage return waits for its pair so split CRLF and
            // lone-CR endings translate identically. Latin-1 needs no unit carry.
            // At EOF everything flushes, matching truncated-tail handling.
            private int DecodeHeadLength()
            {
                var head = _rawLength;
                if (_eof)
                {
                    return head;
                }

                if (_encoding != TextEncodingMode.Latin1)
                {
                    var tail = Math.Min(4, head);
                    for (var k = tail; k >= 1; k--)
                    {
                        if (Rune.DecodeFromUtf8(_raw!.AsSpan(head - k, k), out _, out _) == OperationStatus.NeedMoreData)
                        {
                            head -= k;
                            break;
                        }
                    }
                }

                Debug.Assert(_raw is not null, "head scan runs on buffered bytes");
                if (head > 0 && _raw![head - 1] == (byte)'\r')
                {
                    head--;
                }

                return head;
            }

            private bool Pull()
            {
                if (_eof)
                {
                    return false;
                }

                // The host transfer lands before the carry commits, so missing
                // files (and other host failures) surface with no infrastructure
                // charged, exactly like the prereserve fall-through before them.
                _context.RegisterHostCall(null);
                var payload = _encoding == TextEncodingMode.Latin1
                    ? _context.ReadHostBytesRange(_path, _nextOffset, _windowBytes, null)
                    : _context.ReadTextUtf8Range(_path, _nextOffset, _windowBytes, null);
                EnsureRaw();
                return AcceptPayload(payload);
            }

            private async ValueTask<bool> PullAsync()
            {
                if (_eof)
                {
                    return false;
                }

                _context.RegisterHostCall(null);
                var payload = _encoding == TextEncodingMode.Latin1
                    ? await _context.ReadHostBytesRangeAsync(_path, _nextOffset, _windowBytes, null).ConfigureAwait(false)
                    : await _context.ReadTextUtf8RangeAsync(_path, _nextOffset, _windowBytes, null).ConfigureAwait(false);
                EnsureRaw();
                return AcceptPayload(payload);
            }

            private void EnsureRaw()
            {
                if (_raw is not null)
                {
                    return;
                }

                _raw = new byte[checked(_windowBytes + CarrySlackBytes)];
                _context.MemoryGovernor.Reserve(_raw.Length, null);
                _context.MemoryGovernor.Commit(_raw.Length);
            }

            private bool AcceptPayload(ReadOnlyMemory<byte> payload)
            {
                // Hosts must honor the requested window; clamp defensively so a
                // chatty host cannot overflow the carry (offsets stay exact).
                Debug.Assert(_raw is not null && _rawLength + _windowBytes <= _raw.Length, "chunked carry overflowed its window");
                var take = Math.Min(payload.Length, _windowBytes);
                if (take == 0)
                {
                    _eof = true;
                    return false;
                }

                _cumulativeBytes = RuntimeMemoryEstimates.SaturatingAdd(_cumulativeBytes, take);
                if (_context.Limits.MaxHostReadBytes is { } maxReadBytes && _cumulativeBytes > maxReadBytes)
                {
                    throw RuntimeErrors.Runtime($"host {TextReadKind()} read exceeded maximum bytes ({maxReadBytes})", null);
                }

                payload.Span.Slice(0, take).CopyTo(_raw!.AsSpan(_rawLength));
                _rawLength += take;
                _nextOffset += take;
                return true;
            }

            private void InstallWindow(int head)
            {
                ReleaseWindow();
                if (head == 0)
                {
                    _window = PyString.Empty;
                    _decoded = string.Empty;
                    return;
                }

                Debug.Assert(_raw is not null, "window installs from buffered bytes");
                var source = new ReadOnlySpan<byte>(_raw!, 0, head);
                string decoded;
                if (_encoding == TextEncodingMode.Latin1)
                {
                    decoded = Encoding.UTF8.GetString(DecodeLatin1ToBytes(source, _newline));
                }
                else
                {
                    decoded = DecodeUtf8ToString(source, _errors, null);
                    if (_newline == TextNewlineMode.TranslateUniversal)
                    {
                        decoded = NormalizeNewlineString(decoded);
                    }
                }

                var utf8 = Encoding.UTF8.GetBytes(decoded);
                _window = utf8.Length == 0
                    ? PyString.Empty
                    : PyString.FromOwnedUtf8(utf8, _context.MemoryGovernor, null);
                _windowCharge = utf8.Length == 0 ? 0 : PyString.EstimateApproximateBytes(utf8.Length);
                _decoded = decoded;
                _windowConsumed = 0;
                _charConsumed = 0;
                Buffer.BlockCopy(_raw!, head, _raw!, 0, _rawLength - head);
                _rawLength -= head;
            }

            private void ReleaseWindow()
            {
                if (_windowCharge > 0)
                {
                    _context.MemoryGovernor.Release(_windowCharge);
                    _windowCharge = 0;
                }

                _window = PyString.Empty;
                _decoded = string.Empty;
                _windowConsumed = 0;
                _charConsumed = 0;
            }

            private string TextReadKind() => _encoding == TextEncodingMode.Latin1 ? "binary" : "text";
        }
    }
}
