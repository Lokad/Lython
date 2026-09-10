using System.Diagnostics;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIteratorValue
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
                return _state is TextFileReadState;
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
                    TextFileReadState reader => reader.Position,
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
                PyString text;
                if (encoding == TextEncodingMode.Latin1)
                {
                    using var payload = ReadGovernedHostBytes(path, context, null);
                    text = DecodeText(payload.Memory, encoding, context, null, errors, newline);
                }
                else
                {
                    text = ReadGovernedHostText(path, context, null, errors, newline);
                    if (encoding == TextEncodingMode.Utf8Bom)
                    {
                        text = StripUtf8Bom(text, encoding);
                    }
                }

                context.ObserveString(text, null);
                ChargeFileHandleValue(context.MemoryGovernor, null);
                return new TextFileHandle(path, new TextFileReadState(text, newline), context, encoding, errors);
            }

            public static async ValueTask<TextFileHandle> ForReadAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                PyString text;
                if (encoding == TextEncodingMode.Latin1)
                {
                    using var payload = await ReadGovernedHostBytesAsync(path, context, null).ConfigureAwait(false);
                    text = DecodeText(payload.Memory, encoding, context, null, errors, newline);
                }
                else
                {
                    text = await ReadGovernedHostTextAsync(path, context, null, errors, newline).ConfigureAwait(false);
                    if (encoding == TextEncodingMode.Utf8Bom)
                    {
                        text = StripUtf8Bom(text, encoding);
                    }
                }

                context.ObserveString(text, null);
                ChargeFileHandleValue(context.MemoryGovernor, null);
                return new TextFileHandle(path, new TextFileReadState(text, newline), context, encoding, errors);
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

            private TextFileReadState RequireReader()
                => _state as TextFileReadState
                    ?? throw new LythonRuntimeException("ValueError", "file is not open for reading", null);

            private TextFileWriteState RequireWriter()
                => _state as TextFileWriteState
                    ?? throw new LythonRuntimeException("ValueError", "file is not open for writing", null);

            private void EnsureOpen()
            {
                if (IsClosed)
                {
                    throw new LythonRuntimeException("ValueError", "I/O operation on closed file", null);
                }
            }

        }

        private enum TextFileMode
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

        private abstract class TextFileState(TextFileMode mode)
        {
            public TextFileMode Mode { get; } = mode;
        }
    }
}
