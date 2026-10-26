using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Text policy only. Compression, binary transport and publication remain
    // on GzipFileObject; decoding uses the ordinary file window/carry state.
    private sealed class GzipTextFile : IPyProtocolIterator, IPyRenderableValue, IExecutionFileWriter
    {
        internal const long ShellBytes = 128;
        private readonly GzipFileObject _binary;
        private readonly ExecutionServices _services;
        private readonly LythonSourceSpan _creationSpan;
        private readonly TextEncodingMode _encoding;
        private readonly TextErrorMode _errors;
        private readonly TextNewlineMode _newline;
        private readonly ExecutionContext.ChunkedTextFileReadState? _reader;
        private readonly PyString _mode;
        private bool _closed, _bomWritten;

        internal GzipTextFile(GzipFileObject binary, TextEncodingMode encoding, TextErrorMode errors,
            TextNewlineMode newline, PyString mode, ExecutionContext context, LythonSourceSpan span)
        {
            using var shell = context.MemoryGovernor.ReserveTemporary(ShellBytes, span);
            _binary = binary; _services = context.Services; _creationSpan = span;
            _encoding = encoding; _errors = errors; _newline = newline; _mode = mode;
            binary.TryGetMember("mode", out var binaryMode);
            if (((PyString)binaryMode!).AsString() == "rb")
                _reader = new(binary, context, encoding, errors, newline);
            context.State.UntrackOpenFileWriter(binary);
            context.State.TrackOpenFileWriter(this);
            shell.Commit(ShellBytes);
        }

        private void EnsureOpen(LythonSourceSpan span)
        {
            if (_closed) throw RuntimeErrors.Value("I/O operation on closed file", span);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => _closed,
                "mode" => _mode,
                "encoding" => PyString.FromString(TextEncodingName(_encoding)),
                "errors" => PyString.FromString(TextErrorName(_errors)),
                "name" => _binary.TryGetMember("name", out var label) ? label : PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };
            if (!ReferenceEquals(value, MissingMemberValue.Instance)) return true;
            var parameters = name switch
            {
                "read" or "readline" => new[] { "size" },
                "readlines" => new[] { "hint" },
                "write" => new[] { "data" },
                "writelines" => new[] { "lines" },
                "seek" => new[] { "offset", "whence" },
                "__exit__" => new[] { "type", "value", "traceback" },
                "close" or "flush" or "tell" or "readable" or "writable" or "seekable" or
                    "__enter__" or "__iter__" or "__next__" => [],
                _ => null,
            };
            if (parameters is null) return false;
            var required = name is "write" or "writelines" or "seek" ? 1 : name == "__exit__" ? 3 : 0;
            value = BoundCallable.Create(
                (args, span, context) => InvokeAsync(name, args, span, context, false).GetAwaiter().GetResult(),
                LythonCallableSignature.Create("gzip text file." + name, parameters, required),
                (args, span, context) => InvokeAsync(name, args, span, context, true));
            return true;
        }

        private async ValueTask<object> InvokeAsync(string name, object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (name is "close" or "__exit__")
            {
                await CloseFromRuntimeAsync(context, span, asynchronous).ConfigureAwait(false);
                return name == "close" ? PyNone.Instance : false;
            }
            context.CheckExecution(span);
            EnsureOpen(span);
            switch (name)
            {
                case "__enter__": case "__iter__": return this;
                case "readable": return _reader is not null;
                case "writable": return _reader is null;
                case "seekable": return false;
                case "seek": throw new LythonRuntimeException("NotImplementedError", "gzip text seek/cookies are unsupported", span);
                case "flush": return await _binary.InvokeMemberAsync("flush", [], span, context, asynchronous).ConfigureAwait(false);
                case "tell":
                    if (_reader is null) return await _binary.InvokeMemberAsync("tell", [], span, context, asynchronous).ConfigureAwait(false);
                    if (IsUtf16Encoding(_encoding)) throw new LythonRuntimeException("NotImplementedError", "gzip text UTF-16 position cookies are unsupported", span);
                    return _reader.Position;
                case "write": return await WriteAsync(args[0], context, span, asynchronous).ConfigureAwait(false);
                case "writelines":
                    if (_reader is not null) throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "not writable", span);
                    if (asynchronous)
                        await foreach (var item in PyIteration.ToSequenceAsync(args[0], span, context).ConfigureAwait(false))
                            await WriteAsync(item, context, span, true).ConfigureAwait(false);
                    else foreach (var item in PyIteration.ToSequence(args[0], span, context))
                        await WriteAsync(item, context, span, false).ConfigureAwait(false);
                    return PyNone.Instance;
                case "read": case "readline":
                {
                    if (name == "readline" && args.Length != 0 && args[0] is PyNone)
                        throw RuntimeErrors.Type("None cannot be interpreted as an integer", span);
                    var size = args.Length == 0 || args[0] is PyNone ? -1
                        : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    return await ReadAsync(size, name == "readline", context, span, asynchronous).ConfigureAwait(false);
                }
                case "__next__":
                {
                    var line = await ReadAsync(-1, true, context, span, asynchronous).ConfigureAwait(false);
                    if (line.Utf8Bytes.Length == 0) throw new LythonRuntimeException("StopIteration", "", span);
                    return line;
                }
                case "readlines":
                {
                    var hint = args.Length == 0 || args[0] is PyNone ? -1
                        : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    var lines = new PyList([], context.MemoryGovernor, span);
                    long consumed = 0;
                    while (true)
                    {
                        var line = await ReadAsync(-1, true, context, span, asynchronous).ConfigureAwait(false);
                        if (line.Utf8Bytes.Length == 0) break;
                        lines.Add(line); consumed += line.Length;
                        if (hint > 0 && consumed > hint) break;
                    }
                    return lines;
                }
                default: throw new InvalidOperationException("Unknown gzip text operation");
            }
        }

        private ValueTask<PyString> ReadAsync(long size, bool line, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            => _reader is null ? throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "not readable", span)
                : _reader.ReadGzipAsync(size, line, context, span, asynchronous);

        private async ValueTask<BigInteger> WriteAsync(object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (_reader is not null) throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "not writable", span);
            if (!PyStringOps.TryAsString(value, out var text)) throw RuntimeErrors.Type("write() argument must be str", span);
            var encoding = !_bomWritten ? _encoding : _encoding switch
            {
                TextEncodingMode.Utf8Bom => TextEncodingMode.Utf8,
                TextEncodingMode.Utf16 => TextEncodingMode.Utf16LittleEndian,
                _ => _encoding,
            };
            if (encoding == TextEncodingMode.Utf8 && _newline is TextNewlineMode.TranslateUniversal or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed)
                await _binary.WriteTextBytesAsync(text.Utf8Bytes, context, span, asynchronous).ConfigureAwait(false);
            else
            {
                using var storage = context.MemoryGovernor.ReserveTemporary(0, span);
                var bytes = EncodeText(text, encoding, _errors, _newline, context, span, outputStorage: storage);
                await _binary.WriteTextBytesAsync(bytes, context, span, asynchronous).ConfigureAwait(false);
            }
            _bomWritten = true;
            return new BigInteger(text.Length);
        }

        internal async ValueTask<bool> CloseFromRuntimeAsync(ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (_closed) return false;
            try { await _binary.CloseFromRuntimeAsync(context, span, asynchronous).ConfigureAwait(false); }
            finally
            {
                _closed = true;
                _reader?.Release();
                _services.State.UntrackOpenFileWriter(this);
            }
            return false;
        }

        internal ValueTask<PyString> ReadForLexerAsync(ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            EnsureOpen(span);
            return ReadAsync(-1, false, context, span, asynchronous);
        }
        object IExecutionFileWriter.Exit(ExecutionContext context) => CloseFromRuntimeAsync(context, _creationSpan, false).GetAwaiter().GetResult();
        async ValueTask<object> IExecutionFileWriter.ExitAsync(ExecutionContext context) => await CloseFromRuntimeAsync(context, _creationSpan, true).ConfigureAwait(false);
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<gzip text file>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
