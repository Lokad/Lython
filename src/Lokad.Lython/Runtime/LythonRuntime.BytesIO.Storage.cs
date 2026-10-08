using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class BytesIOObject : IPyIteratorValue, IPyContextManager,
        IPyDynamicAttributes, IPyTruthyValue, IPyRenderableValue, IPyOwnershipSnapshot
    {
        internal const long ShellBytes = 128;
        private readonly ExecutionServices _services;
        private readonly LythonSourceSpan _creationSpan;
        private byte[] _buffer = [];
        private int _length;
        private long _position;
        private bool _closed;

        internal BytesIOObject(ExecutionServices services, LythonSourceSpan span)
        {
            _services = services;
            _creationSpan = span;
            services.MemoryGovernor.Reserve(ShellBytes, span);
            services.MemoryGovernor.Commit(ShellBytes);
        }

        internal long OwnedBytes => ShellBytes + _buffer.Length;
        internal long Position => _position;
        internal int Length => _length;
        public bool TrySnapshotOwnership(out long chargeBytes) => OwnershipSnapshot.Owned(_services.MemoryGovernor, OwnedBytes, out chargeBytes);
        public bool IsTruthy() => true;

        internal void EnsureOpen(LythonSourceSpan span)
        {
            if (_closed) throw new LythonRuntimeException("ValueError", "I/O operation on closed file.", span);
        }

        internal BigInteger Write(object value, LythonSourceSpan span)
        {
            // CPython validates the argument before checking the closed state.
            if (value is not PyBytes bytes)
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + RuntimeErrors.OperandTypeName(value) + "'", span);
            EnsureOpen(span);
            if (bytes.Length == 0) return BigInteger.Zero;
            if (_position > Array.MaxLength - bytes.Length)
                throw RuntimeErrors.Memory("BytesIO buffer exceeds supported storage size", span);
            var end = (int)(_position + bytes.Length);
            _services.CheckExecution(span);
            EnsureCapacity(end, span);
            if (_position > _length) Array.Clear(_buffer, _length, (int)_position - _length);
            bytes.Bytes.CopyTo(_buffer.AsSpan((int)_position));
            _position = end;
            _length = Math.Max(_length, end);
            return new BigInteger(bytes.Length);
        }

        private void EnsureCapacity(int required, LythonSourceSpan span)
        {
            if (required <= _buffer.Length) return;
            var capacity = (int)Math.Min(Array.MaxLength, Math.Max((long)required, Math.Max(8L, (long)_buffer.Length * 2)));
            var governor = _services.MemoryGovernor;
            governor.Reserve(capacity, span);
            byte[] replacement;
            try { replacement = new byte[capacity]; }
            catch { governor.ReleaseReserved(capacity); throw; }
            Array.Copy(_buffer, replacement, _length);
            var oldCapacity = _buffer.Length;
            _buffer = replacement;
            governor.Commit(capacity);
            governor.Release(oldCapacity);
            ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
        }

        internal PyBytes Read(long size, bool line, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_position >= _length || size == 0) return CreateBytes([], _services, span);
            var start = (int)_position;
            var end = size < 0 || size >= _length - start ? _length : start + (int)size;
            if (line)
            {
                for (var index = start; index < end; index++)
                {
                    if ((index & 1023) == 0) _services.CheckExecution(span);
                    if (_buffer[index] == (byte)'\n') { end = index + 1; break; }
                }
            }
            var result = CopyRange(start, end, span);
            _position = end;
            return result;
        }

        internal PyBytes GetValue(LythonSourceSpan span)
        {
            EnsureOpen(span);
            return CopyRange(0, _length, span);
        }

        private PyBytes CopyRange(int start, int end, LythonSourceSpan span)
        {
            var length = end - start;
            if (length == 0) return CreateBytes([], _services, span);
            // The factory adopts this exact array. Fund the final owner before
            // allocating the copy; registry denial refunds fresh ownership.
            _services.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(length), span);
            return CreateBytes(_buffer.AsSpan(start, length).ToArray(), _services, span);
        }

        internal BigInteger Seek(long offset, long whence, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (whence is < 0 or > 2) throw new LythonRuntimeException("ValueError", "Invalid whence", span);
            if (whence == 0)
            {
                if (offset < 0) throw new LythonRuntimeException("ValueError", "Negative seek position", span);
                _position = offset;
            }
            else
            {
                var origin = whence == 1 ? _position : _length;
                if (offset > 0 && origin > long.MaxValue - offset)
                    throw new LythonRuntimeException("OverflowError", "new position too large", span);
                _position = Math.Max(0, origin + offset);
            }
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
                var capacity = _buffer.Length;
                _buffer = [];
                _length = 0;
                _closed = true;
                _services.MemoryGovernor.Release(capacity);
                ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
            }
            return PyNone.Instance;
        }

        internal void ReleaseFailedConstruction() => _services.MemoryGovernor.Release(OwnedBytes);
        internal void RewindInitialValue() => _position = 0;
        public object Enter() { EnsureOpen(_creationSpan); return this; }
        public bool Exit(object exceptionType, object exceptionValue, object traceback) { Close(); return false; }
        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var line = Read(-1, true, _creationSpan);
            value = line;
            return line.Length != 0;
        }
        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var line)) yield return line;
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<_io.BytesIO object>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
