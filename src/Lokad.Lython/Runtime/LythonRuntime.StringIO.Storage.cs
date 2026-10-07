using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Positions and capacity count Python characters, including non-BMP scalars.
    // Retain execution services, not the lexical scope that created the stream.
    internal sealed partial class StringIOObject : IPyIteratorValue, IPyContextManager,
        IPyDynamicAttributes, IPyTruthyValue, IPyRenderableValue, IPyOwnershipSnapshot
    {
        internal const long ShellBytes = 128;
        private readonly ExecutionServices _services;
        private readonly LythonSourceSpan _creationSpan;
        private readonly string? _newline;
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

        internal long OwnedBytes => ShellBytes + (long)_buffer.Length * sizeof(int);
        internal long Position => _position;
        internal int Length => _length;
        internal bool Closed => _closed;
        public bool TrySnapshotOwnership(out long chargeBytes) => OwnershipSnapshot.Owned(_services.MemoryGovernor, OwnedBytes, out chargeBytes);
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
                if ((count & 1023) == 0) _services.CheckExecutionBudget(span);
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
            _services.CheckExecutionBudget(span);
            EnsureCapacity(end, span);
            if (_position > _length) Array.Clear(_buffer, _length, (int)_position - _length);
            var cursor = (int)_position;
            previousCr = false;
            for (var bytes = text.Utf8Bytes.Span; !bytes.IsEmpty;)
            {
                Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
                bytes = bytes[consumed..];
                if ((cursor & 1023) == 0) _services.CheckExecutionBudget(span);
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
                if ((index++ & 1023) == 0) _services.CheckExecutionBudget(span);
                if (cr) { seen |= rune.Value == '\n' ? 4 : 1; cr = false; if (rune.Value == '\n') continue; }
                if (rune.Value == '\r') cr = true;
                else if (rune.Value == '\n') seen |= 2;
            }
            return cr ? seen | 1 : seen;
        }

        private void EnsureCapacity(int required, LythonSourceSpan span)
        {
            if (required <= _buffer.Length) return;
            var capacity = (int)Math.Min(int.MaxValue, Math.Max((long)required, Math.Max(8L, (long)_buffer.Length * 2)));
            var bytes = (long)capacity * sizeof(int);
            var governor = _services.MemoryGovernor;
            governor.Reserve(bytes, span); // old and replacement backing overlap.
            int[] replacement;
            try { replacement = new int[capacity]; }
            catch { governor.ReleaseReserved(bytes); throw; }
            Array.Copy(_buffer, replacement, _length);
            var oldBytes = (long)_buffer.Length * sizeof(int);
            _buffer = replacement;
            governor.Commit(bytes);
            governor.Release(oldBytes);
            ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
        }

        internal PyString Read(long size, bool line, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_position >= _length || size == 0) return PyString.Empty;
            var start = (int)_position;
            var end = size < 0 || size >= _length - start ? _length : start + (int)size;
            if (line)
            {
                for (var index = start; index < end; index++)
                {
                    if ((index & 1023) == 0) _services.CheckExecutionBudget(span);
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
            if (_services.Limits.MaxStringLength is { } maximum && end - start > maximum)
                throw RuntimeErrors.Runtime($"maximum string length exceeded ({maximum})", span);
            var builder = new GovernedByteBuilder(_services.MemoryGovernor, span);
            try
            {
                Span<byte> encoded = stackalloc byte[4];
                for (var index = start; index < end; index++)
                {
                    if ((index & 1023) == 0) _services.CheckExecutionBudget(span);
                    var count = new Rune(_buffer[index]).EncodeToUtf8(encoded);
                    builder.Append(encoded[..count]);
                }
                var result = builder.ToPyStringAndRelease();
                _services.State.CallTemporaries.TrackFreshString(result, span);
                return result;
            }
            finally { builder.Release(); }
        }

        internal BigInteger Seek(long offset, long whence, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (whence is < 0 or > 2) throw new LythonRuntimeException("ValueError", "Invalid whence", span);
            if (whence != 0 && offset != 0) throw new LythonRuntimeException("OSError", "Can't do nonzero cur-relative seeks", span);
            if (whence == 0 && offset < 0) throw new LythonRuntimeException("ValueError", "Negative seek position", span);
            _position = whence switch { 0 => offset, 1 => _position, _ => _length };
            return new BigInteger(_position);
        }

        internal BigInteger Truncate(long size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (size < 0) throw new LythonRuntimeException("ValueError", "Negative size value", span);
            if (size < _length)
            {
                Array.Clear(_buffer, (int)size, _length - (int)size);
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
                _length = 0;
                _closed = true;
                _services.MemoryGovernor.Release(bytes);
                ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
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
