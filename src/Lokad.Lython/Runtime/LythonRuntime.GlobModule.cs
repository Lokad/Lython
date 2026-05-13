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
                _ => null!,
            };

            return value is not null;
        }
    }

    private static object Glob(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (pattern, recursive) = ParseGlobArguments(arguments, "glob.glob", span);
        return ExpandGlob(pattern, recursive, context, span);
    }

    private static async ValueTask<object> GlobAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (pattern, recursive) = ParseGlobArguments(arguments, "glob.glob", span);
        return await ExpandGlobAsync(pattern, recursive, context, span).ConfigureAwait(false);
    }

    private static object IGlob(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (pattern, recursive) = ParseGlobArguments(arguments, "glob.iglob", span);
        return ExpandGlob(pattern, recursive, context, span);
    }

    private static async ValueTask<object> IGlobAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (pattern, recursive) = ParseGlobArguments(arguments, "glob.iglob", span);
        return await ExpandGlobAsync(pattern, recursive, context, span).ConfigureAwait(false);
    }

    private static object Escape(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pathname))
        {
            throw new LythonRuntimeException("TypeError", "glob.escape(pathname) expects one string argument.", span);
        }

        var builder = new StringBuilder(pathname.Length);
        foreach (var ch in pathname.AsString())
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

        return PyString.FromString(builder.ToString());
    }

    private static (string Pattern, bool Recursive) ParseGlobArguments(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var pattern))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(pathname[, recursive]) expects a string pattern plus an optional bool recursive flag.", span);
        }

        var recursive = arguments.Length == 2
            ? arguments[1] switch
            {
                bool value => value,
                _ => throw new LythonRuntimeException("TypeError", $"{owner}(pathname, recursive) expects recursive to be a bool.", span)
            }
            : false;

        return (pattern.AsString(), recursive);
    }

    private static PyList ExpandGlob(string pattern, bool recursive, ExecutionContext context, LythonSourceSpan span)
    {
        var results = new PyList([], context.MemoryGovernor, span);
        if (pattern.Length == 0)
        {
            return results;
        }

        var absolutePattern = pattern.StartsWith("/", StringComparison.Ordinal)
            ? PathOps.Normalize(pattern)
            : PathOps.Normalize(pattern, context.Host.Cwd);
        var absoluteSegments = SplitAbsoluteGlobPattern(absolutePattern);

        foreach (var match in ExpandGlobAt("/", absoluteSegments, 0, recursive, context, span))
        {
            results.Add(new PyPath(PyString.FromString(match)));
            context.ObserveCollectionCount(results.Count, span);
        }

        return results;
    }

    private static async ValueTask<PyList> ExpandGlobAsync(string pattern, bool recursive, ExecutionContext context, LythonSourceSpan span)
    {
        var results = new PyList([], context.MemoryGovernor, span);
        if (pattern.Length == 0)
        {
            return results;
        }

        var absolutePattern = pattern.StartsWith("/", StringComparison.Ordinal)
            ? PathOps.Normalize(pattern)
            : PathOps.Normalize(pattern, context.Host.Cwd);
        var absoluteSegments = SplitAbsoluteGlobPattern(absolutePattern);

        await ExpandGlobAtAsync("/", absoluteSegments, 0, recursive, context, span, results).ConfigureAwait(false);
        return results;
    }

    private static IEnumerable<string> ExpandGlobAt(
        string currentDirectory,
        IReadOnlyList<string> segments,
        int index,
        bool recursive,
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
            foreach (var match in ExpandGlobAt(currentDirectory, segments, index + 1, recursive, context, span))
            {
                yield return match;
            }

            foreach (var directory in EnumerateDirectories(currentDirectory, context, span))
            {
                foreach (var match in ExpandGlobAt(directory, segments, index, recursive, context, span))
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

            foreach (var match in ExpandGlobAt(next, segments, index + 1, recursive, context, span))
            {
                yield return match;
            }

            yield break;
        }

        foreach (var name in ListDirectoryNames(currentDirectory, context, span))
        {
            if (!ShouldConsiderGlobName(name, segment))
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

            foreach (var match in ExpandGlobAt(next, segments, index + 1, recursive, context, span))
            {
                yield return match;
            }
        }
    }

    private static async ValueTask ExpandGlobAtAsync(
        string currentDirectory,
        IReadOnlyList<string> segments,
        int index,
        bool recursive,
        ExecutionContext context,
        LythonSourceSpan span,
        PyList results)
    {
        if (index >= segments.Count)
        {
            if (currentDirectory != "/")
            {
                results.Add(new PyPath(PyString.FromString(currentDirectory)));
                context.ObserveCollectionCount(results.Count, span);
            }

            return;
        }

        var segment = segments[index];
        if (segment == "**" && recursive)
        {
            await ExpandGlobAtAsync(currentDirectory, segments, index + 1, recursive, context, span, results).ConfigureAwait(false);
            foreach (var directory in await EnumerateDirectoriesAsync(currentDirectory, context, span).ConfigureAwait(false))
            {
                await ExpandGlobAtAsync(directory, segments, index, recursive, context, span, results).ConfigureAwait(false);
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
                results.Add(new PyPath(PyString.FromString(next)));
                context.ObserveCollectionCount(results.Count, span);
                return;
            }

            if (!stat.IsDir)
            {
                return;
            }

            await ExpandGlobAtAsync(next, segments, index + 1, recursive, context, span, results).ConfigureAwait(false);
            return;
        }

        foreach (var name in await ListDirectoryNamesAsync(currentDirectory, context, span).ConfigureAwait(false))
        {
            if (!ShouldConsiderGlobName(name, segment))
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
                results.Add(new PyPath(PyString.FromString(next)));
                context.ObserveCollectionCount(results.Count, span);
                continue;
            }

            if (!(await GlobHostStatAsync(next, context, span).ConfigureAwait(false)).IsDir)
            {
                continue;
            }

            await ExpandGlobAtAsync(next, segments, index + 1, recursive, context, span, results).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string currentDirectory, ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var name in ListDirectoryNames(currentDirectory, context, span))
        {
            if (name.StartsWith(".", StringComparison.Ordinal))
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

    private static async ValueTask<List<string>> EnumerateDirectoriesAsync(string currentDirectory, ExecutionContext context, LythonSourceSpan span)
    {
        var result = new List<string>();
        foreach (var name in await ListDirectoryNamesAsync(currentDirectory, context, span).ConfigureAwait(false))
        {
            if (name.StartsWith(".", StringComparison.Ordinal))
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

    private static bool HasGlobMeta(string segment)
        => segment.IndexOfAny(['*', '?', '[']) >= 0;

    private static bool ShouldConsiderGlobName(string name, string patternSegment)
    {
        return !name.StartsWith(".", StringComparison.Ordinal) ||
               patternSegment.StartsWith(".", StringComparison.Ordinal);
    }
}
