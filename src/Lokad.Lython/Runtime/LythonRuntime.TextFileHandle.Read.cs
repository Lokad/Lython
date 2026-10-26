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
        // Input storage is bounded by the current decoded UTF-8 window and raw
        // carry. Accumulated reads admit UTF-8 working capacity before growth;
        // returned values retain their execution/result funding after close.
        // Long streams use bounded host-driven execution batches, not collection
        // of discarded guest values. Input decoding keeps its existing codec path.
        internal sealed class ChunkedTextFileReadState : TextFileState, IPyOwnershipSnapshot
        {
            // The decoded Python window is stored beside byte/carry backing.
            internal const long ShellBytes = 192;
            internal const int DefaultWindowBytes = 16 * 1024;
            internal const int MinimumWindowBytes = 4 * 1024;

            // Raw carry past one window: an incomplete UTF-8 tail plus CR, or
            // UTF-16's CR unit, high surrogate and odd tail (up to five bytes).
            private const int CarrySlackBytes = 8;

            private readonly string _path;
            private readonly GzipFileObject? _gzip;
            private ExecutionContext? _gzipCaller;
            private LythonSourceSpan? _gzipSpan;
            private bool _gzipAsynchronous;
            private readonly ExecutionServices _services;
            private readonly TextEncodingMode _encoding;
            private TextEncodingMode _decodingEncoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly int _windowBytes;
            private byte[]? _raw;
            private int _rawLength;
            private PyString _window = PyString.Empty;
            private long _windowCharge;
            private int _windowConsumed;
            private long _position;
            private long _nextOffset;
            private bool _eof;
            private bool _bomChecked;
            private bool _released;
            internal long OwnedBytes => ShellBytes + (_raw?.LongLength ?? 0) + _windowCharge;

            bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
                => OwnershipSnapshot.Owned(_services.MemoryGovernor, OwnedBytes, out chargeBytes);

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
                _services = context.Services;
                _encoding = encoding;
                _decodingEncoding = encoding;
                _errors = errors;
                _newline = newline;
                _windowBytes = windowBytes;
                context.MemoryGovernor.Reserve(ShellBytes, null);
                context.MemoryGovernor.Commit(ShellBytes);
            }

            internal ChunkedTextFileReadState(GzipFileObject gzip, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                : this("", context, encoding, errors, newline, DefaultWindowBytes) => _gzip = gzip;

            internal async ValueTask<PyString> ReadGzipAsync(long size, bool line, ExecutionContext caller, LythonSourceSpan span, bool asynchronous)
            {
                if (_gzipCaller is not null) throw RuntimeErrors.Value("reentrant gzip text read", span);
                _gzipCaller = caller; _gzipSpan = span; _gzipAsynchronous = asynchronous;
                try
                {
                    // The materialized result has an int-sized representation;
                    // larger requests stop at EOF or that checked representation.
                    var count = size < 0 || size > int.MaxValue ? -1 : (int)size;
                    return asynchronous ? await (line ? ReadLineAsync(count) : ReadAsync(count)).ConfigureAwait(false)
                        : line ? ReadLine(count) : Read(count);
                }
                finally { _gzipCaller = null; _gzipSpan = null; }
            }

            private async ValueTask<bool> PullGzipAsync()
            {
                var bytes = await _gzip!.ReadTextChunkAsync(_gzipCaller!, _gzipSpan!, _gzipAsynchronous).ConfigureAwait(false);
                try { EnsureRaw(); return AcceptPayload(bytes.Memory); }
                finally { _services.MemoryGovernor.RefundUnpublishedValue(bytes); }
            }

            public BigInteger Position => new(_position);

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

                GovernedByteBuilder? builder = null;
                try
                {
                    var remaining = size;
                    while (true)
                    {
                        Refill();
                        if (Drained())
                        {
                            return EmitAccumulated(builder);
                        }

                        var bytes = _window.Utf8Bytes.Span;
                        var end = TextLineScanning.FindLineEndByte(bytes, _windowConsumed, _newline);
                        if (remaining >= 0)
                        {
                            end = Math.Min(end, _window.GetByteIndexAfterRunes(_windowConsumed, remaining));
                            remaining -= CountRunes(_windowConsumed, end);
                        }

                        // A line ending exactly at the window edge may continue past it;
                        // appending and pulling more stays correct, costing one extra
                        // pull in a rare alignment.
                        if (end < bytes.Length || EndsWithTerminator() || remaining == 0)
                        {
                            if (builder is null)
                            {
                                var line = _window.SliceByByteRange(_windowConsumed, end);
                                Advance(end);
                                _services.CheckExecution(null);
                                return line;
                            }

                            AppendRange(builder, _windowConsumed, end);
                            return EmitAccumulated(builder);
                        }

                        builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                        AppendRange(builder, _windowConsumed, bytes.Length);
                    }
                }
                finally { builder?.Release(); }
            }

            public async ValueTask<PyString> ReadLineAsync(int size)
            {
                if (size == 0)
                {
                    return PyString.Empty;
                }

                GovernedByteBuilder? builder = null;
                try
                {
                    var remaining = size;
                    while (true)
                    {
                        await RefillAsync().ConfigureAwait(false);
                        if (Drained())
                        {
                            return EmitAccumulated(builder);
                        }

                        var bytes = _window.Utf8Bytes.Span;
                        var end = TextLineScanning.FindLineEndByte(bytes, _windowConsumed, _newline);
                        if (remaining >= 0)
                        {
                            end = Math.Min(end, _window.GetByteIndexAfterRunes(_windowConsumed, remaining));
                            remaining -= CountRunes(_windowConsumed, end);
                        }

                        if (end < bytes.Length || EndsWithTerminator() || remaining == 0)
                        {
                            if (builder is null)
                            {
                                var line = _window.SliceByByteRange(_windowConsumed, end);
                                Advance(end);
                                _services.CheckExecution(null);
                                return line;
                            }

                            AppendRange(builder, _windowConsumed, end);
                            return EmitAccumulated(builder);
                        }

                        builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                        AppendRange(builder, _windowConsumed, bytes.Length);
                    }
                }
                finally { builder?.Release(); }
            }

            public void Release()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                ReleaseWindow();
                if (_raw is not null)
                {
                    _services.MemoryGovernor.Release(_raw.Length);
                    _raw = null;
                }
            }

            // Construction has exposed no state or emitted values to guest code.
            public void ReleaseUnpublished()
            {
                Release();
                _services.State.MemoryGovernor.RefundUnpublishedValue(this);
            }

            private PyString ReadBounded(int size)
            {
                GovernedByteBuilder? builder = null;
                try
                {
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
                                _services.CheckExecution(null);
                                return piece;
                            }

                            AppendRange(builder, _windowConsumed, take);
                            return EmitAccumulated(builder);
                        }

                        // Reaching the window end may mean an exact fit or a shortfall;
                        // counting runes disambiguates without an extra pull.
                        var got = CountRunes(_windowConsumed, take);
                        builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                        AppendRange(builder, _windowConsumed, take);
                        if (got >= remaining)
                        {
                            return EmitAccumulated(builder);
                        }

                        remaining -= got;
                    }
                }
                finally { builder?.Release(); }
            }

            private async ValueTask<PyString> ReadBoundedAsync(int size)
            {
                GovernedByteBuilder? builder = null;
                try
                {
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
                                _services.CheckExecution(null);
                                return piece;
                            }

                            AppendRange(builder, _windowConsumed, take);
                            return EmitAccumulated(builder);
                        }

                        var got = CountRunes(_windowConsumed, take);
                        builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                        AppendRange(builder, _windowConsumed, take);
                        if (got >= remaining)
                        {
                            return EmitAccumulated(builder);
                        }

                        remaining -= got;
                    }
                }
                finally { builder?.Release(); }
            }

            private PyString ReadAll()
            {
                GovernedByteBuilder? builder = null;
                try
                {
                    while (true)
                    {
                        Refill();
                        if (Drained())
                        {
                            return EmitAccumulated(builder);
                        }

                        builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                        var bytes = _window.Utf8Bytes.Span;
                        AppendRange(builder, _windowConsumed, bytes.Length);
                    }
                }
                finally { builder?.Release(); }
            }

            private async ValueTask<PyString> ReadAllAsync()
            {
                GovernedByteBuilder? builder = null;
                try
                {
                    while (true)
                    {
                        await RefillAsync().ConfigureAwait(false);
                        if (Drained())
                        {
                            return EmitAccumulated(builder);
                        }

                        builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                        var bytes = _window.Utf8Bytes.Span;
                        AppendRange(builder, _windowConsumed, bytes.Length);
                    }
                }
                finally { builder?.Release(); }
            }

            private PyString EmitAccumulated(GovernedByteBuilder? builder)
            {
                if (builder is null || builder.Length == 0) return PyString.Empty;
                var text = builder.ToPyStringAndRelease();
                _services.CheckExecution(null);
                return text;
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

            // Line scanning and rune indexing supply boundaries in the translated
            // UTF-8 window; no second UTF-16 cursor or decoding pass is needed.
            private void Advance(int byteEnd)
            {
                var bytes = _window.Utf8Bytes.Span;
                Debug.Assert(byteEnd >= _windowConsumed && byteEnd <= bytes.Length, "chunked reader advanced outside its window");
                Debug.Assert(byteEnd == bytes.Length || (bytes[byteEnd] & 0xC0) != 0x80, "chunked reader crossed a rune boundary");
                _position += byteEnd - _windowConsumed;
                _windowConsumed = byteEnd;
            }

            private void AppendRange(GovernedByteBuilder builder, int byteStart, int byteEnd)
            {
                Debug.Assert(byteStart == _windowConsumed, "chunked reader appends out of order");
                // Admission and copying finish before this append changes the cursor.
                builder.Append(_window.Utf8Bytes.Span.Slice(byteStart, byteEnd - byteStart));
                Advance(byteEnd);
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

            // Wait for a complete preamble or EOF: three bytes for UTF-8-sig,
            // two for native UTF-16. Explicit UTF-16 byte order keeps BOM text.
            private bool EnsureBomDecided()
            {
                if (_bomChecked)
                {
                    return true;
                }

                if (_encoding == TextEncodingMode.Utf16)
                {
                    if (_rawLength < 2 && !_eof) return false;
                    ReadOnlySpan<byte> source = _raw is null ? [] : _raw.AsSpan(0, _rawLength);
                    _decodingEncoding = ResolveUtf16ByteOrder(ref source, _encoding, stream: true, null);
                    var removed = _rawLength - source.Length;
                    if (removed != 0)
                    {
                        Buffer.BlockCopy(_raw!, removed, _raw!, 0, source.Length);
                        _rawLength = source.Length;
                    }
                    _bomChecked = true;
                    return true;
                }

                if (_encoding != TextEncodingMode.Utf8Bom)
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
            // UTF-16 also retains an odd byte and final high surrogate. At EOF
            // everything flushes, matching truncated-tail error grouping.
            private int DecodeHeadLength()
            {
                var head = _rawLength;
                if (_eof)
                {
                    return head;
                }

                if (IsUtf16Encoding(_encoding))
                {
                    head &= ~1;
                    var bigEndian = _decodingEncoding == TextEncodingMode.Utf16BigEndian;
                    if (head >= 2 && ReadUtf16Unit(_raw!.AsSpan(head - 2), bigEndian) is >= 0xd800 and <= 0xdbff)
                        head -= 2;
                    if (head >= 2 && ReadUtf16Unit(_raw!.AsSpan(head - 2), bigEndian) == '\r')
                        head -= 2;
                    return head;
                }

                if (!IsSingleByteEncoding(_encoding))
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

                if (_gzip is not null) return PullGzipAsync().GetAwaiter().GetResult();
                // The host transfer lands before the carry commits, so missing
                // files (and other host failures) surface with no infrastructure
                // charged, exactly like the prereserve fall-through before them.
                _services.CheckExecution(null);
                var payload = UsesBinaryTextTransport(_encoding)
                    ? _services.ReadHostBytesRange(_path, _nextOffset, _windowBytes, null)
                    : _services.ReadTextUtf8Range(_path, _nextOffset, _windowBytes, null);
                EnsureRaw();
                return AcceptPayload(payload);
            }

            private async ValueTask<bool> PullAsync()
            {
                if (_eof)
                {
                    return false;
                }

                if (_gzip is not null) return await PullGzipAsync().ConfigureAwait(false);
                _services.CheckExecution(null);
                var payload = UsesBinaryTextTransport(_encoding)
                    ? await _services.ReadHostBytesRangeAsync(_path, _nextOffset, _windowBytes, null).ConfigureAwait(false)
                    : await _services.ReadTextUtf8RangeAsync(_path, _nextOffset, _windowBytes, null).ConfigureAwait(false);
                EnsureRaw();
                return AcceptPayload(payload);
            }

            private void EnsureRaw()
            {
                if (_raw is not null)
                {
                    return;
                }

                var length = checked(_windowBytes + CarrySlackBytes);
                _services.MemoryGovernor.Reserve(length, null);
                try
                {
                    var raw = new byte[length];
                    _services.MemoryGovernor.Commit(length);
                    _raw = raw;
                }
                catch
                {
                    _services.MemoryGovernor.ReleaseReserved(length);
                    throw;
                }
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
                    return;
                }

                Debug.Assert(_raw is not null, "window installs from buffered bytes");
                var source = new ReadOnlySpan<byte>(_raw!, 0, head);
                var decoded = string.Empty;
                var ownsDecodedWindow = _encoding is TextEncodingMode.Ascii or TextEncodingMode.Windows1252 || IsUtf16Encoding(_encoding);
                if (ownsDecodedWindow)
                {
                    _window = _encoding == TextEncodingMode.Ascii
                        ? DecodeAsciiText(source, _services, null, _errors, _newline)
                        : _encoding == TextEncodingMode.Windows1252
                            ? DecodeWindows1252Text(source, _services, null, _errors, _newline)
                            : DecodeUtf16Text(source, _decodingEncoding, _services, null, _errors, _newline);
                    _windowCharge = _window.OwnerMemoryGovernor is null ? 0 : _window.CommittedOwnedBytes;
                }
                else if (_encoding == TextEncodingMode.Latin1)
                {
                    decoded = Encoding.UTF8.GetString(DecodeLatin1ToBytes(source, _newline, _services.State.Guards, null));
                }
                else
                {
                    decoded = DecodeUtf8ToString(source, _errors, null, _services.State.Guards);
                    if (_newline == TextNewlineMode.TranslateUniversal)
                    {
                        decoded = NormalizeNewlineString(decoded, _services.State.Guards);
                    }
                }

                if (!ownsDecodedWindow)
                {
                    _window = PyString.FromString(decoded, _services.MemoryGovernor, null);
                    _windowCharge = _window.OwnerMemoryGovernor is null ? 0 : _window.CommittedOwnedBytes;
                }
                _windowConsumed = 0;
                Buffer.BlockCopy(_raw!, head, _raw!, 0, _rawLength - head);
                _rawLength -= head;
            }

            private void ReleaseWindow()
            {
                if (_windowCharge > 0)
                {
                    _services.MemoryGovernor.Release(_windowCharge);
                    _windowCharge = 0;
                }

                _window = PyString.Empty;
                _windowConsumed = 0;
            }

        }
    }
}
