using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class GzipFileObject : IPyDynamicAttributes, IPyAsyncContextManager,
        IPyAsyncIteratorValue, IPyRenderableValue, IPyFinalizableOwnership, IExecutionFileWriter
    {
        internal const long ShellBytes = 512;
        private const int WriteBufferBytes = 32768;
        private const int ReadBufferBytes = 8192;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _creationSpan;
        private readonly bool _ownsStream;
        private readonly bool _reading;
        private readonly PyString _name;
        private object _stream;
        private readonly GovernedByteBuilder _pending;
        private readonly GovernedByteBuilder _output;
        private NativeWriteSink? _sink;
        private DeflateStream? _native;
        private long _nativeCharge;
        private int _level;
        private bool _nativeHasInput;
        private bool _emptySyncWritten;
        private bool _closed;
        private BigInteger _position;
        private uint _crc = uint.MaxValue;
        private uint _memberSize;
        private byte[] _window = [];
        private int _windowPosition;
        private int _windowLength;
        private GzipStreamCursor? _cursor;
        private bool _memberEnded;
        private bool _eof;
        private uint? _mtime;

        private GzipFileObject(object stream, bool ownsStream, PyString name, bool reading,
            ExecutionContext context, LythonSourceSpan span)
        {
            _stream = stream; _ownsStream = ownsStream; _name = name; _reading = reading;
            _context = context; _creationSpan = span;
            _pending = new GovernedByteBuilder(context.MemoryGovernor, span);
            _output = new GovernedByteBuilder(context.MemoryGovernor, span);
        }

        ~GzipFileObject()
        {
            // Finalization frees native state only. The long weak entry retains
            // the complete coupon until the finalized object is collected, and
            // execution-thread sweeps perform all accounting. A compressor's
            // final block is discarded without allocation or guest/host calls.
            _sink?.Discard();
            try { _native?.Dispose(); }
            catch (Exception) { /* A finalizer cannot report a guest failure. */ }
        }

        internal long OwnedBytes => ShellBytes + _nativeCharge + _window.Length
            + _pending.CommittedCapacity + _output.CommittedCapacity;
        public bool TrySnapshotOwnership(out long chargeBytes)
            => OwnershipSnapshot.Owned(_context.MemoryGovernor, OwnedBytes, out chargeBytes);
        private void Changed() => ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);

        internal static GzipFileObject Create(object stream, bool ownsStream, PyString name, bool reading,
            ExecutionContext context, LythonSourceSpan span)
        {
            context.MemoryGovernor.Reserve(ShellBytes, span);
            var result = new GzipFileObject(stream, ownsStream, name, reading, context, span);
            context.MemoryGovernor.Commit(ShellBytes);
            context.State.CallTemporaries.TrackFreshMutable(result, result.OwnedBytes, span);
            return result;
        }

        internal void InitializeCompressor(int level, LythonSourceSpan span)
        {
            _level = level;
            _context.MemoryGovernor.Reserve(ZlibModule.CompressionScratchBytes, span);
            try
            {
                _sink = new NativeWriteSink(this);
                _native = new DeflateStream(_sink, new ZLibCompressionOptions { CompressionLevel = level }, true);
            }
            catch { _context.MemoryGovernor.ReleaseReserved(ZlibModule.CompressionScratchBytes); throw; }
            _context.MemoryGovernor.Commit(ZlibModule.CompressionScratchBytes);
            _nativeCharge = ZlibModule.CompressionScratchBytes;
            Changed();
        }

        internal void AbortConstruction()
        {
            ReleaseLocal();
            GC.SuppressFinalize(this);
            _closed = true;
            _context.State.CallTemporaries.RefundUnpublishedValue(this);
        }

        private void ReleaseNative()
        {
            // Discarding a failed compressor must never allocate more output.
            _sink?.Discard();
            try { _native?.Dispose(); }
            finally
            {
                _native = null; _sink = null;
                _context.MemoryGovernor.Release(_nativeCharge);
                _nativeCharge = 0;
                Changed();
            }
        }

        private void ReleaseLocal()
        {
            ReleaseNative();
            _cursor = null;
            _pending.Release(); _output.Release();
            _context.MemoryGovernor.Release(_window.Length);
            _window = []; _windowLength = _windowPosition = 0;
            Changed();
        }

        private void EnsureOpen(LythonSourceSpan span)
        {
            if (_closed) throw RuntimeErrors.Value("I/O operation on closed file", span);
        }
        private void EnsureReadable(LythonSourceSpan span, bool bufferedBase = false)
        {
            EnsureOpen(span);
            if (!_reading)
                throw bufferedBase
                    ? new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "read", span)
                    : new LythonRuntimeException("OSError", "read() on write-only GzipFile object", span);
        }
        private void EnsureWritable(LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_reading) throw new LythonRuntimeException("OSError", "write() on read-only GzipFile object", span);
        }

        private sealed class NativeWriteSink(GzipFileObject owner) : Stream
        {
            private bool _discard;
            internal void Discard() => _discard = true;
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => owner._output.Length;
            public override long Position { get => Length; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (_discard) return;
                owner._context.CheckExecution(owner._creationSpan);
                owner._output.Append(buffer);
                owner.Changed();
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
