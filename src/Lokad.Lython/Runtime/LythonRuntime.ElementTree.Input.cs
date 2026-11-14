using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ElementTreeModule
    {
        // This adapter owns only native working storage. Borrowed read results
        // retain their guest owner; an owned filename uses one fixed byte buffer.
        private sealed class XmlInputReader : TextReader
        {
            private const int ChunkBytes = 16 * 1024;
            private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
            private static readonly Encoding Utf16Little = new UnicodeEncoding(false, false, true);
            private static readonly Encoding Utf16Big = new UnicodeEncoding(true, false, true);
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly MemoryGovernor.TemporaryMemoryReservation _working;
            private readonly MemoryGovernor.TemporaryMemoryReservation _parser;
            private readonly GovernedByteBuilder _prefix;
            private readonly OpenedByteFile? _file;
            private object? _source;
            private byte[] _buffer;
            private ReadOnlyMemory<byte> _current;
            private Decoder? _decoder;
            private TextEncodingMode _encoding;
            private bool _first = true, _textInput, _prepared, _eof, _asynchronous;
            private int _searchPosition;
            private Utf8XmlTextReader? _legacyReader;
            private PyString? _legacyText;

            internal XmlInputReader(ExecutionContext context, LythonSourceSpan span, OpenedByteFile? file, object? source)
            {
                _context = context; _span = span; _file = file; _source = source;
                _working = context.MemoryGovernor.ReserveTemporary(512L + (file is null ? 0 : 32L + ChunkBytes), span);
                try
                {
                    _parser = context.MemoryGovernor.ReserveTemporary(16_384L + 192L * (MaximumDepth + 1), span);
                    _buffer = file is null ? [] : new byte[ChunkBytes];
                    _prefix = new(context.MemoryGovernor, span);
                }
                catch { _parser?.Dispose(); _working.Dispose(); throw; }
            }

            internal void SetExecutionMode(bool asynchronous) => _asynchronous = asynchronous;

            public override int Read(char[] buffer, int index, int count)
                => ReadCoreAsync(buffer.AsMemory(index, count), false).GetAwaiter().GetResult();

            public override Task<int> ReadAsync(char[] buffer, int index, int count)
                => ReadCoreAsync(buffer.AsMemory(index, count), _asynchronous).AsTask();

            public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ReadCoreAsync(buffer, _asynchronous);
            }

            private async ValueTask<int> ReadCoreAsync(Memory<char> destination, bool asynchronous)
            {
                if (destination.IsEmpty) return 0;
                if (!_prepared) await PrepareAsync(asynchronous).ConfigureAwait(false);
                while (true)
                {
                    _context.CheckExecution(_span);
                    if (_encoding == TextEncodingMode.Windows1252)
                    {
                        if (_legacyReader is not null)
                        {
                            var count = _legacyReader.Read(destination.Span);
                            if (count != 0) return count;
                            ReleaseLegacy();
                        }
                        if (_current.IsEmpty)
                        {
                            if (_eof) return 0;
                            await PullAsync(asynchronous).ConfigureAwait(false);
                            continue;
                        }
                        _legacyText = DecodeText(_current, _encoding, _context, _span,
                            TextErrorMode.Strict, TextNewlineMode.PreserveLineFeed);
                        _current = default;
                        _legacyReader = new(_legacyText, _context.State, _span);
                        continue;
                    }
                    if (_current.IsEmpty && !_eof)
                    {
                        await PullAsync(asynchronous).ConfigureAwait(false);
                        continue;
                    }
                    _decoder!.Convert(_current.Span, destination.Span, _eof,
                        out var consumed, out var written, out var completed);
                    _current = _current[consumed..];
                    if (written != 0) return written;
                    if (_eof && completed) return 0;
                }
            }

            private async ValueTask PullAsync(bool asynchronous)
            {
                _current = default;
                if (_prepared) _prefix.Release();
                // XmlReader can retain names and token capacity. Keep this
                // conservative bound until that reader has actually retired.
                _parser.Grow(24L * ChunkBytes, _span);
                _context.CheckExecution(_span);
                if (_file is not null)
                {
                    var count = await _file.ReadAsync(_buffer, _span, asynchronous).ConfigureAwait(false);
                    _current = _buffer.AsMemory(0, count);
                    _eof = count == 0;
                }
                else
                {
                    var read = asynchronous
                        ? await PyTextStream.ResolveMemberAsync(_source!, "read", _span, _context).ConfigureAwait(false)
                        : PyTextStream.ResolveMember(_source!, "read", _span, _context);
                    var value = asynchronous
                        ? await InvokeCallableTargetAsync(read, _span, _span, _context,
                            () => ValueTask.FromResult<CallArgumentValue[]>([CallArgumentValue.Positional((BigInteger)ChunkBytes)])).ConfigureAwait(false)
                        : InvokeCallableTarget(read, _span, _span, _context, () => [CallArgumentValue.Positional((BigInteger)ChunkBytes)]);
                    if (value is PyString text)
                    {
                        if (_first) _textInput = true;
                        _current = text.Utf8Bytes;
                        _eof = text.ByteLength == 0;
                    }
                    else if (value is PyBytes bytes)
                    {
                        _current = bytes.Memory;
                        _eof = bytes.Length == 0;
                    }
                    else if (asynchronous ? !await IsTruthyAsync(value, _context, _span).ConfigureAwait(false) : !IsTruthy(value, _context, _span))
                        _eof = true;
                    else throw RuntimeErrors.Type("XML read() must return bytes or str.", _span);
                }
                _first = false;
                var difference = _current.Length - ChunkBytes;
                if (difference > 0) _parser.Grow(24L * difference, _span);
                else _parser.Shrink(-24L * difference);
                _context.CheckExecution(_span);
            }

            private async ValueTask PrepareAsync(bool asynchronous)
            {
                await PullAsync(asynchronous).ConfigureAwait(false);
                if (_textInput)
                {
                    _encoding = TextEncodingMode.Utf8;
                    _decoder = StrictUtf8.GetDecoder();
                    _prepared = true;
                    return;
                }
                while (true)
                {
                    _prefix.Append(_current.Span);
                    _current = default;
                    if (ProbeEncoding(out var skip))
                    {
                        _current = _prefix.WrittenMemory[skip..];
                        _decoder = _encoding switch
                        {
                            TextEncodingMode.Utf16LittleEndian => Utf16Little.GetDecoder(),
                            TextEncodingMode.Utf16BigEndian => Utf16Big.GetDecoder(),
                            TextEncodingMode.Latin1 => Encoding.Latin1.GetDecoder(),
                            TextEncodingMode.Ascii => Encoding.GetEncoding(20127, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetDecoder(),
                            TextEncodingMode.Windows1252 => null,
                            _ => StrictUtf8.GetDecoder(),
                        };
                        _prepared = true;
                        return;
                    }
                    await PullAsync(asynchronous).ConfigureAwait(false);
                }
            }

            private bool ProbeEncoding(out int skip)
            {
                var bytes = _prefix.WrittenSpan;
                skip = 0;
                if (bytes.Length < 4 && !_eof) return false;
                if (bytes.StartsWith(new byte[] { 0xff, 0xfe, 0, 0 }) || bytes.StartsWith(new byte[] { 0, 0, 0xfe, 0xff }) ||
                    bytes.StartsWith(new byte[] { 0, 0, 0, 0x3c }) || bytes.StartsWith(new byte[] { 0x3c, 0, 0, 0 }))
                    throw new LythonRuntimeException("NotImplementedError", "XML UTF-32 input is not supported.", _span);
                if (bytes.StartsWith(new byte[] { 0xff, 0xfe })) { _encoding = TextEncodingMode.Utf16LittleEndian; skip = 2; }
                else if (bytes.StartsWith(new byte[] { 0xfe, 0xff })) { _encoding = TextEncodingMode.Utf16BigEndian; skip = 2; }
                else if (bytes.Length >= 4 && bytes[0] == '<' && bytes[1] == 0) _encoding = TextEncodingMode.Utf16LittleEndian;
                else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == '<') _encoding = TextEncodingMode.Utf16BigEndian;
                else
                {
                    _encoding = TextEncodingMode.Utf8;
                    if (bytes.StartsWith("\uFEFF"u8)) skip = 3;
                }
                var utf16 = IsUtf16Encoding(_encoding);
                ReadOnlySpan<byte> opening = _encoding == TextEncodingMode.Utf16LittleEndian ? "<\0?\0x\0m\0l\0"u8
                    : _encoding == TextEncodingMode.Utf16BigEndian ? "\0<\0?\0x\0m\0l"u8 : "<?xml"u8;
                var body = bytes[skip..];
                if (body.Length < opening.Length && !_eof && opening.StartsWith(body)) return false;
                if (!body.StartsWith(opening)) return true;
                ReadOnlySpan<byte> ending = _encoding == TextEncodingMode.Utf16LittleEndian ? "?\0>\0"u8
                    : _encoding == TextEncodingMode.Utf16BigEndian ? "\0?\0>"u8 : "?>"u8;
                for (var position = Math.Max(skip, _searchPosition); position < bytes.Length;)
                {
                    _context.CheckExecution(_span);
                    var size = Math.Min(4096 + ending.Length - 1, bytes.Length - position);
                    var found = bytes.Slice(position, size).IndexOf(ending);
                    if (found >= 0 && (!utf16 || ((position + found - skip) & 1) == 0))
                    {
                        var header = bytes.Slice(skip, position + found + ending.Length - skip);
                        var declaration = utf16 ? (_encoding == TextEncodingMode.Utf16LittleEndian ? Utf16Little : Utf16Big).GetString(header)
                            : Encoding.ASCII.GetString(header);
                        if (DeclaredEncoding(declaration) is { } name)
                        {
                            var declared = DeclarationEncoding(name, _span);
                            if (utf16 && declared != TextEncodingMode.Utf16)
                                throw ParseError("encoding specified in XML declaration is incorrect", _span);
                            if (!utf16 && declared == TextEncodingMode.Utf16)
                                throw ParseError("encoding specified in XML declaration is incorrect", _span);
                            if (!utf16) _encoding = declared;
                        }
                        _context.CheckExecution(_span);
                        return true;
                    }
                    if (found >= 0) { position += found + 1; continue; }
                    position += Math.Max(1, size - ending.Length + 1);
                    _searchPosition = position;
                }
                _searchPosition = Math.Max(skip, bytes.Length - ending.Length + 1);
                return _eof;
            }

            private void ReleaseLegacy()
            {
                _legacyReader?.Dispose(); _legacyReader = null;
                var value = _legacyText; _legacyText = null;
                if (value is not null) _context.MemoryGovernor.RefundUnpublishedValue(value);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _current = default; _source = null; _buffer = []; _decoder = null;
                    ReleaseLegacy();
                    _prefix.Release();
                    _parser.Dispose();
                    _working.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
