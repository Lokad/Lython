using System.Diagnostics;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIteratorValue, IPyAsyncIteratorValue
        {
            private TextFileHandle(
                string path,
                TextFileState state,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors)
            {
                Path = path;
                _state = state;
                _context = context;
                _encoding = encoding;
                _errors = errors;
            }

            private readonly TextFileState _state;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;

            public string Path { get; }

            public string Mode => _state.Mode switch
            {
                TextFileMode.Read => "r",
                TextFileMode.Write => "w",
                TextFileMode.Append => "a",
                _ => throw new UnreachableException(),
            };

            public string EncodingName => _encoding switch
            {
                TextEncodingMode.Utf8Bom => "utf-8-sig",
                TextEncodingMode.Latin1 => "iso8859-1",
                _ => "utf-8"
            };

            public string ErrorsName => TextErrorName(_errors);

            public bool IsClosed { get; private set; }

            public bool IsReadable()
            {
                EnsureOpen();
                return _state is ChunkedTextFileReadState;
            }

            public bool IsWritable()
            {
                EnsureOpen();
                return _state is TextFileWriteState;
            }

            public bool IsSeekable()
            {
                EnsureOpen();
                return false;
            }

            public BigInteger Tell()
            {
                EnsureOpen();
                return _state switch
                {
                    ChunkedTextFileReadState reader => reader.Position,
                    TextFileWriteState writer => writer.Position,
                    _ => throw new UnreachableException(),
                };
            }

            public object Flush()
            {
                EnsureOpen();
                if (_state is TextFileWriteState writer)
                {
                    writer.Flush(null);
                }

                return PyNone.Instance;
            }

            public async ValueTask<object> FlushAsync()
            {
                EnsureOpen();
                if (_state is TextFileWriteState writer)
                {
                    await writer.FlushAsync(null).ConfigureAwait(false);
                }

                return PyNone.Instance;
            }

            public object Seek(LythonSourceSpan span)
                => throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython text handles.", span);

            // Open handles retain a small shell beside governed buffers; charge one table
            // slot at every construction site. Paths alias shared literals, governed
            // computed strings, or guest-owned arguments, so they need no second charge.
            private const long FileHandleValueBytes = 64;

            private static void ChargeFileHandleValue(MemoryGovernor governor, LythonSourceSpan? span)
            {
                governor.Reserve(FileHandleValueBytes, span);
                governor.Commit(FileHandleValueBytes);
            }

            public static TextFileHandle ForRead(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var state = OpenChunkedReader(path, context, encoding, errors, newline);
                state.Prime();
                ChargeFileHandleValue(context.MemoryGovernor, null);
                return new TextFileHandle(path, state, context, encoding, errors);
            }

            public static async ValueTask<TextFileHandle> ForReadAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var state = await OpenChunkedReaderAsync(path, context, encoding, errors, newline).ConfigureAwait(false);
                await state.PrimeAsync().ConfigureAwait(false);
                ChargeFileHandleValue(context.MemoryGovernor, null);
                return new TextFileHandle(path, state, context, encoding, errors);
            }

            // Stats and pre-reserves exactly like the buffered reads this replaces,
            // then streams windows on demand. Latin-1 keeps the binary-read cap and
            // the smaller overhead its single-byte decoding was pre-reserved with.
            // The asynchronous opener stats through the async pump so delayed hosts
            // suspend instead of failing the synchronous capability check.
            private static async ValueTask<ChunkedTextFileReadState> OpenChunkedReaderAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                context.RegisterHostCall(null);
                var stat = await context.HostStatAsync(path, null).ConfigureAwait(false);
                CheckOpenStat(context, encoding, stat);
                context.RegisterHostCall(null);
                return new ChunkedTextFileReadState(path, context, encoding, errors, newline, WindowForKnownSize(stat));
            }

            private static ChunkedTextFileReadState OpenChunkedReader(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                context.RegisterHostCall(null);
                var stat = context.HostStat(path, null);
                CheckOpenStat(context, encoding, stat);
                context.RegisterHostCall(null);
                return new ChunkedTextFileReadState(path, context, encoding, errors, newline, WindowForKnownSize(stat));
            }

            private static void CheckOpenStat(
                ExecutionContext context,
                TextEncodingMode encoding,
                LythonPathStat stat)
            {
                if (stat.Exists && stat.IsFile && context.Limits.MaxHostReadBytes is { } maxHostReadBytes &&
                    stat.Size > new BigInteger(maxHostReadBytes))
                {
                    throw encoding == TextEncodingMode.Latin1
                        ? RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maxHostReadBytes})", null)
                        : RuntimeErrors.Runtime($"host text read exceeded maximum bytes ({maxHostReadBytes})", null);
                }

                EnsureExecutionMemoryForKnownLength(stat, encoding == TextEncodingMode.Latin1 ? 32L : 128L, context, null);
            }

            // Small honest files stream through small windows so bounded
            // infrastructure never prices them out of tight budgets; unknown or
            // huge sizes stream through the default window instead.
            private static int WindowForKnownSize(LythonPathStat stat)
            {
                if (stat.Exists && stat.IsFile && stat.Size > BigInteger.Zero &&
                    stat.Size <= new BigInteger(ChunkedTextFileReadState.DefaultWindowBytes))
                {
                    var size = (int)stat.Size;
                    return Math.Max(ChunkedTextFileReadState.MinimumWindowBytes, size);
                }

                return ChunkedTextFileReadState.DefaultWindowBytes;
            }


            public static TextFileHandle ForWrite(string path, ExecutionContext context)
                => ForWrite(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                ChargeFileHandleValue(context.MemoryGovernor, null);
                return new(
                    path,
                    new TextFileWriteState(path, TextFileWriteMode.Write, context, encoding, errors, newline, BigInteger.Zero),
                    context,
                    encoding,
                    errors);
            }

            public static TextFileHandle ForAppend(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                context.RegisterHostCall(null);
                var stat = context.HostStat(path, null);
                var appendBasePosition = stat is { Exists: true, IsFile: true }
                    ? stat.Size
                    : BigInteger.Zero;
                return CreateAppendHandle(path, context, encoding, errors, newline, appendBasePosition);
            }

            public static async ValueTask<TextFileHandle> ForAppendAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                context.RegisterHostCall(null);
                var stat = await context.HostStatAsync(path, null).ConfigureAwait(false);
                var appendBasePosition = stat is { Exists: true, IsFile: true }
                    ? stat.Size
                    : BigInteger.Zero;
                return CreateAppendHandle(path, context, encoding, errors, newline, appendBasePosition);
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

                if (_state is TextFileWriteState writer)
                {
                    writer.Flush(null);
                    writer.Release();
                }

                if (_state is ChunkedTextFileReadState reader)
                {
                    reader.Release();
                }

                IsClosed = true;
                return false;
            }

            public async ValueTask<object> ExitAsync()
            {
                if (IsClosed)
                {
                    return false;
                }

                if (_state is TextFileWriteState writer)
                {
                    await writer.FlushAsync(null).ConfigureAwait(false);
                    writer.Release();
                }

                if (_state is ChunkedTextFileReadState reader)
                {
                    reader.Release();
                }

                IsClosed = true;
                return false;
            }

            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                _ = await ExitAsync().ConfigureAwait(false);
                return false;
            }

            public PyString Read() => Read(-1);

            public PyString Read(int size)
            {
                EnsureOpen();
                return RequireReader().Read(size);
            }

            public PyString ReadLine() => ReadLine(-1);

            public PyString ReadLine(int size)
            {
                EnsureOpen();
                return RequireReader().ReadLine(size);
            }

            public PyList ReadLines() => ReadLines(-1);

            public PyList ReadLines(int hint)
            {
                EnsureOpen();
                var reader = RequireReader();
                var items = new List<object>();
                var totalBytes = 0;
                while (true)
                {
                    var line = reader.ReadLine(-1);
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

            public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

            public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

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

            public async ValueTask<PyIterationResult> TryMoveNextAsync()
            {
                var line = await ReadLineAsync(-1).ConfigureAwait(false);
                if (line.Length == 0)
                {
                    return PyIterationResult.End;
                }

                return PyIterationResult.Yield(line);
            }

            public async ValueTask<PyString> ReadAsync(int size)
            {
                EnsureOpen();
                return await RequireReader().ReadAsync(size).ConfigureAwait(false);
            }

            public async ValueTask<PyString> ReadLineAsync(int size)
            {
                EnsureOpen();
                return await RequireReader().ReadLineAsync(size).ConfigureAwait(false);
            }

            public async ValueTask<PyList> ReadLinesAsync(int hint)
            {
                EnsureOpen();
                var reader = RequireReader();
                var items = new List<object>();
                var totalBytes = 0;
                while (true)
                {
                    var line = await reader.ReadLineAsync(-1).ConfigureAwait(false);
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

            public BigInteger Write(PyString text)
            {
                EnsureOpen();
                return RequireWriter().Write(text);
            }

            public object WriteLines(object value, LythonSourceSpan span, ExecutionContext context)
            {
                EnsureOpen();
                var writer = RequireWriter();
                foreach (var item in ToSequence(value, span, context))
                {
                    if (!PyStringOps.TryAsString(item, out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects an iterable of strings.", span);
                    }

                    _ = writer.Write(text);
                }

                return PyNone.Instance;
            }

            private static TextFileHandle CreateAppendHandle(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                BigInteger appendBasePosition)
            {
                ChargeFileHandleValue(context.MemoryGovernor, null);
                return new(
                    path,
                    new TextFileWriteState(path, TextFileWriteMode.Append, context, encoding, errors, newline, appendBasePosition),
                    context,
                    encoding,
                    errors);
            }

            private ChunkedTextFileReadState RequireReader()
                => _state as ChunkedTextFileReadState
                    ?? throw new LythonRuntimeException("ValueError", "file is not open for reading", null);

            private TextFileWriteState RequireWriter()
                => _state as TextFileWriteState
                    ?? throw new LythonRuntimeException("ValueError", "file is not open for writing", null);

            internal void EnsureOpen()
            {
                if (IsClosed)
                {
                    throw new LythonRuntimeException("ValueError", "I/O operation on closed file", null);
                }
            }

        }

        internal enum TextFileMode
        {
            Read,
            Write,
            Append,
        }

        private enum TextFileWriteMode
        {
            Write,
            Append,
        }

        internal abstract class TextFileState(TextFileMode mode)
        {
            public TextFileMode Mode { get; } = mode;
        }
    }
}
