using System.Diagnostics;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIteratorValue, IPyAsyncIteratorValue, IExecutionFileWriter
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
                _services = context.Services;
                _encoding = encoding;
                _errors = errors;
            }

            private readonly TextFileState _state;
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

            public string EncodingName => _encoding switch
            {
                TextEncodingMode.Utf8Bom => "utf-8-sig",
                TextEncodingMode.Latin1 => "iso8859-1",
                TextEncodingMode.Ascii => "ascii",
                TextEncodingMode.Windows1252 => "cp1252",
                TextEncodingMode.Utf16 => "utf-16",
                TextEncodingMode.Utf16LittleEndian => "utf-16-le",
                TextEncodingMode.Utf16BigEndian => "utf-16-be",
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
                    ChunkedTextFileReadState when IsUtf16Encoding(_encoding) => throw new LythonRuntimeException(
                        "NotImplementedError", "file.tell() decoding-position cookies are unsupported for UTF-16 readers.", null),
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
                try
                {
                    ChargeFileHandleValue(context.MemoryGovernor, null);
                    return new TextFileHandle(path, state, context, encoding, errors);
                }
                catch
                {
                    state.ReleaseUnpublished();
                    throw;
                }
            }

            public static async ValueTask<TextFileHandle> ForReadAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var state = await OpenChunkedReaderAsync(path, context, encoding, errors, newline).ConfigureAwait(false);
                try
                {
                    ChargeFileHandleValue(context.MemoryGovernor, null);
                    return new TextFileHandle(path, state, context, encoding, errors);
                }
                catch
                {
                    state.ReleaseUnpublished();
                    throw;
                }
            }

            // Validate metadata at open; pull and decode input only when consumed.
            private static async ValueTask<ChunkedTextFileReadState> OpenChunkedReaderAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                context.CheckExecution(null);
                var stat = await context.HostStatAsync(path, null).ConfigureAwait(false);
                CheckOpenStat(path, stat);
                context.CheckExecution(null);
                return new ChunkedTextFileReadState(path, context, encoding, errors, newline, WindowForKnownSize(stat));
            }

            private static ChunkedTextFileReadState OpenChunkedReader(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                context.CheckExecution(null);
                var stat = context.HostStat(path, null);
                CheckOpenStat(path, stat);
                context.CheckExecution(null);
                return new ChunkedTextFileReadState(path, context, encoding, errors, newline, WindowForKnownSize(stat));
            }

            private static void CheckOpenStat(string path, LythonPathStat stat)
            {
                if (!stat.Exists)
                    throw new LythonRuntimeException("FileNotFoundError", "No such file: " + path, null);
                if (!stat.IsFile)
                    throw new LythonRuntimeException("IsADirectoryError", "Is a directory: " + path, null);
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

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenWriterAsync(path, TextFileWriteMode.Write, context, encoding, errors, newline, false).GetAwaiter().GetResult();

            public static ValueTask<TextFileHandle> ForWriteAsync(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenWriterAsync(path, TextFileWriteMode.Write, context, encoding, errors, newline, true);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenWriterAsync(path, TextFileWriteMode.Append, context, encoding, errors, newline, false).GetAwaiter().GetResult();

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline)
                => OpenWriterAsync(path, TextFileWriteMode.Append, context, encoding, errors, newline, true);

            private static async ValueTask<TextFileHandle> OpenWriterAsync(string path, TextFileWriteMode mode,
                ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline, bool asynchronous)
            {
                context.CheckExecution(null);
                var stat = asynchronous
                    ? await context.HostStatAsync(path, null).ConfigureAwait(false)
                    : context.HostStat(path, null);
                if (stat.Exists && !stat.IsFile)
                    throw new LythonRuntimeException("IsADirectoryError", "Is a directory: " + path, null);
                var handle = CreateWriteHandle(path, mode, context, encoding, errors, newline,
                    mode == TextFileWriteMode.Append && stat.Exists ? stat.Size : BigInteger.Zero);
                try
                {
                    if (mode == TextFileWriteMode.Write || !stat.Exists)
                    {
                        var binary = UsesBinaryTextTransport(encoding);
                        if (mode == TextFileWriteMode.Write)
                        {
                            if (binary)
                            {
                                if (asynchronous) await context.Services.WriteHostBytesAsync(path, ReadOnlyMemory<byte>.Empty, null).ConfigureAwait(false);
                                else context.Services.WriteHostBytes(path, ReadOnlyMemory<byte>.Empty, null);
                            }
                            else
                            {
                                if (asynchronous) await context.Services.WriteTextUtf8Async(path, ReadOnlyMemory<byte>.Empty, null).ConfigureAwait(false);
                                else context.Services.WriteTextUtf8(path, ReadOnlyMemory<byte>.Empty, null);
                            }
                        }
                        else
                        {
                            if (binary)
                            {
                                if (asynchronous) await context.Services.AppendHostBytesAsync(path, ReadOnlyMemory<byte>.Empty, null).ConfigureAwait(false);
                                else context.Services.AppendHostBytes(path, ReadOnlyMemory<byte>.Empty, null);
                            }
                            else
                            {
                                if (asynchronous) await context.Services.AppendTextUtf8Async(path, ReadOnlyMemory<byte>.Empty, null).ConfigureAwait(false);
                                else context.Services.AppendTextUtf8(path, ReadOnlyMemory<byte>.Empty, null);
                            }
                        }
                    }
                    context.CheckExecution(null);
                    return handle;
                }
                catch
                {
                    handle.CompleteClose();
                    context.MemoryGovernor.Release(FileHandleValueBytes);
                    throw;
                }
            }

            public object Enter() => this;

            object IExecutionFileWriter.Exit(LythonRuntime.ExecutionContext context) => Exit();
            ValueTask<object> IExecutionFileWriter.ExitAsync(LythonRuntime.ExecutionContext context) => ExitAsync();

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

                try
                {
                    if (_state is TextFileWriteState writer && _services.MemoryGovernor.AdmissionFailure is null)
                        writer.Flush(null);
                }
                catch
                {
                    if (_services.MemoryGovernor.AdmissionFailure is not null) CompleteClose();
                    throw;
                }
                return CompleteClose();
            }

            public async ValueTask<object> ExitAsync()
            {
                if (IsClosed)
                {
                    return false;
                }

                try
                {
                    if (_state is TextFileWriteState writer && _services.MemoryGovernor.AdmissionFailure is null)
                        await writer.FlushAsync(null).ConfigureAwait(false);
                }
                catch
                {
                    if (_services.MemoryGovernor.AdmissionFailure is not null) CompleteClose();
                    throw;
                }
                return CompleteClose();
            }

            private object CompleteClose()
            {
                if (_state is TextFileWriteState writer) writer.Release();
                if (_state is ChunkedTextFileReadState reader) reader.Release();
                _services.State.UntrackOpenFileWriter(this);
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

            private static TextFileHandle CreateWriteHandle(
                string path,
                TextFileWriteMode mode,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                BigInteger appendBasePosition)
            {
                ChargeFileHandleValue(context.MemoryGovernor, null);
                TextFileHandle handle = new(
                    path,
                    new TextFileWriteState(path, mode, context, encoding, errors, newline, appendBasePosition),
                    context,
                    encoding,
                    errors);
                // Outstanding writers remain live until successful publication;
                // failed flushes retain this registration for end-of-run retry.
                context.Services.State.TrackOpenFileWriter(handle);
                return handle;
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
