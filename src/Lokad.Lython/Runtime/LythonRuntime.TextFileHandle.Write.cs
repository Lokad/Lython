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
            private readonly OpenedByteFile _file;
            private readonly ExecutionServices _services;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly GovernedByteBuilder _buffer;
            private BigInteger _publishedPosition;
            private long _bufferedRuneLength;
            private long _bufferedLineFeedCount;
            private long _bufferedUtf16ByteLength;
            private bool _hasWrittenText;
            private bool _bomWritten;

            public TextFileWriteState(
                OpenedByteFile file,
                TextFileMode mode,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
                : base(mode)
            {
                _file = file;
                _services = context.Services;
                _encoding = encoding;
                _errors = errors;
                _newline = newline;
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

                    if (_hasWrittenText && !_bomWritten && _encoding == TextEncodingMode.Utf8Bom)
                    {
                        length += 3;
                    }
                    if (_encoding == TextEncodingMode.Utf16 && _hasWrittenText && !_bomWritten)
                        length += 2;

                    return _publishedPosition + length;
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
                    var normalizedBufferedLength = checked(_bufferedRuneLength + normalizedLength);
                    AppendSingleByteNormalized();
                    _bufferedRuneLength = normalizedBufferedLength;
                    _bufferedLineFeedCount += appendedLineFeedCount;
                    return new BigInteger(inputLength);
                }

                var bufferedLength = checked(_bufferedRuneLength + text.Length);
                _buffer.Append(text);
                if (IsUtf16Encoding(_encoding))
                    _bufferedUtf16ByteLength += 2L * Encoding.UTF8.GetCharCount(text.Utf8Bytes.Span);
                _hasWrittenText = true;
                _bufferedRuneLength = bufferedLength;
                _bufferedLineFeedCount += appendedLineFeedCount;
                return new BigInteger(inputLength);

                long GetSingleByteNormalizedLength()
                {
                    var length = 0L;
                    var position = 0;
                    var source = text.Utf8Bytes.Span;
                    for (var offset = 0; offset < source.Length; position++)
                    {
                        if ((position & 1023) == 0) _services.CheckExecution(null);
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
                            _ => throw SingleByteEncodeError(position, _encoding, null, _services.MemoryGovernor, source, offset, text, _services.State.Guards)
                        };
                    }

                    return length;
                }

                void AppendSingleByteNormalized()
                {
                    const string hex = "0123456789abcdef";
                    var source = text.Utf8Bytes.Span;
                    Span<byte> encoded = stackalloc byte[4];
                    var position = 0;
                    for (var offset = 0; offset < source.Length;)
                    {
                        if ((position++ & 1023) == 0) _services.CheckExecution(null);
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

            internal void InitializePosition(long position)
            {
                _publishedPosition = position;
                _bomWritten = Mode == TextFileMode.Append && position > 0;
            }

            public async ValueTask FlushAsync(LythonSourceSpan? span, bool asynchronous)
            {
                using var pending = PrepareFlush(span);
                if (pending is { } output)
                {
                    await _file.WriteAsync(output.Payload, span, asynchronous).ConfigureAwait(false);
                    // Drop accepted staging before any later metadata/flush failure.
                    CompleteFlush();
                    _services.CheckExecution(span);
                    _publishedPosition = _file.Position(span);
                }
                await _file.FlushAsync(span, asynchronous).ConfigureAwait(false);
            }

            public void Release() => _buffer.Release();

            private PendingTextFlush? PrepareFlush(LythonSourceSpan? span)
            {
                var needsBom = _hasWrittenText && !_bomWritten && _encoding is TextEncodingMode.Utf16 or TextEncodingMode.Utf8Bom;
                if (_buffer.Length == 0 && !needsBom)
                {
                    return null;
                }

                var effectiveEncoding = _encoding == TextEncodingMode.Utf8Bom && _bomWritten ? TextEncodingMode.Utf8 : _encoding;
                if (_encoding == TextEncodingMode.Utf16 && _bomWritten) effectiveEncoding = TextEncodingMode.Utf16LittleEndian;
                if (effectiveEncoding == TextEncodingMode.Utf8 && _newline is TextNewlineMode.TranslateUniversal
                    or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed)
                    return new PendingTextFlush(_buffer.WrittenMemory, null);
                var reservation = _services.MemoryGovernor.ReserveTemporary(0, span);
                byte[] payload;
                try { payload = EncodeText(_buffer.WrittenSpan, effectiveEncoding, _errors, _newline, _services, span, outputStorage: reservation); }
                catch { reservation.Dispose(); throw; }
                return new PendingTextFlush(payload, reservation);
            }

            private void CompleteFlush()
            {
                if (_encoding is TextEncodingMode.Utf16 or TextEncodingMode.Utf8Bom && _hasWrittenText) _bomWritten = true;
                _buffer.Release();
                _bufferedRuneLength = 0;
                _bufferedLineFeedCount = 0;
                _bufferedUtf16ByteLength = 0;
            }

            private readonly record struct PendingTextFlush(ReadOnlyMemory<byte> Payload,
                MemoryGovernor.TemporaryMemoryReservation? Reservation) : IDisposable
            {
                public void Dispose() => Reservation?.Dispose();
            }

        }
    }
}
