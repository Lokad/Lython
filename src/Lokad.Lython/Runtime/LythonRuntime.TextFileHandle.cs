using System.Diagnostics;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIteratorValue, IPyAsyncIteratorValue, IExecutionFile
        {
            private TextFileHandle(
                string path,
                TextFileState state,
                OpenedByteFile file,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors)
            {
                Path = path;
                _state = state;
                _file = file;
                _services = context.Services;
                _encoding = encoding;
                _errors = errors;
            }

            private readonly TextFileState _state;
            private readonly OpenedByteFile _file;
            private bool _closed;
            // File work needs run-wide services, not the opening call's locals.
            // Reader and writer state follow the same lifetime rule.
            private readonly ExecutionServices _services;
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

            public string EncodingName => TextEncodingName(_encoding);

            public string ErrorsName => TextErrorName(_errors);

            public bool IsClosed => _closed || _file.IsClosed;

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
                    ChunkedTextFileReadState when IsUtf16Encoding(_encoding) => throw new LythonRuntimeException(
                        "NotImplementedError", "file.tell() decoding-position cookies are unsupported for UTF-16 readers.", null),
                    ChunkedTextFileReadState reader => reader.Position,
                    TextFileWriteState writer => writer.Position,
                    _ => throw new UnreachableException(),
                };
            }

            public object Flush() => FlushCoreAsync(false).GetAwaiter().GetResult();

            public ValueTask<object> FlushAsync() => FlushCoreAsync(true);

            private async ValueTask<object> FlushCoreAsync(bool asynchronous)
            {
                EnsureOpen();
                try
                {
                    if (_state is TextFileWriteState writer)
                        await writer.FlushAsync(null, asynchronous).ConfigureAwait(false);
                    return PyNone.Instance;
                }
                finally
                {
                    if (_file.IsClosed) await CompleteCloseAsync(asynchronous).ConfigureAwait(false);
                }
            }

            public object Seek(LythonSourceSpan span)
                => throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython text handles.", span);

            // Open handles retain a small shell beside governed buffers; charge one table
            // slot at every construction site. Paths alias shared literals, governed
            // computed strings, or guest-owned arguments, so they need no second charge.
            private const long FileHandleValueBytes = 320;

            private static void ChargeFileHandleValue(MemoryGovernor governor, LythonSourceSpan? span)
            {
                governor.Reserve(FileHandleValueBytes, span);
                governor.Commit(FileHandleValueBytes);
            }

            public static TextFileHandle ForRead(string path, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenAsync(path, TextFileMode.Read, context, encoding, errors, newline, false).GetAwaiter().GetResult();

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenAsync(path, TextFileMode.Read, context, encoding, errors, newline, true);

            public static TextFileHandle ForWrite(string path, ExecutionContext context)
                => ForWrite(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(string path, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenAsync(path, TextFileMode.Write, context, encoding, errors, newline, false).GetAwaiter().GetResult();

            public static ValueTask<TextFileHandle> ForWriteAsync(string path, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenAsync(path, TextFileMode.Write, context, encoding, errors, newline, true);

            public static TextFileHandle ForAppend(string path, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenAsync(path, TextFileMode.Append, context, encoding, errors, newline, false).GetAwaiter().GetResult();

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context,
                TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenAsync(path, TextFileMode.Append, context, encoding, errors, newline, true);

            private static async ValueTask<TextFileHandle> OpenAsync(string path, TextFileMode mode,
                ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline, bool asynchronous)
            {
                context.CheckExecution(null);
                ChargeFileHandleValue(context.MemoryGovernor, null);
                var file = new OpenedByteFile(context.Services);
                TextFileState? state = null;
                TextFileHandle? handle = null;
                try
                {
                    state = mode == TextFileMode.Read
                        ? new ChunkedTextFileReadState(file, context, encoding, errors, newline, ChunkedTextFileReadState.DefaultWindowBytes)
                        : new TextFileWriteState(file, mode, context, encoding, errors, newline);
                    handle = new TextFileHandle(path, state, file, context, encoding, errors);
                    context.State.TrackOpenFile(handle);
                    var options = mode == TextFileMode.Read
                        ? new LythonFileOpenOptions(LythonFileAccess.Read)
                        : new LythonFileOpenOptions(LythonFileAccess.Write, create: true,
                            truncate: mode == TextFileMode.Write, append: mode == TextFileMode.Append);
                    await file.OpenAsync(path, options, null, asynchronous).ConfigureAwait(false);
                    if (state is ChunkedTextFileReadState reader) reader.SetLengthHint(file.LengthHint(null));
                    else ((TextFileWriteState)state).InitializePosition(file.Position(null));
                    context.CheckExecution(null);
                    return handle;
                }
                catch
                {
                    if (handle is not null)
                    {
                        try { await handle.CompleteCloseAsync(asynchronous).ConfigureAwait(false); }
                        catch (Exception exception) when (exception is not OutOfMemoryException) { }
                    }
                    else await file.CloseAfterFailureAsync(null, asynchronous).ConfigureAwait(false);
                    if (state is ChunkedTextFileReadState) context.MemoryGovernor.Release(ChunkedTextFileReadState.ShellBytes);
                    context.MemoryGovernor.Release(FileHandleValueBytes);
                    throw;
                }
            }

            public object Enter() { EnsureOpen(); return this; }

            object IExecutionFile.Exit(LythonRuntime.ExecutionContext context) => Exit();
            ValueTask<object> IExecutionFile.ExitAsync(LythonRuntime.ExecutionContext context) => ExitAsync();

            object IPyContextManager.Enter() => Enter();

            ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter());

            bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
            {
                _ = Exit();
                return false;
            }

            public object Exit() => CloseAsync(false).GetAwaiter().GetResult();

            public ValueTask<object> ExitAsync() => CloseAsync(true);

            private async ValueTask<object> CloseAsync(bool asynchronous)
            {
                if (_closed) return false;
                try
                {
                    if (!_file.IsClosed && _state is TextFileWriteState writer &&
                        _services.MemoryGovernor.AdmissionFailure is null && !_services.State.CancellationRequested)
                        await writer.FlushAsync(null, asynchronous).ConfigureAwait(false);
                }
                finally { await CompleteCloseAsync(asynchronous).ConfigureAwait(false); }
                return false;
            }

            private async ValueTask CompleteCloseAsync(bool asynchronous)
            {
                if (_closed) return;
                _closed = true;
                _services.State.UntrackOpenFile(this);
                try { await _file.CloseAsync(null, asynchronous).ConfigureAwait(false); }
                finally
                {
                    if (_state is TextFileWriteState writer) writer.Release();
                    if (_state is ChunkedTextFileReadState reader) reader.Release();
                }
            }

            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                _ = await ExitAsync().ConfigureAwait(false);
                return false;
            }

            internal ValueTask<int> ReadSymbolAsync(bool asynchronous)
            {
                EnsureOpen();
                return RequireReader().ReadSymbolAsync(asynchronous);
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

            public PyList ReadLines(long hint)
            {
                EnsureOpen();
                var reader = RequireReader();
                var items = new PyList([], _services.MemoryGovernor, null);
                long totalCharacters = 0;
                while (true)
                {
                    var line = reader.ReadLine(-1);
                    if (line.Length == 0)
                    {
                        break;
                    }

                    items.Add(line);
                    totalCharacters += line.Length;
                    if (hint > 0 && totalCharacters > hint)
                    {
                        break;
                    }
                }

                return items;
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

            public async ValueTask<PyList> ReadLinesAsync(long hint)
            {
                EnsureOpen();
                var reader = RequireReader();
                var items = new PyList([], _services.MemoryGovernor, null);
                long totalCharacters = 0;
                while (true)
                {
                    var line = await reader.ReadLineAsync(-1).ConfigureAwait(false);
                    if (line.Length == 0)
                    {
                        break;
                    }

                    items.Add(line);
                    totalCharacters += line.Length;
                    if (hint > 0 && totalCharacters > hint)
                    {
                        break;
                    }
                }

                return items;
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

            private ChunkedTextFileReadState RequireReader()
                => _state as ChunkedTextFileReadState
                    ?? throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "not readable", null);

            private TextFileWriteState RequireWriter()
                => _state as TextFileWriteState
                    ?? throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "not writable", null);

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

        internal abstract class TextFileState(TextFileMode mode)
        {
            public TextFileMode Mode { get; } = mode;
        }
    }
}
