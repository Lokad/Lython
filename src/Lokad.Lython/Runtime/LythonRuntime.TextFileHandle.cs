using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIteratorValue
        {
            private TextFileHandle(string path, string mode, PyString text, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline) : this(path, mode, text, context, encoding, errors, newline, default) { }

            private TextFileHandle(
                string path,
                string mode,
                PyString text,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                BigInteger appendBasePosition)
            {
                Path = path;
                Mode = mode;
                _text = text;
                _context = context;
                _encoding = encoding;
                _errors = errors;
                _newline = newline;
                _appendBasePosition = appendBasePosition;
                _writeBuffer = mode == "r" ? null : new GovernedByteBuilder(context.MemoryGovernor);
            }

            private readonly PyString _text;
            private readonly GovernedByteBuilder? _writeBuffer;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly BigInteger _appendBasePosition;
            private int _readCursorByte;
            private BigInteger _publishedByteLength;
            private long _bufferedRuneLength;
            private bool _hasPublishedWrite;

            public string Path { get; }

            public string Mode { get; }

            public string EncodingName => _encoding switch
            {
                TextEncodingMode.Utf8Bom => "utf-8-sig",
                TextEncodingMode.Latin1 => "iso8859-1",
                _ => "utf-8"
            };

            public string ErrorsName => _errors switch
            {
                TextErrorMode.Ignore => "ignore",
                TextErrorMode.Replace => "replace",
                TextErrorMode.BackslashReplace => "backslashreplace",
                _ => "strict"
            };

            public bool IsClosed { get; private set; }

            public bool IsReadable()
            {
                EnsureOpen();
                return Mode == "r";
            }

            public bool IsWritable()
            {
                EnsureOpen();
                return Mode is "w" or "a";
            }

            public bool IsSeekable()
            {
                EnsureOpen();
                return false;
            }

            public BigInteger Tell()
            {
                EnsureOpen();
                return Mode switch
                {
                    "r" => new BigInteger(_readCursorByte),
                    "w" => _publishedByteLength + PendingEncodedTextLength(),
                    "a" => _appendBasePosition + _publishedByteLength + PendingEncodedTextLength(),
                    _ => BigInteger.Zero
                };
            }

            public object Flush()
            {
                EnsureOpen();
                FlushCore(null);
                return PyNone.Instance;
            }

            public async ValueTask<object> FlushAsync()
            {
                EnsureOpen();
                await FlushCoreAsync(null).ConfigureAwait(false);
                return PyNone.Instance;
            }

            public object Seek(LythonSourceSpan span)
                => throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython text handles.", span);

            public static TextFileHandle ForRead(string path, ExecutionContext context)
                => ForRead(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForRead(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForRead(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForRead(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForRead(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForRead(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                if (encoding == TextEncodingMode.Latin1)
                {
                    using var payload = ReadGovernedHostBytes(path, context, null);
                    var latin1Text = DecodeText(
                        payload.Memory,
                        encoding,
                        context,
                        null,
                        errors,
                        newline);
                    context.ObserveString(latin1Text, null);
                    return new TextFileHandle(path, "r", latin1Text, context, encoding, errors, newline);
                }

                var text = ReadGovernedHostText(path, context, null, errors, newline);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    text = StripUtf8Bom(text, encoding);
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding, errors, newline);
            }

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context)
                => ForReadAsync(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForReadAsync(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForReadAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static async ValueTask<TextFileHandle> ForReadAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                if (encoding == TextEncodingMode.Latin1)
                {
                    using var payload = await ReadGovernedHostBytesAsync(path, context, null).ConfigureAwait(false);
                    var latin1Text = DecodeText(
                        payload.Memory,
                        encoding,
                        context,
                        null,
                        errors,
                        newline);
                    context.ObserveString(latin1Text, null);
                    return new TextFileHandle(path, "r", latin1Text, context, encoding, errors, newline);
                }

                var text = await ReadGovernedHostTextAsync(path, context, null, errors, newline).ConfigureAwait(false);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    text = StripUtf8Bom(text, encoding);
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding, errors, newline);
            }

            public static TextFileHandle ForWrite(string path, ExecutionContext context)
                => ForWrite(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForWrite(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForWrite(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
                => new(path, "w", PyString.Empty, context, encoding, errors, newline);

            public static TextFileHandle ForAppend(string path, ExecutionContext context)
                => ForAppend(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForAppend(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForAppend(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForAppend(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var stat = context.HostStat(path, null);
                var appendBasePosition = stat is { Exists: true, IsFile: true }
                    ? stat.Size
                    : BigInteger.Zero;
                return new TextFileHandle(path, "a", PyString.Empty, context, encoding, errors, newline, appendBasePosition);
            }

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context)
                => ForAppendAsync(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForAppendAsync(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForAppendAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static async ValueTask<TextFileHandle> ForAppendAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var stat = await context.HostStatAsync(path, null).ConfigureAwait(false);
                var appendBasePosition = stat is { Exists: true, IsFile: true }
                    ? stat.Size
                    : BigInteger.Zero;
                return new TextFileHandle(path, "a", PyString.Empty, context, encoding, errors, newline, appendBasePosition);
            }

            public object Enter() => this;

            object IPyContextManager.Enter() => Enter();

            ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter());

            bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
            {
                _ = Exit();
                return false;
            }

            public object Exit()
            {
                if (IsClosed)
                {
                    return false;
                }

                FlushCore(null);
                IsClosed = true;
                _writeBuffer?.Release();
                return false;
            }

            public async ValueTask<object> ExitAsync()
            {
                if (IsClosed)
                {
                    return false;
                }

                await FlushCoreAsync(null).ConfigureAwait(false);
                IsClosed = true;
                _writeBuffer?.Release();
                return false;
            }

            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                _ = await ExitAsync().ConfigureAwait(false);
                return false;
            }

            public PyString Read()
                => Read(-1);

            public PyString Read(int size)
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                if (_readCursorByte >= _text.Utf8Bytes.Length)
                {
                    return PyString.Empty;
                }

                var end = size < 0
                    ? _text.Utf8Bytes.Length
                    : ByteOffsetAfterRunes(_readCursorByte, size);
                var result = _text.SliceByByteRange(_readCursorByte, end);
                _readCursorByte = end;
                return result;
            }

            public PyString ReadLine()
                => ReadLine(-1);

            public PyString ReadLine(int size)
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var source = _text.Utf8Bytes.Span;
                if (_readCursorByte >= source.Length)
                {
                    return PyString.Empty;
                }

                var end = FindLineEndByte(source, _readCursorByte);
                if (size >= 0)
                {
                    end = Math.Min(end, ByteOffsetAfterRunes(_readCursorByte, size));
                }

                var line = _text.SliceByByteRange(_readCursorByte, end);
                _readCursorByte = end;
                return line;
            }

            public PyList ReadLines()
                => ReadLines(-1);

            public PyList ReadLines(int hint)
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var items = new List<object>();
                var totalBytes = 0;
                while (true)
                {
                    var line = ReadLine();
                    if (line.Length == 0)
                    {
                        break;
                    }

                    items.Add(line);
                    totalBytes += line.Utf8Bytes.Length;
                    if (hint > 0 && totalBytes > hint)
                    {
                        break;
                    }
                }

                return new PyList(items, _context.MemoryGovernor, null);
            }

            public IEnumerable<object> Iterate()
            {
                while (TryMoveNext(out var value))
                {
                    yield return value;
                }
            }

            public bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                var line = ReadLine();
                if (line.Length == 0)
                {
                    value = PyNone.Instance;
                    return false;
                }

                value = line;
                return true;
            }

            public BigInteger Write(PyString text)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                var inputLength = text.Length;
                if (_encoding == TextEncodingMode.Latin1)
                {
                    var normalizedLength = GetLatin1NormalizedLength();
                    EnsureBufferedLength(normalizedLength);
                    AppendLatin1Normalized();
                    _bufferedRuneLength += normalizedLength;
                    return new BigInteger(inputLength);
                }

                EnsureBufferedLength(text.Length);
                _writeBuffer.RequireNotNull().Append(text);
                _bufferedRuneLength += text.Length;
                return new BigInteger(inputLength);

                long GetLatin1NormalizedLength()
                {
                    var length = 0L;
                    var position = 0;
                    var source = text.Utf8Bytes.Span;
                    for (var offset = 0; offset < source.Length; position++)
                    {
                        _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                        offset += consumed;
                        if (rune.Value <= byte.MaxValue)
                        {
                            length++;
                            continue;
                        }

                        length = _errors switch
                        {
                            TextErrorMode.Ignore => length,
                            TextErrorMode.Replace => checked(length + 1),
                            TextErrorMode.BackslashReplace => checked(length + (rune.Value <= 0xFFFF ? 6 : 10)),
                            _ => throw Latin1EncodeError(rune, position, null)
                        };
                    }

                    return length;
                }

                void EnsureBufferedLength(long additionalLength)
                {
                    var bufferedLength = checked(_bufferedRuneLength + additionalLength);
                    if (_context.Limits.MaxStringLength is { } maximumLength && bufferedLength > maximumLength)
                    {
                        throw RuntimeErrors.Runtime($"maximum string length exceeded ({maximumLength})", null);
                    }
                }

                void AppendLatin1Normalized()
                {
                    const string hex = "0123456789abcdef";
                    var buffer = _writeBuffer.RequireNotNull();
                    var source = text.Utf8Bytes.Span;
                    Span<byte> encoded = stackalloc byte[4];
                    for (var offset = 0; offset < source.Length;)
                    {
                        _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                        offset += consumed;
                        if (rune.Value <= 0x7F)
                        {
                            buffer.Append((byte)rune.Value);
                            continue;
                        }

                        if (rune.Value <= byte.MaxValue)
                        {
                            var encodedLength = rune.EncodeToUtf8(encoded);
                            buffer.Append(encoded[..encodedLength]);
                            continue;
                        }

                        if (_errors == TextErrorMode.Ignore)
                        {
                            continue;
                        }

                        if (_errors == TextErrorMode.Replace)
                        {
                            buffer.Append((byte)'?');
                            continue;
                        }

                        var digits = rune.Value <= 0xFFFF ? 4 : 8;
                        buffer.Append((byte)'\\');
                        buffer.Append(rune.Value <= 0xFFFF ? (byte)'u' : (byte)'U');
                        for (var shift = (digits - 1) * 4; shift >= 0; shift -= 4)
                        {
                            buffer.Append((byte)hex[(rune.Value >> shift) & 0xF]);
                        }
                    }
                }
            }

            public object WriteLines(object value, LythonSourceSpan span)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                foreach (var item in ToSequence(value, span))
                {
                    if (!PyStringOps.TryAsString(item, out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects an iterable of strings.", span);
                    }

                    _ = Write(text);
                }

                return PyNone.Instance;
            }

            private int ByteOffsetAfterRunes(int startByte, int runeCount)
                => _text.GetByteIndexAfterRunes(startByte, runeCount);

            private int FindLineEndByte(ReadOnlySpan<byte> source, int startByte)
            {
                for (var i = startByte; i < source.Length; i++)
                {
                    if (source[i] == (byte)'\n' &&
                        _newline is TextNewlineMode.TranslateUniversal or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed)
                    {
                        return i + 1;
                    }

                    if (source[i] != (byte)'\r')
                    {
                        continue;
                    }

                    if (_newline is TextNewlineMode.PreserveCarriageReturn)
                    {
                        return i + 1;
                    }

                    if (_newline is TextNewlineMode.PreserveCarriageReturnLineFeed)
                    {
                        if (i + 1 < source.Length && source[i + 1] == (byte)'\n')
                        {
                            return i + 2;
                        }

                        continue;
                    }

                    if (_newline == TextNewlineMode.PreserveUniversal)
                    {
                        return i + 1 < source.Length && source[i + 1] == (byte)'\n'
                            ? i + 2
                            : i + 1;
                    }
                }

                return source.Length;
            }

            private void FlushCore(LythonSourceSpan? span)
            {
                var pending = PrepareFlush(span);
                if (pending is null)
                {
                    return;
                }

                _context.RegisterHostCall(span);
                if (pending.Value.IsBinary)
                {
                    if (pending.Value.IsAppend)
                    {
                        _context.AppendHostBytes(Path, pending.Value.Payload, span);
                    }
                    else
                    {
                        _context.WriteHostBytes(Path, pending.Value.Payload, span);
                    }
                }
                else if (pending.Value.IsAppend)
                {
                    _context.AppendTextUtf8(Path, pending.Value.Payload, span);
                }
                else
                {
                    _context.WriteTextUtf8(Path, pending.Value.Payload, span);
                }

                CompleteFlush(pending.Value.Payload.Length);
            }

            private async ValueTask FlushCoreAsync(LythonSourceSpan? span)
            {
                var pending = PrepareFlush(span);
                if (pending is null)
                {
                    return;
                }

                _context.RegisterHostCall(span);
                if (pending.Value.IsBinary)
                {
                    if (pending.Value.IsAppend)
                    {
                        await _context.AppendHostBytesAsync(Path, pending.Value.Payload, span).ConfigureAwait(false);
                    }
                    else
                    {
                        await _context.WriteHostBytesAsync(Path, pending.Value.Payload, span).ConfigureAwait(false);
                    }
                }
                else if (pending.Value.IsAppend)
                {
                    await _context.AppendTextUtf8Async(Path, pending.Value.Payload, span).ConfigureAwait(false);
                }
                else
                {
                    await _context.WriteTextUtf8Async(Path, pending.Value.Payload, span).ConfigureAwait(false);
                }

                CompleteFlush(pending.Value.Payload.Length);
            }

            private PendingTextFlush? PrepareFlush(LythonSourceSpan? span)
            {
                if (Mode == "r")
                {
                    return null;
                }

                var buffer = _writeBuffer.RequireNotNull();
                if (buffer.Length == 0 && (Mode == "a" || _hasPublishedWrite))
                {
                    return null;
                }

                var isAppend = Mode == "a" || _hasPublishedWrite;
                var effectiveEncoding = isAppend && _encoding == TextEncodingMode.Utf8Bom
                    ? TextEncodingMode.Utf8
                    : _encoding;
                var text = buffer.Length == 0
                    ? PyString.Empty
                    : PyString.FromUtf8(buffer.WrittenMemory);
                var payload = EncodeText(text, effectiveEncoding, _errors, _newline, _context, span);
                return new PendingTextFlush(payload, isAppend, effectiveEncoding == TextEncodingMode.Latin1);
            }

            private BigInteger PendingEncodedTextLength()
            {
                var buffer = _writeBuffer.RequireNotNull();
                var length = _encoding == TextEncodingMode.Latin1
                    ? _bufferedRuneLength
                    : buffer.Length;
                if (_newline == TextNewlineMode.PreserveCarriageReturnLineFeed)
                {
                    foreach (var value in buffer.WrittenSpan)
                    {
                        if (value == (byte)'\n')
                        {
                            length++;
                        }
                    }
                }

                if (Mode == "w" && !_hasPublishedWrite && _encoding == TextEncodingMode.Utf8Bom)
                {
                    length += 3;
                }

                return new BigInteger(length);
            }

            private void CompleteFlush(int byteLength)
            {
                _publishedByteLength += byteLength;
                _hasPublishedWrite = true;
                _writeBuffer.RequireNotNull().Release();
                _bufferedRuneLength = 0;
            }

            private readonly record struct PendingTextFlush(byte[] Payload, bool IsAppend, bool IsBinary);

            private void EnsureOpen()
            {
                if (IsClosed)
                {
                    throw new LythonRuntimeException("ValueError", "I/O operation on closed file", null);
                }
            }
        }
    }
}
