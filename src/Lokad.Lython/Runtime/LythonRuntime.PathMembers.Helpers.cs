using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private readonly record struct PathOpenOptions(
            TextFileOperation Operation,
            TextEncodingMode EncodingMode,
            TextErrorMode Errors,
            TextNewlineMode Newline,
            bool Binary);

        private readonly record struct PathReadTextOptions(
            TextEncodingMode EncodingMode,
            TextErrorMode Errors,
            TextNewlineMode Newline);

        private readonly record struct PathWriteTextOptions(
            PyString Text,
            TextEncodingMode EncodingMode,
            TextErrorMode Errors,
            TextNewlineMode Newline);

        private static BoundCallable UnsupportedPathMember(LythonCallableSignature signature, string message)
            => BoundCallable.Create((object[] arguments, LythonSourceSpan span, ExecutionContext context) =>
            {
                _ = arguments;
                _ = context;
                throw new LythonRuntimeException("NotImplementedError", message, span);
            }, signature);

        private static PyString ParsePathGlobArguments(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(pattern[, case_sensitive][, recurse_symlinks]) expects a string pattern.", span);
            }

            if (arguments.Length >= 2 &&
                arguments[1] is not PyNone &&
                arguments[1] is not true)
            {
                throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., case_sensitive=False) is not supported by Lython's normalized path matcher.", span);
            }

            if (arguments.Length >= 3 &&
                arguments[2] is not PyNone and not false)
            {
                throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., recurse_symlinks=True) is not supported by Lython because symlink traversal is outside the host path model.", span);
            }

            return pattern;
        }

        private static PyPath RequirePath(object value, string signature, LythonSourceSpan span, ExecutionContext context, bool pure = false)
        {
            var text = PathOps.NormalizeLexical(CoercePathLike(value,context,span,signature),context,span);
            return OwnPathResult(text,text,context.MemoryGovernor,span,pure);
        }

        private static async ValueTask<PyPath> RequirePathAsync(object value, string signature, LythonSourceSpan span, ExecutionContext context, bool pure = false)
        {
            var text = PathOps.NormalizeLexical(await CoercePathLikeAsync(value,context,span,signature).ConfigureAwait(false),context,span);
            return OwnPathResult(text,text,context.MemoryGovernor,span,pure);
        }

        private static bool Supplied(BoundCallArguments arguments, int index)
            => index < arguments.Assigned.Length && arguments.Assigned[index];

        private static async ValueTask ValidatePathMode(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (!Supplied(arguments,0)) return;
            var value = arguments.Values[0];
            if (asynchronous) value = await ConvertIntegerIndexAsync(value,context,span).ConfigureAwait(false);
            else if (TryInvokeUnarySpecialMethod(value,"__index__",context,span,out var converted))
                value = ValidateIntegerConversionResult(converted,"__index__",context,span);
            if (!PyNumberOps.TryAsInteger(value,out _)) throw RuntimeErrors.Type("mode must be an integer",span);
        }

        private static async ValueTask<object> PathMkDirCoreAsync(PyString path, BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            await ValidatePathMode(arguments,span,context,asynchronous).ConfigureAwait(false);
            var parents = Supplied(arguments,1) && (asynchronous ? await IsTruthyAsync(arguments.Values[1],context,span).ConfigureAwait(false) : IsTruthy(arguments.Values[1],context,span));
            var existOk = Supplied(arguments,2) && (asynchronous ? await IsTruthyAsync(arguments.Values[2],context,span).ConfigureAwait(false) : IsTruthy(arguments.Values[2],context,span));
            using var scratch = PathOps.ReserveWorking(path,context,span);
            var normalized = PathOps.Normalize(path.AsString(),context.Host.Cwd);
            if (parents)
            {
                if (asynchronous) await PathMkDirsAsync(normalized,existOk,span,context).ConfigureAwait(false);
                else PathMkDirs(normalized,existOk,span,context);
            }
            else
            {
                if (existOk)
                {
                    var stat = asynchronous ? await context.HostStatAsync(normalized,span).ConfigureAwait(false) : context.HostStat(normalized,span);
                    if (stat.Exists && stat.IsDir) return PyNone.Instance;
                    if (stat.Exists) throw RuntimeErrors.Runtime("Path.mkdir() target already exists",span);
                }
                if (asynchronous) await context.HostMkDirAsync(normalized,span).ConfigureAwait(false);
                else context.HostMkDir(normalized,span);
            }
            return PyNone.Instance;
        }

        private static void PathMkDirs(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var stat = context.HostStat(normalized, span);
            if (stat.Exists)
            {
                if (stat.IsDir && existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
            }

            foreach (var current in EnumerateMissingDirectories(normalized))
            {
                context.CheckExecution(span);
                var currentStat = context.HostStat(current, span);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.CheckExecution(span);
                context.HostMkDir(current, span);
            }
        }

        private static async ValueTask PathMkDirsAsync(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
            if (stat.Exists)
            {
                if (stat.IsDir && existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
            }

            foreach (var current in EnumerateMissingDirectories(normalized))
            {
                context.CheckExecution(span);
                var currentStat = await context.HostStatAsync(current, span).ConfigureAwait(false);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.CheckExecution(span);
                await context.HostMkDirAsync(current, span).ConfigureAwait(false);
            }
        }

        private static async ValueTask<object> PathTouchCoreAsync(PyString path, BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            await ValidatePathMode(arguments,span,context,asynchronous).ConfigureAwait(false);
            var existOk = !Supplied(arguments,1) || (asynchronous ? await IsTruthyAsync(arguments.Values[1],context,span).ConfigureAwait(false) : IsTruthy(arguments.Values[1],context,span));
            using var scratch = PathOps.ReserveWorking(path,context,span);
            var normalized = PathOps.Normalize(path.AsString(),context.Host.Cwd);
            var stat = asynchronous ? await context.HostStatAsync(normalized,span).ConfigureAwait(false) : context.HostStat(normalized,span);
            if (stat.Exists)
            {
                if (!existOk) throw RuntimeErrors.Runtime("Path.touch() target already exists",span);
            }
            else if (asynchronous) await context.WriteTextUtf8Async(normalized,Array.Empty<byte>(),span).ConfigureAwait(false);
            else context.WriteTextUtf8(normalized,Array.Empty<byte>(),span);
            return PyNone.Instance;
        }

        private sealed class PathOpenCallable(PyString pathValue) : ICallable
        {
            private static readonly LythonCallableSignature CallSignature = LythonCallableSignature.Create(
                "Path.open",
                ["mode", "buffering", "encoding", "errors", "newline"],
                requiredCount: 0);

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                using var scratch = PathOps.ReserveWorking(pathValue,context,span);
                var pathBytes = checked(64L + 2L * pathValue.Utf8Bytes.Length);
                context.MemoryGovernor.Reserve(pathBytes,span);
                context.MemoryGovernor.Commit(pathBytes);
                var path = pathValue.AsString();
                context.CheckExecution(span);
                var (operation, encodingMode, errors, newline, binary) = ParsePathOpenArgumentsAsync(BindArguments(arguments, span), span, context, false).GetAwaiter().GetResult();
                if (binary) return ExecutionContext.BinaryFileHandle.OpenAsync(path, operation, context, span, false).GetAwaiter().GetResult();
                return OpenTextFile(path, operation, encodingMode, errors, newline, context);
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                using var scratch = PathOps.ReserveWorking(pathValue,context,span);
                var pathBytes = checked(64L + 2L * pathValue.Utf8Bytes.Length);
                context.MemoryGovernor.Reserve(pathBytes,span);
                context.MemoryGovernor.Commit(pathBytes);
                var path = pathValue.AsString();
                context.CheckExecution(span);
                var (operation, encodingMode, errors, newline, binary) = await ParsePathOpenArgumentsAsync(BindArguments(arguments, span), span, context, true).ConfigureAwait(false);
                if (binary) return await ExecutionContext.BinaryFileHandle.OpenAsync(path, operation, context, span, true).ConfigureAwait(false);
                return operation switch
                {
                    TextFileOperation.Read => await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    TextFileOperation.Write => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    TextFileOperation.Append => await LythonRuntime.ExecutionContext.TextFileHandle.ForAppendAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown text file operation."),
                };
            }

            private static BoundOpenArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
                => BoundOpenArguments.From(CallBinder.BindNamedArgumentsWithPresence(arguments, span, CallSignature, PythonCallableKind.Method));

            private static object OpenTextFile(
                string path,
                TextFileOperation operation,
                TextEncodingMode encodingMode,
                TextErrorMode errors,
                TextNewlineMode newline,
                ExecutionContext context)
            {
                return operation switch
                {
                    TextFileOperation.Read => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(path, context, encodingMode, errors, newline),
                    TextFileOperation.Write => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    TextFileOperation.Append => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path, context, encodingMode, errors, newline),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown text file operation."),
                };
            }
        }

        private static async ValueTask<PathOpenOptions> ParsePathOpenArgumentsAsync(BoundOpenArguments boundArguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = boundArguments.Values;
            if (boundArguments.Count > 5)
            {
                throw new LythonRuntimeException("TypeError", "Path.open([mode][, buffering][, encoding][, errors][, newline]) expects supported file-mode options.", span);
            }

            var mode = boundArguments.Assigned[0]
                ? arguments[0] switch
                {
                    PyString text => text,
                    _ => throw new LythonRuntimeException("TypeError", "Path.open(mode) expects mode to be a string.", span)
                }
                : PyString.FromString("r");

            var (operation, binary) = ParseFileOpenMode(mode, "Path.open()", span);
            if (binary) await ValidateBinaryOpenOptionsAsync(boundArguments, 1, "Path.open()", span, context, asynchronous).ConfigureAwait(false);

            if (!binary && boundArguments.Count >= 2)
            {
                ValidateTextBuffering(arguments[1], "Path.open()", span);
            }

            var encodingMode = boundArguments.Count >= 3
                ? ParseTextEncoding(arguments[2], "Path.open()", span)
                : TextEncodingMode.Utf8;
            var errors = boundArguments.Count >= 4
                ? ParseTextErrors(arguments[3], "Path.open()", span)
                : TextErrorMode.Strict;
            var newline = boundArguments.Count >= 5
                ? ParseTextNewline(arguments[4], "Path.open()", span)
                : TextNewlineMode.TranslateUniversal;

            return new PathOpenOptions(operation, encodingMode, errors, newline, binary);
        }

        private static PathReadTextOptions ParsePathReadTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.read_text([encoding][, errors][, newline]) expects zero to three arguments.", span);
            }

            var encodingMode = arguments.Length >= 1
                ? ParseTextEncoding(arguments[0], "Path.read_text()", span)
                : TextEncodingMode.Utf8;
            var errors = arguments.Length >= 2
                ? ParseTextErrors(arguments[1], "Path.read_text()", span)
                : TextErrorMode.Strict;
            var newline = arguments.Length >= 3
                ? ParseTextNewline(arguments[2], "Path.read_text()", span)
                : TextNewlineMode.TranslateUniversal;

            return new PathReadTextOptions(encodingMode, errors, newline);
        }

        private static PathWriteTextOptions ParsePathWriteTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Path.write_text(text[, encoding][, errors][, newline]) expects a string plus optional keyword-compatible arguments.", span);
            }

            var encodingMode = arguments.Length >= 2
                ? ParseTextEncoding(arguments[1], "Path.write_text()", span)
                : TextEncodingMode.Utf8;
            var errors = arguments.Length >= 3
                ? ParseTextErrors(arguments[2], "Path.write_text()", span)
                : TextErrorMode.Strict;
            var newline = arguments.Length == 4
                ? ParseTextNewline(arguments[3], "Path.write_text()", span)
                : TextNewlineMode.TranslateUniversal;

            return new PathWriteTextOptions(text, encodingMode, errors, newline);
        }

        private static PyString ReadPathText(
            string path,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            PyString text;
            if (UsesBinaryTextTransport(encodingMode))
            {
                using var payload = ReadGovernedHostBytes(path, context, span);
                text = DecodeText(payload.Memory, encodingMode, context, span, errors, newline, stream: true);
            }
            else
            {
                text = StripUtf8Bom(ReadGovernedHostText(path, context, span, errors, newline), encodingMode);
            }

            context.CheckExecution(span);
            return text;
        }

        private static async ValueTask<PyString> ReadPathTextAsync(
            string path,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            PyString text;
            if (UsesBinaryTextTransport(encodingMode))
            {
                using var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
                text = DecodeText(payload.Memory, encodingMode, context, span, errors, newline, stream: true);
            }
            else
            {
                text = StripUtf8Bom(
                    await ReadGovernedHostTextAsync(path, context, span, errors, newline).ConfigureAwait(false),
                    encodingMode);
            }

            context.CheckExecution(span);
            return text;
        }

        private static byte[] EncodePathText(
            PyString text,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span,
            bool outputAlreadyFunded)
            => EncodeText(text, encodingMode, errors, newline, context, span, outputAlreadyFunded);

        private static void WriteEncodedHostText(
            string path,
            ReadOnlyMemory<byte> payload,
            TextEncodingMode encoding,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (UsesBinaryTextTransport(encoding))
            {
                context.WriteHostBytes(path, payload, span);
            }
            else
            {
                context.WriteTextUtf8(path, payload, span);
            }
        }

        private static ValueTask WriteEncodedHostTextAsync(
            string path,
            ReadOnlyMemory<byte> payload,
            TextEncodingMode encoding,
            ExecutionContext context,
            LythonSourceSpan span)
            => UsesBinaryTextTransport(encoding)
                ? context.WriteHostBytesAsync(path, payload, span)
                : context.WriteTextUtf8Async(path, payload, span);

        private static IEnumerable<object> EnumerateRecursive(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                context.CheckExecution(span);
                foreach (var name in context.HostListDir(root.AsString(), span))
                {
                    context.CheckExecution(span);
                    var child = OwnPathResult(PathOps.Join(root, PyString.FromString(name)),root,context.MemoryGovernor,span);
                    context.CheckExecution(span);
                    var stat = context.HostStat(child.Value.AsString(), span);
                    if (stat.IsDir)
                    {
                        foreach (var nested in EnumerateRecursive(child.Value, pattern, context, span))
                        {
                            yield return nested;
                        }

                        continue;
                    }

                    if (stat.IsFile && MatchRglobPattern(name, pattern, context.Limits.CancellationToken))
                    {
                        yield return child;
                    }
                }
            }
            finally { context.LeaveInterpreterFrame(); }
        }

        private static async ValueTask EnumerateRecursiveAsync(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span, PyList results)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                context.CheckExecution(span);
                var names = await context.HostListDirAsync(root.AsString(), span).ConfigureAwait(false);
                foreach (var name in names)
                {
                    context.CheckExecution(span);
                    var child = OwnPathResult(PathOps.Join(root, PyString.FromString(name)),root,context.MemoryGovernor,span);
                    context.CheckExecution(span);
                    var stat = await context.HostStatAsync(child.Value.AsString(), span).ConfigureAwait(false);
                    if (stat.IsDir)
                    {
                        await EnumerateRecursiveAsync(child.Value, pattern, context, span, results).ConfigureAwait(false);
                        continue;
                    }

                    if (stat.IsFile && MatchRglobPattern(name, pattern, context.Limits.CancellationToken))
                    {
                        results.Add(child);
                        context.CheckExecution(span);
                    }
                }
            }
            finally { context.LeaveInterpreterFrame(); }
        }

        private static bool MatchRglobPattern(string name, PyString pattern, CancellationToken cancellationToken)
        {
            return LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern, cancellationToken);
        }
    }
}
