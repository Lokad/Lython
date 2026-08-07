using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private static BoundCallable UnsupportedPathMember(string name, string message)
            => new((object[] arguments, LythonSourceSpan span, ExecutionContext context) =>
            {
                _ = arguments;
                _ = context;
                throw new LythonRuntimeException("NotImplementedError", message, span);
            }, name: name);

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

        private static PyPath RequirePath(object value, string signature, LythonSourceSpan span)
        {
            return value switch
            {
                PyPath path => path,
                _ when PyStringOps.TryAsString(value, out var text) => new PyPath(PathOps.NormalizeLexical(text)),
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects a Path or string argument.", span)
            };
        }

        private static PyList PathSuffixes(PyString path)
        {
            var name = PathOps.BaseName(path.AsString());
            var suffixes = new List<object>();
            var dot = name.IndexOf('.', name.StartsWith(".", StringComparison.Ordinal) ? 1 : 0);
            while (dot >= 0 && dot < name.Length - 1)
            {
                var next = name.IndexOf('.', dot + 1);
                suffixes.Add(PyString.FromString(next < 0 ? name[dot..] : name[dot..next]));
                dot = next;
            }

            return new PyList(suffixes);
        }

        private static bool ParseOptionalBool(object[] arguments, int index, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is null or PyNone)
            {
                return defaultValue;
            }

            return arguments[index] is bool value
                ? value
                : throw new LythonRuntimeException("TypeError", $"{owner} expects {parameterName} to be a bool.", span);
        }

        private static void ValidateIgnoredPathMode(object[] arguments, int index, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is null or PyNone)
            {
                return;
            }

            if (!Numbers.PyNumberOps.TryAsInteger(arguments[index], out _))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects mode to be an integer.", span);
            }
        }

        private static void PathMkDir(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.mkdir([mode][, parents][, exist_ok])", span);
            var parents = ParseOptionalBool(arguments, 1, false, "Path.mkdir([mode][, parents][, exist_ok])", "parents", span);
            var existOk = ParseOptionalBool(arguments, 2, false, "Path.mkdir([mode][, parents][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);

            if (parents)
            {
                PathMkDirs(normalized, existOk, span, context);
                return;
            }

            if (existOk)
            {
                context.RegisterHostCall(span);
                var stat = context.HostStat(normalized, span);
                if (stat.Exists && stat.IsDir)
                {
                    return;
                }

                if (stat.Exists)
                {
                    throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
                }
            }

            context.RegisterHostCall(span);
            context.HostMkDir(normalized, span);
        }

        private static async ValueTask PathMkDirAsync(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.mkdir([mode][, parents][, exist_ok])", span);
            var parents = ParseOptionalBool(arguments, 1, false, "Path.mkdir([mode][, parents][, exist_ok])", "parents", span);
            var existOk = ParseOptionalBool(arguments, 2, false, "Path.mkdir([mode][, parents][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);

            if (parents)
            {
                await PathMkDirsAsync(normalized, existOk, span, context).ConfigureAwait(false);
                return;
            }

            if (existOk)
            {
                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
                if (stat.Exists && stat.IsDir)
                {
                    return;
                }

                if (stat.Exists)
                {
                    throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
                }
            }

            context.RegisterHostCall(span);
            await context.HostMkDirAsync(normalized, span).ConfigureAwait(false);
        }

        private static void PathMkDirs(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.RegisterHostCall(span);
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
                context.RegisterHostCall(span);
                var currentStat = context.HostStat(current, span);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                context.HostMkDir(current, span);
            }
        }

        private static async ValueTask PathMkDirsAsync(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.RegisterHostCall(span);
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
                context.RegisterHostCall(span);
                var currentStat = await context.HostStatAsync(current, span).ConfigureAwait(false);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                await context.HostMkDirAsync(current, span).ConfigureAwait(false);
            }
        }

        private static void PathTouch(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.touch([mode][, exist_ok])", span);
            var existOk = ParseOptionalBool(arguments, 1, true, "Path.touch([mode][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);
            context.RegisterHostCall(span);
            var stat = context.HostStat(normalized, span);
            if (stat.Exists)
            {
                if (existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.touch() target already exists: {normalized}", span);
            }

            context.RegisterHostCall(span);
            context.WriteTextUtf8(normalized, Array.Empty<byte>(), span);
        }

        private static async ValueTask PathTouchAsync(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.touch([mode][, exist_ok])", span);
            var existOk = ParseOptionalBool(arguments, 1, true, "Path.touch([mode][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
            if (stat.Exists)
            {
                if (existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.touch() target already exists: {normalized}", span);
            }

            context.RegisterHostCall(span);
            await context.WriteTextUtf8Async(normalized, Array.Empty<byte>(), span).ConfigureAwait(false);
        }

        private sealed class PathOpenCallable(string path) : ICallable
        {
            private static readonly LythonCallableSignature CallSignature = new(
                "Path.open",
                ["mode", "buffering", "encoding", "errors", "newline"],
                RequiredCount: 0);

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var (mode, encodingMode, errors, newline) = ParsePathOpenArguments(BindArguments(arguments, span), span);
                return OpenTextFile(path, mode, encodingMode, errors, newline, span, context);
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var (mode, encodingMode, errors, newline) = ParsePathOpenArguments(BindArguments(arguments, span), span);
                return mode switch
                {
                    "r" => await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    "a" => await LythonRuntime.ExecutionContext.TextFileHandle.ForAppendAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                };
            }

            private static BoundOpenArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
                => BoundOpenArguments.From(CallBinder.BindNamedArgumentsWithPresence(arguments, span, CallSignature, PythonCallableKind.Method));

            private static object OpenTextFile(
                string path,
                string mode,
                TextEncodingMode encodingMode,
                TextErrorMode errors,
                TextNewlineMode newline,
                LythonSourceSpan span,
                ExecutionContext context)
            {
                return mode switch
                {
                    "r" => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(path, context, encodingMode, errors, newline),
                    "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    "a" => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path, context, encodingMode, errors, newline),
                    _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                };
            }
        }

        private static (string Mode, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathOpenArguments(BoundOpenArguments boundArguments, LythonSourceSpan span)
        {
            var arguments = boundArguments.Values;
            if (boundArguments.Count > 5)
            {
                throw new LythonRuntimeException("TypeError", "Path.open([mode][, buffering][, encoding][, errors][, newline]) expects supported text-mode options.", span);
            }

            var mode = boundArguments.Assigned[0]
                ? arguments[0] switch
                {
                    PyString text => text,
                    _ => throw new LythonRuntimeException("TypeError", "Path.open(mode) expects mode to be a string.", span)
                }
                : PyString.FromString("r");

            if (boundArguments.Count >= 2)
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

            return (ParseTextOpenMode(mode, "Path.open()", span), encodingMode, errors, newline);
        }

        private static (TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathReadTextArguments(object[] arguments, LythonSourceSpan span)
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

            return (encodingMode, errors, newline);
        }

        private static (PyString Text, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathWriteTextArguments(object[] arguments, LythonSourceSpan span)
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

            return (text, encodingMode, errors, newline);
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
            if (encodingMode == TextEncodingMode.Latin1)
            {
                using var payload = ReadGovernedHostBytes(path, context, span);
                text = DecodeText(payload.Memory, encodingMode, context, span, errors, newline);
            }
            else
            {
                text = StripUtf8Bom(ReadGovernedHostText(path, context, span, errors, newline), encodingMode);
            }

            context.ObserveString(text, span);
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
            if (encodingMode == TextEncodingMode.Latin1)
            {
                using var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
                text = DecodeText(payload.Memory, encodingMode, context, span, errors, newline);
            }
            else
            {
                text = StripUtf8Bom(
                    await ReadGovernedHostTextAsync(path, context, span, errors, newline).ConfigureAwait(false),
                    encodingMode);
            }

            context.ObserveString(text, span);
            return text;
        }

        private static byte[] EncodePathText(
            PyString text,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
            => EncodeText(text, encodingMode, errors, newline, context, span);

        private static void WriteEncodedHostText(
            string path,
            ReadOnlyMemory<byte> payload,
            TextEncodingMode encoding,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (encoding == TextEncodingMode.Latin1)
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
            => encoding == TextEncodingMode.Latin1
                ? context.WriteHostBytesAsync(path, payload, span)
                : context.WriteTextUtf8Async(path, payload, span);

        private static IEnumerable<object> EnumerateRecursive(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span)
        {
            context.RegisterHostCall(span);
            foreach (var name in context.HostListDir(root.AsString(), span))
            {
                context.CheckExecutionBudget(span);
                var child = new PyPath(PathOps.Join(root, PyString.FromString(name)));
                context.RegisterHostCall(span);
                var stat = context.HostStat(child.Value.AsString(), span);
                if (stat.IsDir)
                {
                    foreach (var nested in EnumerateRecursive(child.Value, pattern, context, span))
                    {
                        yield return nested;
                    }

                    continue;
                }

                if (stat.IsFile && MatchRglobPattern(name, pattern))
                {
                    yield return child;
                }
            }
        }

        private static async ValueTask EnumerateRecursiveAsync(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span, PyList results)
        {
            context.RegisterHostCall(span);
            var names = await context.HostListDirAsync(root.AsString(), span).ConfigureAwait(false);
            foreach (var name in names)
            {
                context.CheckExecutionBudget(span);
                var child = new PyPath(PathOps.Join(root, PyString.FromString(name)));
                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(child.Value.AsString(), span).ConfigureAwait(false);
                if (stat.IsDir)
                {
                    await EnumerateRecursiveAsync(child.Value, pattern, context, span, results).ConfigureAwait(false);
                    continue;
                }

                if (stat.IsFile && MatchRglobPattern(name, pattern))
                {
                    results.Add(child);
                    context.ObserveCollectionCount(results.Count, span);
                }
            }
        }

        private static bool MatchRglobPattern(string name, PyString pattern)
        {
            return LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern);
        }
    }
}
