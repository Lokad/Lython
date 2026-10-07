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
            private long _bufferedUtf16ByteLength;
            private bool _hasPublishedWrite;
            private bool _hasWrittenText;
            private bool _utf16BomWritten;

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
                _utf16BomWritten = mode == TextFileWriteMode.Append && appendBasePosition > BigInteger.Zero;
                _buffer = new GovernedByteBuilder(context.MemoryGovernor);
            }

            public BigInteger Position
            {
                get
                {
                    var length = IsUtf16Encoding(_encoding) ? _bufferedUtf16ByteLength : IsSingleByteEncoding(_encoding)
                        ? _bufferedRuneLength
                        : _buffer.Length;
                    if (_newline == TextNewlineMode.PreserveCarriageReturnLineFeed)
                    {
                        length += _bufferedLineFeedCount * (IsUtf16Encoding(_encoding) ? 2 : 1);
                    }

                    if (Mode == TextFileMode.Write && !_hasPublishedWrite && _encoding == TextEncodingMode.Utf8Bom)
                    {
                        length += 3;
                    }
                    if (_encoding == TextEncodingMode.Utf16 && _hasWrittenText && !_utf16BomWritten)
                        length += 2;

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

                if (IsSingleByteEncoding(_encoding))
                {
                    var normalizedLength = GetSingleByteNormalizedLength();
                    EnsureBufferedLength(normalizedLength);
                    AppendSingleByteNormalized();
                    _bufferedRuneLength += normalizedLength;
                    _bufferedLineFeedCount += appendedLineFeedCount;
                    return new BigInteger(inputLength);
                }

                EnsureBufferedLength(text.Length);
                _buffer.Append(text);
                if (IsUtf16Encoding(_encoding))
                    _bufferedUtf16ByteLength += 2L * Encoding.UTF8.GetCharCount(text.Utf8Bytes.Span);
                _hasWrittenText = true;
                _bufferedRuneLength += text.Length;
                _bufferedLineFeedCount += appendedLineFeedCount;
                return new BigInteger(inputLength);

                long GetSingleByteNormalizedLength()
                {
                    var length = 0L;
                    var position = 0;
                    var source = text.Utf8Bytes.Span;
                    for (var offset = 0; offset < source.Length; position++)
                    {
                        if ((position & 1023) == 0) _context.CheckExecutionBudget(null);
                        _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                        offset += consumed;
                        if (TryEncodeSingleByteScalar(rune.Value, _encoding, out _))
                        {
                            length++;
                            continue;
                        }

                        length = _errors switch
                        {
                            TextErrorMode.Ignore => length,
                            TextErrorMode.Replace => checked(length + 1),
                            TextErrorMode.BackslashReplace => checked(length + BackslashEscapedRuneLength(rune)),
                            _ => throw SingleByteEncodeError(rune, position, _encoding, null)
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

                void AppendSingleByteNormalized()
                {
                    const string hex = "0123456789abcdef";
                    var source = text.Utf8Bytes.Span;
                    Span<byte> encoded = stackalloc byte[4];
                    var position = 0;
                    for (var offset = 0; offset < source.Length;)
                    {
                        if ((position++ & 1023) == 0) _context.CheckExecutionBudget(null);
                        _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                        offset += consumed;
                        if (rune.Value <= 0x7F)
                        {
                            _buffer.Append((byte)rune.Value);
                            continue;
                        }

                        if (TryEncodeSingleByteScalar(rune.Value, _encoding, out _))
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

                        var digits = rune.Value <= 0xff ? 2 : rune.Value <= 0xFFFF ? 4 : 8;
                        _buffer.Append((byte)'\\');
                        _buffer.Append(rune.Value <= 0xff ? (byte)'x' : rune.Value <= 0xFFFF ? (byte)'u' : (byte)'U');
                        for (var shift = (digits - 1) * 4; shift >= 0; shift -= 4)
                        {
                            _buffer.Append((byte)hex[(rune.Value >> shift) & 0xF]);
                        }
                    }
                }
            }

            public void Flush(LythonSourceSpan? span)
            {
                using var pending = PrepareFlush(span);
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
                using var pending = PrepareFlush(span);
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
                var needsUtf16Bom = _encoding == TextEncodingMode.Utf16 && _hasWrittenText && !_utf16BomWritten;
                if (_buffer.Length == 0 && _hasPublishedWrite && !needsUtf16Bom)
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
                if (_encoding == TextEncodingMode.Utf16 && !needsUtf16Bom)
                    effectiveEncoding = TextEncodingMode.Utf16LittleEndian;
                var text = _buffer.Length == 0
                    ? PyString.Empty
                    : PyString.FromUtf8(_buffer.WrittenMemory);
                var reservation = ReserveUtf16Output(text, effectiveEncoding, _newline, _context, span);
                byte[] payload;
                try { payload = EncodeText(text, effectiveEncoding, _errors, _newline, _context, span, reservation is not null); }
                catch { reservation?.Dispose(); throw; }
                var operation = (isAppend, UsesBinaryTextTransport(effectiveEncoding)) switch
                {
                    (false, false) => PendingTextFlushOperation.WriteText,
                    (true, false) => PendingTextFlushOperation.AppendText,
                    (false, true) => PendingTextFlushOperation.WriteBytes,
                    (true, true) => PendingTextFlushOperation.AppendBytes,
                };
                return new PendingTextFlush(payload, operation, reservation);
            }

            private void CompleteFlush(int byteLength)
            {
                _publishedByteLength += byteLength;
                _hasPublishedWrite = true;
                if (_encoding == TextEncodingMode.Utf16 && _hasWrittenText) _utf16BomWritten = true;
                _buffer.Release();
                _bufferedRuneLength = 0;
                _bufferedLineFeedCount = 0;
                _bufferedUtf16ByteLength = 0;
            }

            private readonly record struct PendingTextFlush(byte[] Payload, PendingTextFlushOperation Operation,
                MemoryGovernor.TemporaryMemoryReservation? Reservation) : IDisposable
            {
                public void Dispose() => Reservation?.Dispose();
            }

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
