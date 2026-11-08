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
        // carry. Unchanged UTF-8 windows borrow the admitted raw prefix until
        // consumption; transformed windows retain their codec output funding.
        // Accumulated reads admit capacity before growth and returned strings
        // own independent storage. Long streams use bounded host-driven batches.
        internal sealed class ChunkedTextFileReadState : TextFileState, IPyOwnershipSnapshot, IDisposable
        {
            // The decoded Python window is stored beside byte/carry backing.
            internal const long ShellBytes = 192;
            internal const int DefaultWindowBytes = 16 * 1024;
            internal const int MinimumWindowBytes = 4 * 1024;

            // Raw carry past one window: an incomplete UTF-8 tail plus CR, or
            // UTF-16's CR unit, high surrogate and odd tail (up to five bytes).
            private const int CarrySlackBytes = 8;

            private readonly OpenedByteFile? _file;
            private readonly GzipFileObject? _gzip;
            private ExecutionContext? _gzipCaller;
            private LythonSourceSpan? _gzipSpan;
            private bool _gzipAsynchronous;
            private readonly ExecutionState _services;
            private readonly TextEncodingMode _encoding;
            private TextEncodingMode _decodingEncoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private int _windowBytes;
            private byte[]? _raw;
            private int _rawLength;
            private ReadOnlyMemory<byte> _window;
            private int _rawWindowLength;
            private long _windowCharge;
            private int _windowConsumed;
            private long _position;
            private bool _eof;
            private bool _bomChecked;
            private bool _released;
            private bool _reading;
            internal long OwnedBytes => ShellBytes + (_raw?.LongLength ?? 0) + _windowCharge;

            bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
                => OwnershipSnapshot.Owned(_services.MemoryGovernor, OwnedBytes, out chargeBytes);

            internal ChunkedTextFileReadState(
                OpenedByteFile? file,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                int windowBytes)
                : base(TextFileMode.Read)
            {
                _file = file;
                _services = context.State;
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
                : this(null, context, encoding, errors, newline, DefaultWindowBytes) => _gzip = gzip;

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
                EnsureRaw();
                EnsureNotReleased();
                var bytes = await _gzip!.ReadTextChunkAsync(_gzipCaller!, _gzipSpan!, _gzipAsynchronous).ConfigureAwait(false);
                try { EnsureNotReleased(); return AcceptGzipPayload(bytes.Memory); }
                finally { _services.MemoryGovernor.RefundUnpublishedValue(bytes); }
            }

            internal void SetLengthHint(long? length)
            {
                if (length is > 0 and <= DefaultWindowBytes)
                    _windowBytes = Math.Max(MinimumWindowBytes, (int)length.Value);
            }

            public BigInteger Position => new(_position);

            internal async ValueTask<int> ReadSymbolAsync(bool asynchronous)
            {
                BeginRead();
                var failed = true;
                try
                {
                    if (asynchronous) await RefillAsync().ConfigureAwait(false); else Refill();
                    var result = -1;
                    if (!Drained())
                    {
                        Rune.DecodeFromUtf8(_window.Span[_windowConsumed..], out var rune, out var consumed);
                        Advance(_windowConsumed + consumed);
                        result = rune.Value;
                    }
                    failed = false;
                    return result;
                }
                finally
                {
                    if (asynchronous) await EndReadAsync(failed).ConfigureAwait(false);
                    else EndRead(failed);
                }
            }

            internal async ValueTask<int> ReadGzipSymbolAsync(ExecutionContext caller, LythonSourceSpan span, bool asynchronous)
            {
                if (_gzipCaller is not null) throw RuntimeErrors.Value("reentrant gzip text read", span);
                _gzipCaller = caller; _gzipSpan = span; _gzipAsynchronous = asynchronous;
                try { return await ReadSymbolAsync(asynchronous).ConfigureAwait(false); }
                finally { _gzipCaller = null; _gzipSpan = null; }
            }

            public PyString Read(int size)
            {
                BeginRead();
                var failed = true;
                try
                {
                    var result = size == 0 ? PyString.Empty : size < 0 ? ReadAll() : ReadBounded(size);
                    failed = false;
                    return result;
                }
                finally { EndRead(failed); }
            }

            public ValueTask<PyString> ReadAsync(int size)
            {
                BeginRead();
                ValueTask<PyString> pending;
                try { pending = size == 0 ? new(PyString.Empty) : size < 0 ? ReadAllAsync() : ReadBoundedAsync(size); }
                catch (Exception error) { pending = ValueTask.FromException<PyString>(error); }
                return CompleteReadAsync(pending);
            }

            private ValueTask<PyString> CompleteReadAsync(ValueTask<PyString> pending)
            {
                if (!pending.IsCompleted || _released) return AwaitReadAsync(pending);
                try { return new(pending.GetAwaiter().GetResult()); }
                finally { _reading = false; }
            }

            private async ValueTask<PyString> AwaitReadAsync(ValueTask<PyString> pending)
            {
                var failed = true;
                try
                {
                    var result = await pending.ConfigureAwait(false);
                    failed = false;
                    return result;
                }
                finally { await EndReadAsync(failed).ConfigureAwait(false); }
            }

            public PyString ReadLine(int size)
                => ReadLineOwned(size, null);

            public ValueTask<PyString> ReadLineAsync(int size)
                => ReadLineOwnedAsync(size, null);

            private PyString ReadLineOwned(int size, GovernedByteBuilder? destination)
            {
                BeginRead();
                var failed = true;
                try
                {
                    var result = ReadLineCore(size, asynchronous: false, destination).GetAwaiter().GetResult();
                    failed = false;
                    return result;
                }
                finally { EndRead(failed); }
            }

            private ValueTask<PyString> ReadLineOwnedAsync(int size, GovernedByteBuilder? destination)
            {
                BeginRead();
                ValueTask<PyString> pending;
                try { pending = ReadLineCore(size, asynchronous: true, destination); }
                catch (Exception error) { pending = ValueTask.FromException<PyString>(error); }
                return CompleteReadAsync(pending);
            }

            // Native consumers own their destination through the complete line.
            // The same scanner appends directly; no intermediate string escapes.
            internal bool ReadLineInto(GovernedByteBuilder destination)
            {
                var before = destination.Length;
                _ = ReadLineOwned(-1, destination);
                return destination.Length != before;
            }

            internal ValueTask<bool> ReadLineIntoAsync(GovernedByteBuilder destination)
            {
                var before = destination.Length;
                var line = ReadLineOwnedAsync(-1, destination);
                if (!line.IsCompletedSuccessfully) return AwaitLineIntoAsync(line, destination, before);
                _ = line.GetAwaiter().GetResult();
                return new(destination.Length != before);
            }

            private static async ValueTask<bool> AwaitLineIntoAsync(ValueTask<PyString> pending,
                GovernedByteBuilder destination, int before)
            {
                _ = await pending.ConfigureAwait(false);
                return destination.Length != before;
            }

            private ValueTask<PyString> ReadLineCore(int size, bool asynchronous,
                GovernedByteBuilder? destination = null)
            {
                var ownsBuilder = destination is null;
                var builder = destination;
                var remaining = size;
                var transferred = false;
                try
                {
                    if (size == 0) return new(PyString.Empty);
                    while (true)
                    {
                        // BeginRead checks entry; refills and completed lines
                        // keep their checks through each bounded window.
                        if (_windowConsumed >= _window.Length)
                        {
                            if (asynchronous)
                            {
                                var refill = RefillAsync();
                                if (!refill.IsCompleted)
                                {
                                    var result = ReadLineAfterRefillAsync(refill, remaining, builder, ownsBuilder);
                                    transferred = true;
                                    return result;
                                }
                                refill.GetAwaiter().GetResult();
                            }
                            else Refill();
                        }
                        if (TryReadLineWindow(ref remaining, ref builder, ownsBuilder, out var line))
                        {
                            return new(line);
                        }
                    }
                }
                finally
                {
                    if (!transferred && ownsBuilder) builder?.Release();
                }
            }

            // A pending refill transfers the private builder to this one iterative
            // continuation. Caller-owned destinations remain with their caller.
            private async ValueTask<PyString> ReadLineAfterRefillAsync(ValueTask refill, int remaining,
                GovernedByteBuilder? builder, bool ownsBuilder)
            {
                try
                {
                    await refill.ConfigureAwait(false);
                    while (true)
                    {
                        _services.CheckExecution(null);
                        if (TryReadLineWindow(ref remaining, ref builder, ownsBuilder, out var line))
                        {
                            return line;
                        }
                        await RefillAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (ownsBuilder) builder?.Release();
                }
            }

            private bool TryReadLineWindow(ref int remaining, ref GovernedByteBuilder? builder,
                bool ownsBuilder, out PyString line)
            {
                if (Drained())
                {
                    line = ownsBuilder ? EmitAccumulated(builder) : PyString.Empty;
                    return true;
                }

                var bytes = _window.Span;
                var end = TextLineScanning.FindLineEndByte(bytes, _windowConsumed, _newline);
                if (remaining >= 0)
                {
                    end = WindowEndAfterRunes(end, remaining, out var taken);
                    remaining -= taken;
                }

                // The decoder's CR holdback makes a terminator at the window
                // edge complete. An unterminated edge needs another refill.
                if (end < bytes.Length || EndsWithTerminator() || remaining == 0)
                {
                    if (builder is null)
                    {
                        line = CopyWindowRange(_windowConsumed, end);
                        Advance(end);
                    }
                    else
                    {
                        AppendRange(builder, _windowConsumed, end);
                        line = ownsBuilder ? EmitAccumulated(builder) : PyString.Empty;
                    }
                    _services.CheckExecution(null);
                    return true;
                }

                builder ??= new GovernedByteBuilder(_services.MemoryGovernor, null);
                AppendRange(builder, _windowConsumed, bytes.Length);
                line = PyString.Empty;
                return false;
            }

            public void Release()
            {
                if (_released) return;
                _released = true;
                // A budget callback can request cleanup while allocation still
                // borrows our input. The active read owns final retirement.
                if (!_reading) ReleaseCoreAsync(failed: false, asynchronous: false).GetAwaiter().GetResult();
            }

            private void BeginRead()
            {
                _services.CheckExecution(null);
                EnsureNotReleased();
                if (_reading) throw RuntimeErrors.Runtime("reentrant text read", null);
                _reading = true;
            }

            private void EndRead(bool failed)
            {
                _reading = false;
                if (_released) ReleaseCoreAsync(failed, asynchronous: false).GetAwaiter().GetResult();
            }

            private ValueTask EndReadAsync(bool failed)
            {
                _reading = false;
                return _released ? ReleaseCoreAsync(failed, asynchronous: true) : default;
            }

            private void EnsureNotReleased()
            {
                if (_released) throw RuntimeErrors.Value("I/O operation on closed file", null);
            }

            private async ValueTask ReleaseCoreAsync(bool failed, bool asynchronous)
            {
                try
                {
                    if (_file is not null)
                    {
                        if (failed) await _file.CloseAfterFailureAsync(null, asynchronous).ConfigureAwait(false);
                        else await _file.CloseAsync(null, asynchronous).ConfigureAwait(false);
                    }
                }
                finally
                {
                    ReleaseWindow();
                    if (_raw is not null)
                    {
                        _services.MemoryGovernor.Release(_raw.Length);
                        _raw = null;
                    }
                }
            }

            public void Dispose() => Release();

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

                        var take = WindowEndAfterRunes(_window.Length, remaining, out var got);
                        if (take < _window.Length)
                        {
                            if (builder is null)
                            {
                                var piece = CopyWindowRange(_windowConsumed, take);
                                Advance(take);
                                _services.CheckExecution(null);
                                return piece;
                            }

                            AppendRange(builder, _windowConsumed, take);
                            return EmitAccumulated(builder);
                        }

                        // Reaching the window end may mean an exact fit or a shortfall;
                        // counting runes disambiguates without an extra pull.
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

                        var take = WindowEndAfterRunes(_window.Length, remaining, out var got);
                        if (take < _window.Length)
                        {
                            if (builder is null)
                            {
                                var piece = CopyWindowRange(_windowConsumed, take);
                                Advance(take);
                                _services.CheckExecution(null);
                                return piece;
                            }

                            AppendRange(builder, _windowConsumed, take);
                            return EmitAccumulated(builder);
                        }

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
                        var bytes = _window.Span;
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
                        var bytes = _window.Span;
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
                => _eof && _rawLength == 0 && _windowConsumed >= _window.Length;

            // A terminator ending exactly at the window edge is complete: the
            // carriage-return holdback reunites split CRLF pairs, so a trailing
            // newline can never await bytes from the next window. Without this,
            // boundary-aligned lines would swallow their terminator as content
            // and merge with the following line.
            private bool EndsWithTerminator()
            {
                var bytes = _window.Span;
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
                var bytes = _window.Span;
                Debug.Assert(byteEnd >= _windowConsumed && byteEnd <= bytes.Length, "chunked reader advanced outside its window");
                Debug.Assert(byteEnd == bytes.Length || (bytes[byteEnd] & 0xC0) != 0x80, "chunked reader crossed a rune boundary");
                _position += byteEnd - _windowConsumed;
                _windowConsumed = byteEnd;
            }

            private void AppendRange(GovernedByteBuilder builder, int byteStart, int byteEnd)
            {
                Debug.Assert(byteStart == _windowConsumed, "chunked reader appends out of order");
                // Admission and copying finish before this append changes the cursor.
                builder.Append(_window.Span.Slice(byteStart, byteEnd - byteStart));
                Advance(byteEnd);
            }

            private PyString CopyWindowRange(int start, int end)
                => start >= end ? PyString.Empty
                    : PyString.CopyFromUtf8(_window.Span[start..end], _services.MemoryGovernor, null);

            private int WindowEndAfterRunes(int limit, int maximum, out int count)
            {
                var bytes = _window.Span[..limit];
                var offset = _windowConsumed;
                count = 0;
                while (offset < bytes.Length && count < maximum)
                {
                    var scanLength = Math.Min(maximum - count, bytes.Length - offset);
                    var prefix = bytes.Slice(offset, scanLength).IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
                    if (prefix != 0)
                    {
                        var take = prefix < 0 ? scanLength : prefix;
                        count += take;
                        offset += take;
                        continue;
                    }
                    var lead = bytes[offset];
                    offset += lead < 0xe0 ? 2 : lead < 0xf0 ? 3 : 4;
                    count++;
                }
                return offset;
            }

            private void Refill()
            {
                EnsureNotReleased();
                while (_windowConsumed >= _window.Length)
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
                EnsureNotReleased();
                while (_windowConsumed >= _window.Length)
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
                    _decodingEncoding = ResolveUtf16ByteOrder(ref source, _encoding, true, _services.MemoryGovernor, _services, null);
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

            private bool Pull() => PullCoreAsync(false).GetAwaiter().GetResult();

            private ValueTask<bool> PullAsync() => PullCoreAsync(true);

            private async ValueTask<bool> PullCoreAsync(bool asynchronous)
            {
                EnsureNotReleased();
                if (_eof) return false;
                if (_gzip is not null) return await PullGzipAsync().ConfigureAwait(false);
                _services.CheckExecution(null);
                EnsureRaw();
                EnsureNotReleased();
                var count = await _file!.ReadAsync(_raw!.AsMemory(_rawLength, _windowBytes), null, asynchronous).ConfigureAwait(false);
                EnsureNotReleased();
                return AcceptCount(count);
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

            private bool AcceptGzipPayload(ReadOnlyMemory<byte> payload)
            {
                Debug.Assert(_raw is not null && _rawLength + _windowBytes <= _raw.Length, "chunked carry overflowed its window");
                if (payload.Length > _windowBytes) throw RuntimeErrors.Runtime("gzip text chunk exceeded its window", _gzipSpan);
                payload.Span.CopyTo(_raw!.AsSpan(_rawLength));
                return AcceptCount(payload.Length);
            }

            private bool AcceptCount(int count)
            {
                if (count == 0) { _eof = true; return false; }
                _rawLength += count;
                return true;
            }

            private void InstallWindow(int head)
            {
                ReleaseWindow();
                if (head == 0)
                {
                    _window = default;
                    return;
                }

                Debug.Assert(_raw is not null, "window installs from buffered bytes");
                var source = new ReadOnlySpan<byte>(_raw!, 0, head);
                if (_encoding is TextEncodingMode.Utf8 or TextEncodingMode.Utf8Bom
                    && IsUnchangedUtf8Text(source, _newline))
                {
                    _services.CheckExecution(null);
                    // The raw prefix cannot move or be refilled while this view
                    // is active. Returned values acquire their independent copy.
                    _window = _raw.AsMemory(0, head);
                }
                else
                {
                    var decoded = _encoding switch
                    {
                        TextEncodingMode.Ascii => DecodeAsciiText(source, _services, null, _errors, _newline),
                        TextEncodingMode.Windows1252 => DecodeWindows1252Text(source, _services, null, _errors, _newline),
                        TextEncodingMode.Latin1 => DecodeLatin1Text(source, _services, null, _newline),
                        _ when IsUtf16Encoding(_encoding) => DecodeUtf16Text(source, _decodingEncoding, _services, null, _errors, _newline),
                        _ => DecodeUtf8Text(_raw.AsMemory(0, head), _services.MemoryGovernor,
                            null, _errors, _newline, _services),
                    };
                    _window = decoded.Utf8Bytes;
                    _windowCharge = decoded.OwnerMemoryGovernor is null ? 0 : decoded.CommittedOwnedBytes;
                }
                _windowConsumed = 0;
                _rawWindowLength = head;
            }

            private void ReleaseWindow()
            {
                if (_windowCharge > 0)
                {
                    _services.MemoryGovernor.Release(_windowCharge);
                    _windowCharge = 0;
                }

                // Retire the decoded view before compacting its raw prefix.
                // The same rule applies to borrowed and transformed windows.
                _window = default;
                _windowConsumed = 0;
                if (_rawWindowLength > 0)
                {
                    Buffer.BlockCopy(_raw!, _rawWindowLength, _raw!, 0, _rawLength - _rawWindowLength);
                    _rawLength -= _rawWindowLength;
                    _rawWindowLength = 0;
                }
            }

        }
    }
}
