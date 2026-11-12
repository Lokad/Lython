using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private sealed class TextFileWriteState : TextFileState
        {
            private readonly OpenedByteFile _file;
            private readonly ExecutionState _services;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly GovernedByteBuilder _buffer;
            // Storage/transport batches, not a limit on a write or file size.
            private const int BufferBatchBytes = 64 * 1024;
            private const int SourceChunkBytes = 4096;
            private BigInteger _publishedPosition;
            private bool _hasWrittenText;
            private bool _bomWritten;
            private static readonly byte[] Utf8Preamble = [0xef, 0xbb, 0xbf];
            private static readonly byte[] Utf16Preamble = [0xff, 0xfe];

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
                _services = context.State;
                _encoding = encoding;
                _errors = errors;
                _newline = newline;
                _buffer = new GovernedByteBuilder(context.MemoryGovernor);
            }

            private ReadOnlyMemory<byte> PendingPreamble => !_hasWrittenText || _bomWritten ? default
                : _encoding == TextEncodingMode.Utf8Bom ? Utf8Preamble
                : _encoding == TextEncodingMode.Utf16 ? Utf16Preamble : default;

            public BigInteger Position => _publishedPosition + _buffer.Length + PendingPreamble.Length;

            public ValueTask<BigInteger> WriteAsync(PyString text, bool asynchronous)
            {
                if (TryStage(text.Utf8Span, text, out var count)) return new(new BigInteger(count));
                return WriteBatchesAsync(text.Utf8Bytes, count, asynchronous);
            }

            internal ValueTask<BigInteger> WriteAsync(ReadOnlyMemory<byte> utf8, bool asynchronous)
                => TryStage(utf8.Span, null, out var count) ? new(new BigInteger(count))
                    : WriteBatchesAsync(utf8, count, asynchronous);

            private bool TryStage(ReadOnlySpan<byte> source, PyString? original, out int inputLength)
            {
                _services.CheckExecution(null);
                inputLength = original?.GetCharacterCount(_services.CancellationToken, null) ?? CountScalars(source);
                var encoding = EffectiveEncoding;
                // Validate the whole write before publishing any buffered prefix,
                // including an encoding failure near the end of a large input.
                var byteCount = EncodedTextByteCount(source, encoding, _errors, _newline, _services, null, original);
                if (byteCount > BufferBatchBytes - _buffer.Length) return false;
                Stage(source, (int)byteCount);
                _hasWrittenText = true;
                return true;
            }

            private void Stage(ReadOnlySpan<byte> source, int byteCount)
            {
                var destination = _buffer.GetWriteWindow(byteCount).Span;
                _services.CheckExecution(null);
                WriteEncodedText(source, destination, EffectiveEncoding, _errors, _newline, _services, null);
                _buffer.Advance(byteCount);
            }

            private async ValueTask<BigInteger> WriteBatchesAsync(ReadOnlyMemory<byte> source, int inputLength, bool asynchronous)
            {
                // Admit all working capacity before this write's first I/O.
                // Subsequent chunks reuse this funded backing; there is no
                // cumulative allocation count or late core-buffer admission.
                _ = _buffer.GetWriteWindow(BufferBatchBytes - _buffer.Length);
                _services.CheckExecution(null);
                _hasWrittenText = true;
                var published = false;
                try
                {
                    for (var offset = 0; offset < source.Length;)
                    {
                        _services.CheckExecution(null);
                        var end = offset + Math.Min(SourceChunkBytes, source.Length - offset);
                        while (end < source.Length && (source.Span[end] & 0xc0) == 0x80) end--;
                        var count = (int)EncodedTextByteCount(source.Span[offset..end], EffectiveEncoding,
                            _errors, _newline, _services, null);
                        if (count > BufferBatchBytes - _buffer.Length)
                        {
                            await PublishBufferedAsync(null, asynchronous).ConfigureAwait(false);
                            published = true;
                        }
                        Stage(source.Span[offset..end], count);
                        offset = end;
                    }
                }
                catch
                {
                    // Earlier completed batches are part of this same write.
                    if (published) await _file.CloseAfterFailureAsync(null, asynchronous).ConfigureAwait(false);
                    throw;
                }
                return new BigInteger(inputLength);
            }

            // BOM framing remains a flush concern, independently of capacity
            // admission/reentry while a write stages its final payload bytes.
            private TextEncodingMode EffectiveEncoding => _encoding switch
            {
                TextEncodingMode.Utf8Bom => TextEncodingMode.Utf8,
                TextEncodingMode.Utf16 => TextEncodingMode.Utf16LittleEndian,
                _ => _encoding,
            };

            private int CountScalars(ReadOnlySpan<byte> source)
            {
                var count = 0;
                for (var index = 0; index < source.Length; index++)
                {
                    if ((index & 1023) == 0) _services.CheckExecution(null);
                    if ((source[index] & 0xc0) != 0x80) count++;
                }
                return count;
            }

            internal void InitializePosition(long position)
            {
                _publishedPosition = position;
                _bomWritten = Mode == TextFileMode.Append && position > 0;
            }

            public async ValueTask FlushAsync(LythonSourceSpan? span, bool asynchronous)
            {
                await PublishBufferedAsync(span, asynchronous).ConfigureAwait(false);
                _buffer.Release();
                await _file.FlushAsync(span, asynchronous).ConfigureAwait(false);
            }

            private async ValueTask PublishBufferedAsync(LythonSourceSpan? span, bool asynchronous)
            {
                var publishedPreamble = false;
                try
                {
                    if (PendingPreamble is { IsEmpty: false } preamble)
                    {
                        await _file.WriteAsync(preamble, span, asynchronous).ConfigureAwait(false);
                        publishedPreamble = true;
                        _bomWritten = true;
                        _services.CheckExecution(span);
                        _publishedPosition = _file.Position(span);
                    }
                    if (_buffer.Length != 0)
                    {
                        // Final bytes stay funded through the real host borrow.
                        await _file.WriteAsync(_buffer.WrittenMemory, span, asynchronous).ConfigureAwait(false);
                        _buffer.Clear();
                        _services.CheckExecution(span);
                        _publishedPosition = _file.Position(span);
                    }
                }
                catch
                {
                    // The preamble and payload form one logical publication.
                    // A later zero-byte failure still follows an accepted prefix;
                    // retire the handle instead of allowing that flush to replay.
                    if (publishedPreamble) await _file.CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                    throw;
                }
            }

            public void Release() => _buffer.Release();
        }
    }
}
