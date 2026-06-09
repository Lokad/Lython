using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class GlobModule : PyModule
    {
        public static readonly GlobModule Instance = new();

        private GlobModule() : base("glob")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "glob" => new BuiltinCallable(LythonKnownCallableSignatures.Glob, Glob, GlobAsync),
                "iglob" => new BuiltinCallable(LythonKnownCallableSignatures.IGlob, IGlob, IGlobAsync),
                "escape" => new BuiltinCallable(LythonKnownCallableSignatures.GlobEscape, Escape),
                "has_magic" => new BuiltinCallable(LythonKnownCallableSignatures.GlobHasMagic, HasMagic),
                "translate" => new BuiltinCallable(LythonKnownCallableSignatures.GlobTranslate, Translate),
                "glob0" => new BuiltinCallable(LythonKnownCallableSignatures.Glob0, UnsupportedGlobInternal),
                "glob1" => new BuiltinCallable(LythonKnownCallableSignatures.Glob1, UnsupportedGlobInternal),
                _ => null!,
            };

            return value is not null;
        }
    }

    private sealed record GlobRequest(
        string Pattern,
        string RootDirectory,
        bool PatternIsAbsolute,
        bool Recursive,
        bool IncludeHidden);

    private sealed class PyGlobIterator : PyIteratorBase
    {
        private readonly object[] _items;
        private int _index;

        public PyGlobIterator(object[] items)
        {
            _items = items;
        }

        public override bool TryMoveNext(out object value)
        {
            if (_index >= _items.Length)
            {
                value = PyNone.Instance;
                return false;
            }

            value = _items[_index++];
            return true;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<glob.iglob object>");
        }
    }

    private static object Glob(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var request = ParseGlobArguments(arguments, "glob.glob", context, span);
        return ExpandGlob(request, context, span);
    }

    private static async ValueTask<object> GlobAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var request = ParseGlobArguments(arguments, "glob.glob", context, span);
        return await ExpandGlobAsync(request, context, span).ConfigureAwait(false);
    }

    private static object IGlob(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var request = ParseGlobArguments(arguments, "glob.iglob", context, span);
        return new PyGlobIterator(ExpandGlob(request, context, span).ToArray());
    }

    private static async ValueTask<object> IGlobAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var request = ParseGlobArguments(arguments, "glob.iglob", context, span);
        var results = await ExpandGlobAsync(request, context, span).ConfigureAwait(false);
        return new PyGlobIterator(results.ToArray());
    }

    private static object Escape(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "glob.escape(pathname) expects one path-like argument.", span);
        }

        var pathname = GetPath(arguments[0], "glob.escape", span);
        var builder = new StringBuilder(pathname.Length);
        foreach (var ch in pathname)
        {
            switch (ch)
            {
                case '*':
                case '?':
                case '[':
                    builder.Append('[').Append(ch).Append(']');
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return PyString.FromString(builder.ToString(), context.MemoryGovernor, span);
    }

    private static object HasMagic(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "glob.has_magic(s) expects one path-like argument.", span);
        }

        return HasGlobMeta(GetPath(arguments[0], "glob.has_magic", span));
    }

    private static object Translate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 1 || arguments.Length > 4)
        {
            throw new LythonRuntimeException("TypeError", "glob.translate(pathname, *, recursive=False, include_hidden=False, seps=None) expects one path-like argument plus supported keyword options.", span);
        }

        var pattern = GetPath(arguments[0], "glob.translate", span);
        var recursive = ParseGlobOptionalBool(arguments, 1, false, "glob.translate", "recursive", span);
        var includeHidden = ParseGlobOptionalBool(arguments, 2, false, "glob.translate", "include_hidden", span);
        var separators = "/";
        if (arguments.Length >= 4 && arguments[3] is not PyNone)
        {
            if (!PyStringOps.TryAsString(arguments[3], out var seps))
            {
                throw new LythonRuntimeException("TypeError", "glob.translate(..., seps=...) expects a string or None.", span);
            }

            separators = seps.AsString();
            if (separators.Length == 0)
            {
                throw new LythonRuntimeException("ValueError", "glob.translate(..., seps=...) expects at least one separator character.", span);
            }
        }

        var translated = TranslateGlobPattern(pattern, recursive, includeHidden, separators);
        return PyString.FromString(translated, context.MemoryGovernor, span);
    }

    private static object UnsupportedGlobInternal(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = arguments;
        _ = context;
        throw new LythonRuntimeException("NotImplementedError", "glob.glob0() and glob.glob1() are CPython internal helpers and are not supported by Lython; use glob.glob() or glob.iglob().", span);
    }

    private static GlobRequest ParseGlobArguments(object[] arguments, string owner, ExecutionContext context, LythonSourceSpan span)
    {
        if (arguments.Length < 1 || arguments.Length > 5)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(pathname, *, root_dir=None, dir_fd=None, recursive=False, include_hidden=False) expects one path-like pattern plus supported keyword options.", span);
        }

        var pattern = GetPath(arguments[0], owner, span);
        var rootDirectory = PathOps.Normalize(context.Host.Cwd);
        if (arguments.Length >= 2 && arguments[1] is not PyNone)
        {
            var root = GetPath(arguments[1], owner, span);
            rootDirectory = root.Length == 0
                ? PathOps.Normalize(context.Host.Cwd)
                : PathOps.Normalize(root, context.Host.Cwd);
        }

        if (arguments.Length >= 3 && arguments[2] is not PyNone)
        {
            throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., dir_fd=...) is not supported by Lython; raw file descriptors are outside the host path model.", span);
        }

        var recursive = ParseGlobOptionalBool(arguments, 3, false, owner, "recursive", span);
        var includeHidden = ParseGlobOptionalBool(arguments, 4, false, owner, "include_hidden", span);
        return new GlobRequest(
            pattern,
            rootDirectory,
            pattern.StartsWith("/", StringComparison.Ordinal),
            recursive,
            includeHidden);
    }

    private static bool ParseGlobOptionalBool(object[] arguments, int index, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return defaultValue;
        }

        return arguments[index] is bool value
            ? value
            : throw new LythonRuntimeException("TypeError", $"{owner}(..., {parameterName}=...) expects {parameterName} to be a bool.", span);
    }

    private static PyList ExpandGlob(GlobRequest request, ExecutionContext context, LythonSourceSpan span)
    {
        var results = new PyList([], context.MemoryGovernor, span);
        if (request.Pattern.Length == 0)
        {
            return results;
        }

        var absolutePattern = request.PatternIsAbsolute
            ? PathOps.Normalize(request.Pattern)
            : PathOps.Normalize(request.Pattern, request.RootDirectory);
        var absoluteSegments = SplitAbsoluteGlobPattern(absolutePattern);

        foreach (var match in ExpandGlobAt("/", absoluteSegments, 0, request.Recursive, request.IncludeHidden, context, span))
        {
            AddGlobResult(results, FormatGlobResult(match, request), context, span);
        }

        return results;
    }

    private static async ValueTask<PyList> ExpandGlobAsync(GlobRequest request, ExecutionContext context, LythonSourceSpan span)
    {
        var results = new PyList([], context.MemoryGovernor, span);
        if (request.Pattern.Length == 0)
        {
            return results;
        }

        var absolutePattern = request.PatternIsAbsolute
            ? PathOps.Normalize(request.Pattern)
            : PathOps.Normalize(request.Pattern, request.RootDirectory);
        var absoluteSegments = SplitAbsoluteGlobPattern(absolutePattern);

        await ExpandGlobAtAsync("/", absoluteSegments, 0, request, context, span, results).ConfigureAwait(false);
        return results;
    }

    private static void AddGlobResult(PyList results, string path, ExecutionContext context, LythonSourceSpan span)
    {
        results.Add(PyString.FromString(path, context.MemoryGovernor, span));
        context.ObserveCollectionCount(results.Count, span);
    }

    private static string FormatGlobResult(string absoluteMatch, GlobRequest request)
    {
        if (request.PatternIsAbsolute)
        {
            return absoluteMatch;
        }

        var relative = RelPath(absoluteMatch, request.RootDirectory, request.RootDirectory);
        return request.Pattern.StartsWith("./", StringComparison.Ordinal) &&
               !relative.StartsWith("../", StringComparison.Ordinal) &&
               relative != "."
            ? "./" + relative
            : relative;
    }

    private static IEnumerable<string> ExpandGlobAt(
        string currentDirectory,
        IReadOnlyList<string> segments,
        int index,
        bool recursive,
        bool includeHidden,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (index >= segments.Count)
        {
            if (currentDirectory != "/")
            {
                yield return currentDirectory;
            }

            yield break;
        }

        var segment = segments[index];
        if (segment == "**" && recursive)
        {
            foreach (var match in ExpandGlobAt(currentDirectory, segments, index + 1, recursive, includeHidden, context, span))
            {
                yield return match;
            }

            foreach (var directory in EnumerateDirectories(currentDirectory, includeHidden, context, span))
            {
                foreach (var match in ExpandGlobAt(directory, segments, index, recursive, includeHidden, context, span))
                {
                    yield return match;
                }
            }

            yield break;
        }

        if (!HasGlobMeta(segment))
        {
            var next = currentDirectory == "/" ? "/" + segment : currentDirectory + "/" + segment;
            var stat = HostStat(next, context, span);
            if (!stat.Exists)
            {
                yield break;
            }

            if (index == segments.Count - 1)
            {
                yield return next;
                yield break;
            }

            if (!stat.IsDir)
            {
                yield break;
            }

            foreach (var match in ExpandGlobAt(next, segments, index + 1, recursive, includeHidden, context, span))
            {
                yield return match;
            }

            yield break;
        }

        foreach (var name in ListDirectoryNames(currentDirectory, context, span))
        {
            if (!ShouldConsiderGlobName(name, segment, includeHidden))
            {
                continue;
            }

            if (!FnMatchModule.MatchSimple(PyString.FromString(name), PyString.FromString(segment)))
            {
                continue;
            }

            var next = currentDirectory == "/" ? "/" + name : currentDirectory + "/" + name;
            if (index == segments.Count - 1)
            {
                yield return next;
                continue;
            }

            if (!HostStat(next, context, span).IsDir)
            {
                continue;
            }

            foreach (var match in ExpandGlobAt(next, segments, index + 1, recursive, includeHidden, context, span))
            {
                yield return match;
            }
        }
    }

    private static async ValueTask ExpandGlobAtAsync(
        string currentDirectory,
        IReadOnlyList<string> segments,
        int index,
        GlobRequest request,
        ExecutionContext context,
        LythonSourceSpan span,
        PyList results)
    {
        if (index >= segments.Count)
        {
            if (currentDirectory != "/")
            {
                AddGlobResult(results, FormatGlobResult(currentDirectory, request), context, span);
            }

            return;
        }

        var segment = segments[index];
        if (segment == "**" && request.Recursive)
        {
            await ExpandGlobAtAsync(currentDirectory, segments, index + 1, request, context, span, results).ConfigureAwait(false);
            foreach (var directory in await EnumerateDirectoriesAsync(currentDirectory, request.IncludeHidden, context, span).ConfigureAwait(false))
            {
                await ExpandGlobAtAsync(directory, segments, index, request, context, span, results).ConfigureAwait(false);
            }

            return;
        }

        if (!HasGlobMeta(segment))
        {
            var next = currentDirectory == "/" ? "/" + segment : currentDirectory + "/" + segment;
            var stat = await GlobHostStatAsync(next, context, span).ConfigureAwait(false);
            if (!stat.Exists)
            {
                return;
            }

            if (index == segments.Count - 1)
            {
                AddGlobResult(results, FormatGlobResult(next, request), context, span);
                return;
            }

            if (!stat.IsDir)
            {
                return;
            }

            await ExpandGlobAtAsync(next, segments, index + 1, request, context, span, results).ConfigureAwait(false);
            return;
        }

        foreach (var name in await ListDirectoryNamesAsync(currentDirectory, context, span).ConfigureAwait(false))
        {
            if (!ShouldConsiderGlobName(name, segment, request.IncludeHidden))
            {
                continue;
            }

            if (!FnMatchModule.MatchSimple(PyString.FromString(name), PyString.FromString(segment)))
            {
                continue;
            }

            var next = currentDirectory == "/" ? "/" + name : currentDirectory + "/" + name;
            if (index == segments.Count - 1)
            {
                AddGlobResult(results, FormatGlobResult(next, request), context, span);
                continue;
            }

            if (!(await GlobHostStatAsync(next, context, span).ConfigureAwait(false)).IsDir)
            {
                continue;
            }

            await ExpandGlobAtAsync(next, segments, index + 1, request, context, span, results).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string currentDirectory, bool includeHidden, ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var name in ListDirectoryNames(currentDirectory, context, span))
        {
            if (!includeHidden && name.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            var next = currentDirectory == "/" ? "/" + name : currentDirectory + "/" + name;
            if (!HostStat(next, context, span).IsDir)
            {
                continue;
            }

            yield return next;
        }
    }

    private static async ValueTask<List<string>> EnumerateDirectoriesAsync(string currentDirectory, bool includeHidden, ExecutionContext context, LythonSourceSpan span)
    {
        var result = new List<string>();
        foreach (var name in await ListDirectoryNamesAsync(currentDirectory, context, span).ConfigureAwait(false))
        {
            if (!includeHidden && name.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            var next = currentDirectory == "/" ? "/" + name : currentDirectory + "/" + name;
            if (!(await GlobHostStatAsync(next, context, span).ConfigureAwait(false)).IsDir)
            {
                continue;
            }

            result.Add(next);
        }

        return result;
    }

    private static IReadOnlyList<string> ListDirectoryNames(string path, ExecutionContext context, LythonSourceSpan span)
    {
        var stat = HostStat(path, context, span);
        if (!stat.IsDir)
        {
            return Array.Empty<string>();
        }

        context.RegisterHostCall(span);
        return context.HostListDir(path, span);
    }

    private static async ValueTask<IReadOnlyList<string>> ListDirectoryNamesAsync(string path, ExecutionContext context, LythonSourceSpan span)
    {
        var stat = await GlobHostStatAsync(path, context, span).ConfigureAwait(false);
        if (!stat.IsDir)
        {
            return Array.Empty<string>();
        }

        context.RegisterHostCall(span);
        return await context.HostListDirAsync(path, span).ConfigureAwait(false);
    }

    private static async ValueTask<LythonPathStat> GlobHostStatAsync(string path, ExecutionContext context, LythonSourceSpan span)
    {
        context.RegisterHostCall(span);
        return await context.HostStatAsync(path, span).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> SplitAbsoluteGlobPattern(string absolutePattern)
    {
        if (absolutePattern == "/")
        {
            return Array.Empty<string>();
        }

        return absolutePattern[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool HasGlobMeta(string text)
        => text.IndexOfAny(['*', '?', '[']) >= 0;

    private static bool ShouldConsiderGlobName(string name, string patternSegment, bool includeHidden)
    {
        return includeHidden ||
               !name.StartsWith(".", StringComparison.Ordinal) ||
               patternSegment.StartsWith(".", StringComparison.Ordinal);
    }

    private static string TranslateGlobPattern(string pattern, bool recursive, bool includeHidden, string separators)
    {
        var segments = SplitGlobPattern(pattern, separators);
        var builder = new StringBuilder();
        builder.Append('^');
        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                AppendSeparatorRegex(builder, separators);
            }

            var segment = segments[i];
            if (recursive && segment == "**")
            {
                if (i == segments.Count - 1)
                {
                    builder.Append(".*");
                }
                else
                {
                    builder.Append("(?:.*");
                    AppendSeparatorRegex(builder, separators);
                    builder.Append(")?");
                }

                continue;
            }

            AppendGlobSegmentRegex(builder, segment, includeHidden, separators);
        }

        builder.Append('$');
        return builder.ToString();
    }

    private static List<string> SplitGlobPattern(string pattern, string separators)
    {
        var segments = new List<string>();
        var start = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (separators.IndexOf(pattern[i]) >= 0)
            {
                segments.Add(pattern[start..i]);
                start = i + 1;
            }
        }

        segments.Add(pattern[start..]);
        return segments;
    }

    private static void AppendGlobSegmentRegex(StringBuilder builder, string segment, bool includeHidden, string separators)
    {
        if (!includeHidden && segment.Length != 0 && !segment.StartsWith(".", StringComparison.Ordinal))
        {
            builder.Append("(?!\\.)");
        }

        for (var i = 0; i < segment.Length; i++)
        {
            var ch = segment[i];
            switch (ch)
            {
                case '*':
                    AppendNonSeparatorClass(builder, separators);
                    builder.Append('*');
                    break;
                case '?':
                    AppendNonSeparatorClass(builder, separators);
                    break;
                case '[':
                    i = AppendTranslatedCharacterClass(builder, segment, i);
                    break;
                default:
                    AppendEscapedRegexLiteral(builder, ch);
                    break;
            }
        }
    }

    private static int AppendTranslatedCharacterClass(StringBuilder builder, string segment, int openBracket)
    {
        var closeBracket = segment.IndexOf(']', openBracket + 1);
        if (closeBracket < 0)
        {
            builder.Append("\\[");
            return openBracket;
        }

        builder.Append('[');
        var index = openBracket + 1;
        if (index < closeBracket && segment[index] is '!' or '^')
        {
            builder.Append('^');
            index++;
        }

        if (index < closeBracket && segment[index] == ']')
        {
            builder.Append("\\]");
            index++;
        }

        for (; index < closeBracket; index++)
        {
            var ch = segment[index];
            if (ch is '\\' or '[')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        builder.Append(']');
        return closeBracket;
    }

    private static void AppendSeparatorRegex(StringBuilder builder, string separators)
    {
        if (separators.Length == 1)
        {
            AppendEscapedRegexLiteral(builder, separators[0]);
            return;
        }

        builder.Append('[');
        AppendEscapedCharacterClassChars(builder, separators);
        builder.Append(']');
    }

    private static void AppendNonSeparatorClass(StringBuilder builder, string separators)
    {
        builder.Append("[^");
        AppendEscapedCharacterClassChars(builder, separators);
        builder.Append(']');
    }

    private static void AppendEscapedCharacterClassChars(StringBuilder builder, string value)
    {
        foreach (var ch in value)
        {
            if (ch is '\\' or ']' or '-' or '^')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }
    }

    private static void AppendEscapedRegexLiteral(StringBuilder builder, char ch)
    {
        if (".^$*+?{}[]\\|()".IndexOf(ch) >= 0)
        {
            builder.Append('\\');
        }

        builder.Append(ch);
    }
}
