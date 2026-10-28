using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Positions and capacity count Python characters, including non-BMP scalars.
    // Retain execution services, not the lexical scope that created the stream.
    internal sealed partial class StringIOObject : IPyIteratorValue, IPyContextManager,
        IPyDynamicAttributes, IPyTruthyValue, IPyRenderableValue
    {
        // Character backing and run capabilities retain no guest-value children.
        internal const long ShellBytes = 128;
        private readonly ExecutionServices _services;
        private readonly LythonSourceSpan _creationSpan;
        private readonly string? _newline;
        // Immutable input is borrowed until the first write, with one stream cursor.
        // The source retains its existing owner; materialization admits the full
        // writable backing before changing any state or relinquishing this alias.
        private PyString? _borrowedText;
        private int _borrowedByteOffset;
        private int _borrowedByteEnd;
        private int[] _buffer = [];
        private int _length;
        private long _position;
        private int _newlines;
        private bool _closed;

        internal StringIOObject(ExecutionServices services, LythonSourceSpan span, string? newline)
        {
            _services = services;
            _creationSpan = span;
            _newline = newline;
            services.MemoryGovernor.Reserve(ShellBytes, span);
            services.MemoryGovernor.Commit(ShellBytes);
        }

        internal static StringIOObject FromBorrowedText(PyString text, ExecutionServices services, LythonSourceSpan span)
        {
            var result = new StringIOObject(services, span, "\n") { _borrowedText = text, _borrowedByteEnd = text.Utf8Bytes.Length };
            for (var bytes = text.Utf8Bytes.Span; !bytes.IsEmpty;)
            {
                if ((result._length & 1023) == 0) services.CheckExecution(span);
                Rune.DecodeFromUtf8(bytes, out _, out var consumed);
                bytes = bytes[consumed..];
                result._length++;
            }
            return result;
        }

        internal long OwnedBytes => ShellBytes + (long)_buffer.Length * sizeof(int);
        internal long Position => _position;
        internal int Length => _length;
        public bool IsTruthy() => true;

        internal void EnsureOpen(LythonSourceSpan span)
        {
            if (_closed) throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
        }

        internal BigInteger Write(object value, LythonSourceSpan span)
        {
            if (value is not PyString text)
                throw new LythonRuntimeException("TypeError", "string argument expected, got '" + RuntimeErrors.OperandTypeName(value) + "'", span);
            EnsureOpen(span);
            long count = 0, translated = 0;
            var seen = 0;
            var previousCr = false;
            for (var bytes = text.Utf8Bytes.Span; !bytes.IsEmpty;)
            {
                Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
                bytes = bytes[consumed..];
                if ((count & 1023) == 0) _services.CheckExecution(span);
                count++;
                var scalar = rune.Value;
                if (_newline is null && previousCr && scalar == '\n') { previousCr = false; continue; }
                translated += _newline == "\r\n" && scalar == '\n' ? 2 : 1;
                previousCr = scalar == '\r';
            }
            // Newline history covers the input rather than translated storage.
            if (_newline is null or "") seen = ScanNewlines(text, span);
            if (translated == 0) return BigInteger.Zero;
            if (_position > int.MaxValue - translated)
                throw RuntimeErrors.Memory("StringIO buffer exceeds supported storage size", span);
            var end = (int)(_position + translated);
            _services.CheckExecution(span);
            EnsureCapacity(end, span);
            if (_position > _length) Array.Clear(_buffer, _length, (int)_position - _length);
            var cursor = (int)_position;
            previousCr = false;
            for (var bytes = text.Utf8Bytes.Span; !bytes.IsEmpty;)
            {
                Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
                bytes = bytes[consumed..];
                if ((cursor & 1023) == 0) _services.CheckExecution(span);
                var scalar = rune.Value;
                if (_newline is null && previousCr && scalar == '\n') { previousCr = false; continue; }
                previousCr = scalar == '\r';
                if (_newline is null && scalar == '\r') scalar = '\n';
                if (_newline == "\r\n" && scalar == '\n') _buffer[cursor++] = '\r';
                if (_newline == "\r" && scalar == '\n') scalar = '\r';
                _buffer[cursor++] = scalar;
            }
            _position = end;
            _length = Math.Max(_length, end);
            _newlines |= seen;
            return new BigInteger(count);
        }

        private int ScanNewlines(PyString text, LythonSourceSpan span)
        {
            var seen = 0;
            var cr = false;
            var index = 0;
            for (var bytes = text.Utf8Bytes.Span; !bytes.IsEmpty;)
            {
                Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
                bytes = bytes[consumed..];
                if ((index++ & 1023) == 0) _services.CheckExecution(span);
                if (cr) { seen |= rune.Value == '\n' ? 4 : 1; cr = false; if (rune.Value == '\n') continue; }
                if (rune.Value == '\r') cr = true;
                else if (rune.Value == '\n') seen |= 2;
            }
            return cr ? seen | 1 : seen;
        }

        private void EnsureCapacity(int required, LythonSourceSpan span)
        {
            if (required <= _buffer.Length && _borrowedText is null) return;
            required = Math.Max(required, _length);
            var capacity = (int)Math.Min(int.MaxValue, Math.Max((long)required, Math.Max(8L, (long)_buffer.Length * 2)));
            var bytes = (long)capacity * sizeof(int);
            var governor = _services.MemoryGovernor;
            governor.Reserve(bytes, span); // old and replacement backing overlap.
            int[] replacement;
            try
            {
                _services.CheckExecution(span);
                replacement = new int[capacity];
                if (_borrowedText is { } source)
                {
                    var index = 0;
                    for (var bytesLeft = source.Utf8Bytes.Span[.._borrowedByteEnd]; !bytesLeft.IsEmpty;)
                    {
                        if ((index & 1023) == 0) _services.CheckExecution(span);
                        Rune.DecodeFromUtf8(bytesLeft, out var rune, out var consumed);
                        replacement[index++] = rune.Value;
                        bytesLeft = bytesLeft[consumed..];
                    }
                }
                else for (var offset = 0; offset < _length; offset += Math.Min(1024, _length - offset))
                {
                    _services.CheckExecution(span);
                    Array.Copy(_buffer, offset, replacement, offset, Math.Min(1024, _length - offset));
                }
            }
            catch { governor.ReleaseReserved(bytes); throw; }
            var oldBytes = (long)_buffer.Length * sizeof(int);
            _buffer = replacement;
            _borrowedText = null;
            governor.Commit(bytes);
            governor.Release(oldBytes);
        }

        internal int ReadSymbol(LythonSourceSpan span)
        {
            EnsureOpen(span);
            _services.CheckExecution(span);
            if (_position >= _length) return -1;
            if (_borrowedText is not { } text) return _buffer[(int)_position++];
            Rune.DecodeFromUtf8(text.Utf8Bytes.Span[_borrowedByteOffset..], out var rune, out var consumed);
            _borrowedByteOffset += consumed;
            _position++;
            return rune.Value;
        }

        internal PyString Read(long size, bool line, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_position >= _length || size == 0) return PyString.Empty;
            if (_borrowedText is { } borrowed)
            {
                var startByte = _borrowedByteOffset;
                var endByte = size < 0 || size >= _length - _position ? _borrowedByteEnd
                    : BorrowedByteAfter(startByte, (int)size, span);
                if (line)
                {
                    for (var offset = startByte; offset < endByte;)
                    {
                        _services.CheckExecution(span);
                        var chunk = borrowed.Utf8Bytes.Span.Slice(offset, Math.Min(4096, endByte - offset));
                        var newline = chunk.IndexOf((byte)'\n');
                        if (newline >= 0) { endByte = offset + newline + 1; break; }
                        offset += chunk.Length;
                    }
                }
                var slice = CopyBorrowedRange(startByte, endByte, span);
                _position += slice.Length;
                _borrowedByteOffset = endByte;
                return slice;
            }
            var start = (int)_position;
            var end = size < 0 || size >= _length - start ? _length : start + (int)size;
            if (line)
            {
                for (var index = start; index < end; index++)
                {
                    if ((index & 1023) == 0) _services.CheckExecution(span);
                    var scalar = _buffer[index];
                    if (_newline is null or "")
                    {
                        if (scalar == '\r') { end = index + 1; if (end < _length && (size < 0 || end - start < size) && _buffer[end] == '\n') end++; break; }
                        if (scalar == '\n') { end = index + 1; break; }
                    }
                    else if (_newline == "\r\n")
                    {
                        if (scalar == '\r' && index + 1 < end && _buffer[index + 1] == '\n') { end = index + 2; break; }
                    }
                    else if (scalar == _newline[0]) { end = index + 1; break; }
                }
            }
            var result = RenderRange(start, end, span);
            _position = end; // failed materialization does not consume input.
            return result;
        }

        internal PyString GetValue(LythonSourceSpan span)
        {
            EnsureOpen(span);
            return RenderRange(0, _length, span);
        }

        private PyString RenderRange(int start, int end, LythonSourceSpan span)
        {
            if (_borrowedText is { } text)
            {
                var first = BorrowedByteAfter(0, start, span);
                return CopyBorrowedRange(first, BorrowedByteAfter(first, end - start, span), span);
            }
            var builder = new GovernedByteBuilder(_services.MemoryGovernor, span);
            try
            {
                Span<byte> encoded = stackalloc byte[4];
                for (var index = start; index < end; index++)
                {
                    if ((index & 1023) == 0) _services.CheckExecution(span);
                    var count = new Rune(_buffer[index]).EncodeToUtf8(encoded);
                    builder.Append(encoded[..count]);
                }
                var result = builder.ToPyStringAndRelease();
                return result;
            }
            finally { builder.Release(); }
        }

        private int BorrowedByteAfter(int start, int count, LythonSourceSpan span)
        {
            var bytes = _borrowedText!.Utf8Bytes.Span;
            for (var i = 0; i < count && start < _borrowedByteEnd; i++)
            {
                if ((i & 1023) == 0) _services.CheckExecution(span);
                Rune.DecodeFromUtf8(bytes[start..], out _, out var consumed);
                start += consumed;
            }
            return start;
        }

        private PyString CopyBorrowedRange(int start, int end, LythonSourceSpan span)
        {
            var text = _borrowedText!;
            if (start == 0 && end == text.Utf8Bytes.Length) return text;
            return PyString.CopyFromUtf8(text.Utf8Bytes.Span[start..end], _services.MemoryGovernor, span);
        }

        internal BigInteger Seek(long offset, long whence, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (whence is < 0 or > 2) throw new LythonRuntimeException("ValueError", "Invalid whence", span);
            if (whence != 0 && offset != 0) throw new LythonRuntimeException("OSError", "Can't do nonzero cur-relative seeks", span);
            if (whence == 0 && offset < 0) throw new LythonRuntimeException("ValueError", "Negative seek position", span);
            var position = whence switch { 0 => offset, 1 => _position, _ => _length };
            var byteOffset = _borrowedText is null ? 0 : BorrowedByteAfter(0, (int)Math.Min(position, _length), span);
            _position = position;
            _borrowedByteOffset = byteOffset;
            return new BigInteger(_position);
        }

        internal BigInteger Truncate(long size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (size < 0) throw new LythonRuntimeException("ValueError", "Negative size value", span);
            if (size < _length)
            {
                if (_borrowedText is { } text)
                {
                    _borrowedByteEnd = BorrowedByteAfter(0, (int)size, span);
                    _borrowedByteOffset = Math.Min(_borrowedByteOffset, _borrowedByteEnd);
                }
                else Array.Clear(_buffer, (int)size, _length - (int)size);
                _length = (int)size;
            }
            return new BigInteger(size);
        }

        internal object Close()
        {
            if (!_closed)
            {
                var bytes = (long)_buffer.Length * sizeof(int);
                _buffer = [];
                _borrowedText = null;
                _length = 0;
                _closed = true;
                _services.MemoryGovernor.Release(bytes);
            }
            return PyNone.Instance;
        }

        // Used only before fresh registration, when constructor validation or
        // initial storage allocation fails and no guest can retain this object.
        internal void ReleaseFailedConstruction() => _services.MemoryGovernor.Release(OwnedBytes);
        internal void RewindInitialValue() => _position = 0;
        public object Enter() { EnsureOpen(_creationSpan); return this; }
        public bool Exit(object exceptionType, object exceptionValue, object traceback) { Close(); return false; }
        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var line = Read(-1, true, _creationSpan);
            value = line;
            return line.Utf8Bytes.Length != 0;
        }
        public IEnumerable<object> Iterate()
        {
            EnsureOpen(_creationSpan);
            while (TryMoveNext(out var line)) yield return line;
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<_io.StringIO object>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
