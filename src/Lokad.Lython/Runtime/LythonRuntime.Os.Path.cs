using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object OsPathJoin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "os.path.join(...) expects one or more string arguments.", span);
        }

        var first = GetPath(arguments[0], "os.path.join", span);
        using var reservation = context.MemoryGovernor.ReserveTemporary(64L + (4L * first.Length), span);
        var current = new StringBuilder(first);
        for (var i = 1; i < arguments.Length; i++)
        {
            var next = GetPath(arguments[i], "os.path.join", span);
            reservation.Grow(4L + (4L * next.Length), span);
            if ((next.Length > 0 && next[0] == '/') || current.Length == 0)
            {
                // POSIX join discards the accumulated prefix at an absolute
                // component; Clear retains capacity without copying prefixes.
                current.Clear();
                current.Append(next);
            }
            else
            {
                if (current[^1] != '/')
                {
                    current.Append('/');
                }

                current.Append(next);
            }
        }

        return CreateString(current.ToString(), context, span);
    }

    private static object OsPathSplit(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.split", span);
        var (head, tail) = SplitPath(path);
        return new PyTuple([PyString.FromString(head), PyString.FromString(tail)], context.MemoryGovernor, span);
    }

    private static object OsPathSplitExt(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.splitext", span);
        var (root, extension) = SplitExt(path);
        return new PyTuple([PyString.FromString(root), PyString.FromString(extension)], context.MemoryGovernor, span);
    }

    private static object OsPathBaseName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var path = GetSinglePath(arguments, "os.path.basename", span);
        return PyString.FromString(SplitPath(path).Tail);
    }

    private static object OsPathDirName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var path = GetSinglePath(arguments, "os.path.dirname", span);
        return PyString.FromString(SplitPath(path).Head);
    }

    private static object OsPathIsAbs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var path = GetSinglePath(arguments, "os.path.isabs", span);
        return path.StartsWith("/", StringComparison.Ordinal);
    }

    private static object OsPathNormPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var path = GetSinglePath(arguments, "os.path.normpath", span);
        return PyString.FromString(path.Length == 0 ? "." : PathOps.Normalize(path));
    }

    private static object OsPathNormCase(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var path = GetSinglePath(arguments, "os.path.normcase", span);
        return PyString.FromString(path);
    }

    private static object OsPathAbsPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.abspath", span);
        return PyString.FromString(PathOps.Normalize(path, context.Host.Cwd));
    }

    private static object OsPathRelPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "os.path.relpath(path[, start]) expects one or two string arguments.", span);
        }

        var path = GetPath(arguments[0], "os.path.relpath", span);
        var start = arguments.Length == 2 ? GetPath(arguments[1], "os.path.relpath", span) : context.Host.Cwd;
        return PyString.FromString(RelPath(path, start, context.Host.Cwd));
    }

    private static object OsPathCommonPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.path.commonpath(paths) expects one iterable argument.", span);
        }

        if (PyStringOps.TryAsString(arguments[0], out _))
        {
            throw new LythonRuntimeException("TypeError", "os.path.commonpath(paths) expects an iterable of strings, not a single string.", span);
        }

        var sequence = ToSequence(arguments[0], span);
        var paths = new List<string>();
        foreach (var item in sequence)
        {
            paths.Add(GetPath(item, "os.path.commonpath", span));
        }

        if (paths.Count == 0)
        {
            throw new LythonRuntimeException("ValueError", "os.path.commonpath() arg is an empty sequence", span);
        }

        return PyString.FromString(CommonPath(paths));
    }

    private static object OsPathCommonPrefix(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.path.commonprefix(list) expects one iterable argument.", span);
        }

        var paths = new List<string>();
        foreach (var item in ToSequence(arguments[0], span))
        {
            paths.Add(GetPath(item, "os.path.commonprefix", span));
        }

        if (paths.Count == 0)
        {
            return PyString.Empty;
        }

        var prefix = paths[0];
        for (var i = 1; i < paths.Count && prefix.Length != 0; i++)
        {
            var candidate = paths[i];
            var length = Math.Min(prefix.Length, candidate.Length);
            var index = 0;
            while (index < length && prefix[index] == candidate[index])
            {
                index++;
            }

            prefix = prefix[..index];
        }

        return PyString.FromString(prefix);
    }

    private static object OsPathExists(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.exists", span);
        return HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span).Exists;
    }

    private static async ValueTask<object> OsPathExistsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.exists", span);
        return (await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false)).Exists;
    }

    private static object OsPathIsFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.isfile", span);
        return HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span).IsFile;
    }

    private static async ValueTask<object> OsPathIsFileAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.isfile", span);
        return (await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false)).IsFile;
    }

    private static object OsPathIsDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.isdir", span);
        return HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span).IsDir;
    }

    private static async ValueTask<object> OsPathIsDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.isdir", span);
        return (await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false)).IsDir;
    }

    private static object OsPathGetSize(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.getsize", span);
        return HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span).Size;
    }

    private static async ValueTask<object> OsPathGetSizeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.getsize", span);
        return (await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false)).Size;
    }

    private static object OsPathGetMTime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.getmtime", span);
        return PathModifiedAtSeconds(HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span).ModifiedAtTimestamp, span);
    }

    private static async ValueTask<object> OsPathGetMTimeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.getmtime", span);
        var stat = await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false);
        return PathModifiedAtSeconds(stat.ModifiedAtTimestamp, span);
    }

    private static object OsPathSameFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.path.samefile(path1, path2) expects two path-like arguments.", span);
        }

        var first = PathOps.Normalize(GetPath(arguments[0], "os.path.samefile", span), context.Host.Cwd);
        var second = PathOps.Normalize(GetPath(arguments[1], "os.path.samefile", span), context.Host.Cwd);
        var firstStat = HostStat(first, context, span);
        var secondStat = HostStat(second, context, span);
        if (!firstStat.Exists || !secondStat.Exists)
        {
            throw new LythonRuntimeException("RuntimeError", "os.path.samefile() expects both paths to exist.", span);
        }

        return string.Equals(first, second, StringComparison.Ordinal);
    }

    private static object OsPathRealPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.realpath", span);
        return PyString.FromString(PathOps.Normalize(path, context.Host.Cwd));
    }

    private static object OsPathExpandVars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.expandvars", span);
        return PyString.FromString(ExpandVars(path, context.State.Environment), context.MemoryGovernor, span);
    }

    private static object OsPathSplitDrive(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.splitdrive", span);
        return new PyTuple([PyString.Empty, PyString.FromString(path)], context.MemoryGovernor, span);
    }

    private static object OsPathSplitRoot(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.splitroot", span);
        string root;
        string tail;
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            root = string.Empty;
            tail = path;
        }
        else if (path.Length >= 2 &&
                 path[1] == '/' &&
                 (path.Length == 2 || path[2] != '/'))
        {
            root = "//";
            tail = path[2..];
        }
        else
        {
            root = "/";
            tail = path[1..];
        }

        return new PyTuple(
            [PyString.Empty, PyString.FromString(root), PyString.FromString(tail)],
            context.MemoryGovernor,
            span);
    }

    private static object OsPathIsMount(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.ismount", span);
        return PathOps.Normalize(path, context.Host.Cwd) == "/";
    }

    private static string GetSinglePath(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(path) expects one string argument.", span);
        }

        return GetPath(arguments[0], owner, span);
    }

    private static string GetPathOrDefault(object[] arguments, string owner, LythonSourceSpan span, string defaultPath)
    {
        return arguments.Length switch
        {
            0 => defaultPath,
            1 => GetPath(arguments[0], owner, span),
            _ => throw new LythonRuntimeException("TypeError", $"{owner}([path]) expects zero or one string argument.", span)
        };
    }

    private static string GetPath(object value, string owner, LythonSourceSpan span)
    {
        if (value is PyPath pyPath)
        {
            return pyPath.Value.AsString();
        }

        if (value is PyDirEntryObject dirEntry)
        {
            return dirEntry.Path;
        }

        if (!PyStringOps.TryAsString(value, out var path))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(...) expects string or Path path arguments.", span);
        }

        return path.AsString();
    }

    private static LythonPathStat HostStat(string path, ExecutionContext context, LythonSourceSpan span)
    {
        context.RegisterHostCall(span);
        return context.HostStat(path, span);
    }

    private static async ValueTask<LythonPathStat> OsHostStatAsync(string path, ExecutionContext context, LythonSourceSpan span)
    {
        context.RegisterHostCall(span);
        return await context.HostStatAsync(path, span).ConfigureAwait(false);
    }

    private static bool IsHostRuntimeFailure(LythonRuntimeException exception)
        => string.Equals(exception.ExceptionType, "RuntimeError", StringComparison.Ordinal) &&
           exception.InnerException is not null &&
           exception.Message.StartsWith("Host ", StringComparison.Ordinal);

    private static double PathModifiedAtSeconds(DateTimeOffset? modifiedAt, LythonSourceSpan? span)
    {
        if (modifiedAt is not { } timestamp)
        {
            throw new LythonRuntimeException("ValueError", "host stat has no modification timestamp.", span);
        }

        return (timestamp - DateTimeOffset.UnixEpoch).TotalSeconds;
    }

    private static IEnumerable<string> EnumerateMissingDirectories(string path)
    {
        var normalized = PathOps.Normalize(path);
        if (normalized == "/")
        {
            yield break;
        }

        var current = normalized.StartsWith("/", StringComparison.Ordinal) ? "/" : ".";
        foreach (var part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current switch
            {
                "/" => "/" + part,
                "." => part,
                _ => current + "/" + part
            };

            yield return current;
        }
    }

    private static string RelPath(string path, string start, string cwd)
    {
        var absolutePath = PathOps.Normalize(path, cwd);
        var absoluteStart = PathOps.Normalize(start, cwd);
        var pathParts = SplitNormalizedPath(absolutePath);
        var startParts = SplitNormalizedPath(absoluteStart);
        var commonLength = 0;

        while (commonLength < pathParts.Length &&
               commonLength < startParts.Length &&
               string.Equals(pathParts[commonLength], startParts[commonLength], StringComparison.Ordinal))
        {
            commonLength++;
        }

        var parentCount = startParts.Length - commonLength;
        var tailCount = pathParts.Length - commonLength;
        var parts = new string[parentCount + tailCount];
        for (var i = 0; i < parentCount; i++)
        {
            parts[i] = "..";
        }

        for (var i = 0; i < tailCount; i++)
        {
            parts[parentCount + i] = pathParts[commonLength + i];
        }

        return parts.Length == 0 ? "." : string.Join("/", parts);
    }

    private static string CommonPath(IReadOnlyList<string> paths)
    {
        var normalized = new string[paths.Count];
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            normalized[i] = path.Length == 0 ? "." : PathOps.Normalize(path);
        }

        var firstIsAbsolute = normalized[0].StartsWith("/", StringComparison.Ordinal);
        for (var i = 1; i < normalized.Length; i++)
        {
            if (normalized[i].StartsWith("/", StringComparison.Ordinal) != firstIsAbsolute)
            {
                throw new LythonRuntimeException("ValueError", "Can't mix absolute and relative paths in os.path.commonpath().", null);
            }
        }

        var split = new string[normalized.Length][];
        var commonLength = int.MaxValue;
        for (var i = 0; i < normalized.Length; i++)
        {
            split[i] = SplitNormalizedPath(normalized[i]);
            commonLength = Math.Min(commonLength, split[i].Length);
        }

        var index = 0;
        while (index < commonLength)
        {
            var segment = split[0][index];
            var allMatch = true;
            for (var i = 1; i < split.Length; i++)
            {
                if (!string.Equals(split[i][index], segment, StringComparison.Ordinal))
                {
                    allMatch = false;
                    break;
                }
            }

            if (!allMatch)
            {
                break;
            }

            index++;
        }

        if (index == 0)
        {
            return firstIsAbsolute ? "/" : string.Empty;
        }

        var common = string.Join("/", split[0], 0, index);
        return firstIsAbsolute ? "/" + common : common;
    }

    private static string[] SplitNormalizedPath(string normalized)
    {
        if (normalized == "/" || normalized == ".")
        {
            return [];
        }

        return normalized.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static (string Head, string Tail) SplitPath(string path)
    {
        if (path.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var slash = path.LastIndexOf('/') + 1;
        if (slash < 0)
        {
            return (string.Empty, path);
        }

        var head = path[..slash];
        var tail = path[slash..];
        if (head.Length != 0 && head.Any(character => character != '/'))
        {
            head = head.TrimEnd('/');
        }

        return (head, tail);
    }

    private static (string Root, string Extension) SplitExt(string path)
    {
        if (path.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var slash = path.LastIndexOf('/');
        var segmentStart = slash + 1;
        var dot = path.LastIndexOf('.');
        if (dot <= segmentStart)
        {
            return (path, string.Empty);
        }

        return (path[..dot], path[dot..]);
    }

    private static string ParentDirectory(string path)
    {
        if (path == "/")
        {
            return "/";
        }

        var trimmed = path.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash <= 0 ? "/" : trimmed[..slash];
    }

    private static string JoinChild(string directory, string name) => directory == "/" ? "/" + name : directory + "/" + name;

    private static ICallable UnsupportedOsCallable(string name, string message)
        => new UnsupportedOsCallableObject(name, message);

}
