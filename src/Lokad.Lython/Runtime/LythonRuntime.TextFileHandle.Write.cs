using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private sealed class TextFileWriteState : TextFileState
        {
            private readonly string _path;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly BigInteger _appendBasePosition;
            private readonly GovernedByteBuilder _buffer;
                private BigInteger _publishedByteLength;
                private long _bufferedRuneLength;
                private long _bufferedLineFeedCount;
                private bool _hasPublishedWrite;

            public TextFileWriteState(
                string path,
                    TextFileWriteMode mode,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                BigInteger appendBasePosition)
                    : base(mode == TextFileWriteMode.Write ? TextFileMode.Write : TextFileMode.Append)
                {
                _path = path;
                _context = context;
                _encoding = encoding;
                _errors = errors;
                _newline = newline;
                _appendBasePosition = appendBasePosition;
                _buffer = new GovernedByteBuilder(context.MemoryGovernor);
            }

            public BigInteger Position
            {
                get
                {
                    var length = _encoding == TextEncodingMode.Latin1
                        ? _bufferedRuneLength
                        : _buffer.Length;
                        if (_newline == TextNewlineMode.PreserveCarriageReturnLineFeed)
                        {
                            length += _bufferedLineFeedCount;
                    }

                    if (Mode == TextFileMode.Write && !_hasPublishedWrite && _encoding == TextEncodingMode.Utf8Bom)
                    {
                        length += 3;
                    }

                    var basePosition = Mode == TextFileMode.Append ? _appendBasePosition : BigInteger.Zero;
                    return basePosition + _publishedByteLength + length;
                }
            }

                public BigInteger Write(PyString text)
                {
                    var inputLength = text.Length;
                    var appendedLineFeedCount = 0;
                    if (_newline == TextNewlineMode.PreserveCarriageReturnLineFeed)
                    {
                        foreach (var value in text.Utf8Bytes.Span)
                        {
                            if (value == (byte)'\n')
                            {
                                appendedLineFeedCount++;
                            }
                        }
                    }

                if (_encoding == TextEncodingMode.Latin1)
                {
                    var normalizedLength = GetLatin1NormalizedLength();
                    EnsureBufferedLength(normalizedLength);
                        AppendLatin1Normalized();
                        _bufferedRuneLength += normalizedLength;
                        _bufferedLineFeedCount += appendedLineFeedCount;
                        return new BigInteger(inputLength);
                }

                EnsureBufferedLength(text.Length);
                    _buffer.Append(text);
                    _bufferedRuneLength += text.Length;
                    _bufferedLineFeedCount += appendedLineFeedCount;
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
                    var source = text.Utf8Bytes.Span;
                    Span<byte> encoded = stackalloc byte[4];
                    for (var offset = 0; offset < source.Length;)
                    {
                        _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                        offset += consumed;
                        if (rune.Value <= 0x7F)
                        {
                            _buffer.Append((byte)rune.Value);
                            continue;
                        }

                        if (rune.Value <= byte.MaxValue)
                        {
                            var encodedLength = rune.EncodeToUtf8(encoded);
                            _buffer.Append(encoded[..encodedLength]);
                            continue;
                        }

                        if (_errors == TextErrorMode.Ignore)
                        {
                            continue;
                        }

                        if (_errors == TextErrorMode.Replace)
                        {
                            _buffer.Append((byte)'?');
                            continue;
                        }

                        var digits = rune.Value <= 0xFFFF ? 4 : 8;
                        _buffer.Append((byte)'\\');
                        _buffer.Append(rune.Value <= 0xFFFF ? (byte)'u' : (byte)'U');
                        for (var shift = (digits - 1) * 4; shift >= 0; shift -= 4)
                        {
                            _buffer.Append((byte)hex[(rune.Value >> shift) & 0xF]);
                        }
                    }
                }
            }

            public void Flush(LythonSourceSpan? span)
            {
                var pending = PrepareFlush(span);
                if (pending is null)
                {
                    return;
                }

                _context.RegisterHostCall(span);
                switch (pending.Value.Operation)
                {
                    case PendingTextFlushOperation.WriteText:
                        _context.WriteTextUtf8(_path, pending.Value.Payload, span);
                        break;
                    case PendingTextFlushOperation.AppendText:
                        _context.AppendTextUtf8(_path, pending.Value.Payload, span);
                        break;
                    case PendingTextFlushOperation.WriteBytes:
                        _context.WriteHostBytes(_path, pending.Value.Payload, span);
                        break;
                    case PendingTextFlushOperation.AppendBytes:
                        _context.AppendHostBytes(_path, pending.Value.Payload, span);
                        break;
                }

                CompleteFlush(pending.Value.Payload.Length);
            }

            public async ValueTask FlushAsync(LythonSourceSpan? span)
            {
                var pending = PrepareFlush(span);
                if (pending is null)
                {
                    return;
                }

                _context.RegisterHostCall(span);
                switch (pending.Value.Operation)
                {
                    case PendingTextFlushOperation.WriteText:
                        await _context.WriteTextUtf8Async(_path, pending.Value.Payload, span).ConfigureAwait(false);
                        break;
                    case PendingTextFlushOperation.AppendText:
                        await _context.AppendTextUtf8Async(_path, pending.Value.Payload, span).ConfigureAwait(false);
                        break;
                    case PendingTextFlushOperation.WriteBytes:
                        await _context.WriteHostBytesAsync(_path, pending.Value.Payload, span).ConfigureAwait(false);
                        break;
                    case PendingTextFlushOperation.AppendBytes:
                        await _context.AppendHostBytesAsync(_path, pending.Value.Payload, span).ConfigureAwait(false);
                        break;
                }

                CompleteFlush(pending.Value.Payload.Length);
            }

            public void Release() => _buffer.Release();

            private PendingTextFlush? PrepareFlush(LythonSourceSpan? span)
            {
                if (_buffer.Length == 0 && (Mode == TextFileMode.Append || _hasPublishedWrite))
                {
                    return null;
                }

                // The first successful write-mode flush replaces the file; every
                // later flush appends only new buffered text. This also ensures a
                // UTF-8 BOM is emitted at most once. CompleteFlush is deliberately
                // called only after the host write succeeds, so failures are retryable.
                var isAppend = Mode == TextFileMode.Append || _hasPublishedWrite;
                var effectiveEncoding = isAppend && _encoding == TextEncodingMode.Utf8Bom
                    ? TextEncodingMode.Utf8
                    : _encoding;
                var text = _buffer.Length == 0
                    ? PyString.Empty
                    : PyString.FromUtf8(_buffer.WrittenMemory);
                var payload = EncodeText(text, effectiveEncoding, _errors, _newline, _context, span);
                var operation = (isAppend, effectiveEncoding == TextEncodingMode.Latin1) switch
                {
                    (false, false) => PendingTextFlushOperation.WriteText,
                    (true, false) => PendingTextFlushOperation.AppendText,
                    (false, true) => PendingTextFlushOperation.WriteBytes,
                    (true, true) => PendingTextFlushOperation.AppendBytes,
                };
                return new PendingTextFlush(payload, operation);
            }

            private void CompleteFlush(int byteLength)
            {
                _publishedByteLength += byteLength;
                _hasPublishedWrite = true;
                    _buffer.Release();
                    _bufferedRuneLength = 0;
                    _bufferedLineFeedCount = 0;
                }

            private readonly record struct PendingTextFlush(byte[] Payload, PendingTextFlushOperation Operation);

            private enum PendingTextFlushOperation
            {
                WriteText,
                AppendText,
                WriteBytes,
                AppendBytes,
            }
        }
    }
}
