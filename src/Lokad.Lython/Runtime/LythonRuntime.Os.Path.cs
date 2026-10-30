using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private readonly record struct PathSplit(string Head, string Tail);

    private readonly record struct ExtensionSplit(string Root, string Extension);

    private static object OsPathJoin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathJoinCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathJoinAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathJoinCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathJoinCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                var part = asynchronous ? await CoercePathLikeAsync(arguments[i], context, span, "os.path.join").ConfigureAwait(false)
                    : CoercePathLike(arguments[i], context, span, "os.path.join");
                var bytes = part.Utf8Bytes;
                if (!bytes.IsEmpty && bytes.Span[0] == (byte)'/') builder.Truncate(0);
                else if (builder.Length != 0 && builder.WrittenSpan[^1] != (byte)'/') builder.Append((byte)'/');
                PathOps.Append(builder, bytes.Span, context, span);
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static object OsPathSplit(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathSplitAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathSplitCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.split", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.split", span, context, pathScratch));
        var (head, tail) = SplitPath(path);
        return PyTuple.FromOwnedArray([PyString.FromString(head, context.MemoryGovernor, span), PyString.FromString(tail, context.MemoryGovernor, span)], context.MemoryGovernor, span);
    }

    private static object OsPathSplitExt(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitExtCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathSplitExtAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitExtCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathSplitExtCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.splitext", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.splitext", span, context, pathScratch));
        var (root, extension) = SplitExt(path);
        return PyTuple.FromOwnedArray([PyString.FromString(root, context.MemoryGovernor, span), PyString.FromString(extension, context.MemoryGovernor, span)], context.MemoryGovernor, span);
    }

    private static object OsPathBaseName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathBaseNameCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathBaseNameAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathBaseNameCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathBaseNameCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.basename", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.basename", span, context, pathScratch));
        return PyString.FromString(SplitPath(path).Tail, context.MemoryGovernor, span);
    }

    private static object OsPathDirName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathDirNameCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathDirNameAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathDirNameCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathDirNameCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.dirname", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.dirname", span, context, pathScratch));
        return PyString.FromString(SplitPath(path).Head, context.MemoryGovernor, span);
    }

    private static object OsPathIsAbs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathIsAbsCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathIsAbsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathIsAbsCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathIsAbsCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        _ = context;
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.isabs", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.isabs", span, context, pathScratch));
        return path.StartsWith("/", StringComparison.Ordinal);
    }

    private static object OsPathNormPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathNormPathCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathNormPathAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathNormPathCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathNormPathCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.normpath", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.normpath", span, context, pathScratch));
        return PyString.FromString(path.Length == 0 ? "." : PathOps.Normalize(path), context.MemoryGovernor, span);
    }

    private static object OsPathNormCase(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathNormCaseCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathNormCaseAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathNormCaseCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathNormCaseCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.normcase", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.normcase", span, context, pathScratch));
        return PyString.FromString(path, context.MemoryGovernor, span);
    }

    private static object OsPathAbsPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathAbsPathCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathAbsPathAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathAbsPathCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathAbsPathCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.abspath", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.abspath", span, context, pathScratch));
        return PyString.FromString(PathOps.Normalize(path, context.Host.Cwd), context.MemoryGovernor, span);
    }

    private static object OsPathRelPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathRelPathCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathRelPathAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathRelPathCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathRelPathCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "os.path.relpath(path[, start]) expects one or two string arguments.", span);
        }

        var path = (asynchronous ? await GetPathAsync(arguments[0], "os.path.relpath", span, context, pathScratch).ConfigureAwait(false) : GetPath(arguments[0], "os.path.relpath", span, context, pathScratch));
        var start = arguments.Length == 2 ? (asynchronous ? await GetPathAsync(arguments[1], "os.path.relpath", span, context, pathScratch).ConfigureAwait(false) : GetPath(arguments[1], "os.path.relpath", span, context, pathScratch)) : context.Host.Cwd;
        return PyString.FromString(RelPath(path, start, context.Host.Cwd), context.MemoryGovernor, span);
    }

    private static object OsPathCommonPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathCommonPathCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathCommonPathAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathCommonPathCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathCommonPathCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        PyString? prefix = null;
        if (asynchronous)
        {
            await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
                prefix = CommonPathPart(prefix, await CoercePathLikeAsync(item, context, span, "os.path.commonpath").ConfigureAwait(false), context, span);
        }
        else foreach (var item in ToSequence(arguments[0], span, context))
            prefix = CommonPathPart(prefix, CoercePathLike(item, context, span, "os.path.commonpath"), context, span);
        return prefix ?? throw RuntimeErrors.Value("os.path.commonpath() arg is an empty sequence", span);
    }

    private static PyString CommonPathPart(PyString? prefix, PyString value, ExecutionContext context, LythonSourceSpan span)
    {
        value = PathOps.NormalizeLexical(value, context, span);
        if (value.Utf8Bytes.Span.SequenceEqual("."u8)) value = PyString.Empty;
        // os.path.commonpath has one POSIX root even where pathlib preserves //.
        if (PathOps.RootLength(value.Utf8Bytes.Span) == 2)
            value = PyString.CopyFromUtf8(value.Utf8Bytes.Span[1..], context.MemoryGovernor, span);
        if (prefix is null) return value;
        var left = prefix.Utf8Bytes.Span; var right = value.Utf8Bytes.Span;
        var root = PathOps.RootLength(left);
        if (root != PathOps.RootLength(right)) throw RuntimeErrors.Value("Can't mix absolute and relative paths", span);
        var end = root; var start = root;
        while (start < left.Length && start < right.Length)
        {
            context.CheckExecution(span);
            var a = left[start..].IndexOf((byte)'/'); var b = right[start..].IndexOf((byte)'/');
            var ae = a < 0 ? left.Length : start + a; var be = b < 0 ? right.Length : start + b;
            if (!left[start..ae].SequenceEqual(right[start..be])) break;
            end = ae; start = ae + 1;
        }
        return end == left.Length ? prefix : PyString.CopyFromUtf8(left[..end], context.MemoryGovernor, span);
    }

    private static object OsPathCommonPrefix(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathCommonPrefixCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathCommonPrefixAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathCommonPrefixCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathCommonPrefixCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        PyString? first = null; var length = 0;
        if (asynchronous)
        {
            await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
                Accept(await CoercePathLikeAsync(item, context, span, "os.path.commonprefix").ConfigureAwait(false));
        }
        else foreach (var item in ToSequence(arguments[0], span, context)) Accept(CoercePathLike(item, context, span, "os.path.commonprefix"));
        if (first is null || length == 0) return PyString.Empty;
        return length == first.Utf8Bytes.Length ? first : PyString.CopyFromUtf8(first.Utf8Bytes.Span[..length], context.MemoryGovernor, span);
        void Accept(PyString candidate)
        {
            context.CheckExecution(span);
            if (first is null) { first = candidate; length = candidate.Utf8Bytes.Length; return; }
            var left = first.Utf8Bytes.Span; var right = candidate.Utf8Bytes.Span;
            var limit = Math.Min(length, right.Length); var count = 0;
            while (count < limit && left[count] == right[count]) { if ((count & 4095) == 0) context.CheckExecution(span); count++; }
            while (count > 0 && count < left.Length && (left[count] & 0xc0) == 0x80) count--;
            length = count;
        }
    }

    private static object OsPathExists(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = GetSinglePath(arguments, "os.path.exists", span, context, pathScratch);
        return HostPathKind(path, context, span, osPath: true) != LythonPathKind.Missing;
    }

    private static async ValueTask<object> OsPathExistsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = await GetSinglePathAsync(arguments, "os.path.exists", span, context, pathScratch).ConfigureAwait(false);
        return await HostPathKindAsync(path, context, span, osPath: true).ConfigureAwait(false) != LythonPathKind.Missing;
    }

    private static object OsPathIsFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = GetSinglePath(arguments, "os.path.isfile", span, context, pathScratch);
        return HostPathKind(path, context, span, osPath: true) == LythonPathKind.File;
    }

    private static async ValueTask<object> OsPathIsFileAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = await GetSinglePathAsync(arguments, "os.path.isfile", span, context, pathScratch).ConfigureAwait(false);
        return await HostPathKindAsync(path, context, span, osPath: true).ConfigureAwait(false) == LythonPathKind.File;
    }

    private static object OsPathIsDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = GetSinglePath(arguments, "os.path.isdir", span, context, pathScratch);
        return HostPathKind(path, context, span, osPath: true) == LythonPathKind.Directory;
    }

    private static async ValueTask<object> OsPathIsDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = await GetSinglePathAsync(arguments, "os.path.isdir", span, context, pathScratch).ConfigureAwait(false);
        return await HostPathKindAsync(path, context, span, osPath: true).ConfigureAwait(false) == LythonPathKind.Directory;
    }

    private static object OsPathGetSize(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = GetSinglePath(arguments, "os.path.getsize", span, context, pathScratch);
        return RequireExistingPath(HostStat(path, context, span), span).Size;
    }

    private static async ValueTask<object> OsPathGetSizeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = await GetSinglePathAsync(arguments, "os.path.getsize", span, context, pathScratch).ConfigureAwait(false);
        return RequireExistingPath(await HostStatAsync(path, context, span).ConfigureAwait(false), span).Size;
    }

    private static object OsPathGetMTime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = GetSinglePath(arguments, "os.path.getmtime", span, context, pathScratch);
        return PathModifiedAtSeconds(RequireExistingPath(HostStat(path, context, span), span).ModifiedAtTimestamp, span);
    }

    private static async ValueTask<object> OsPathGetMTimeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = await GetSinglePathAsync(arguments, "os.path.getmtime", span, context, pathScratch).ConfigureAwait(false);
        var stat = await HostStatAsync(path, context, span).ConfigureAwait(false);
        return PathModifiedAtSeconds(RequireExistingPath(stat, span).ModifiedAtTimestamp, span);
    }

    private static object OsPathSameFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSameFileCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathSameFileAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSameFileCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathSameFileCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.path.samefile(path1, path2) expects two path-like arguments.", span);
        }

        var first = (asynchronous ? await GetPathAsync(arguments[0], "os.path.samefile", span, context, pathScratch).ConfigureAwait(false) : GetPath(arguments[0], "os.path.samefile", span, context, pathScratch));
        var second = (asynchronous ? await GetPathAsync(arguments[1], "os.path.samefile", span, context, pathScratch).ConfigureAwait(false) : GetPath(arguments[1], "os.path.samefile", span, context, pathScratch));
        var firstStat = (asynchronous ? await HostStatAsync(first, context, span).ConfigureAwait(false) : HostStat(first, context, span));
        var secondStat = (asynchronous ? await HostStatAsync(second, context, span).ConfigureAwait(false) : HostStat(second, context, span));
        if (!firstStat.Exists || !secondStat.Exists)
        {
            throw new LythonRuntimeException("FileNotFoundError", "os.path.samefile() expects both paths to exist.", span);
        }

        return string.Equals(PathOps.Normalize(first, context.Host.Cwd), PathOps.Normalize(second, context.Host.Cwd), StringComparison.Ordinal);
    }

    private static object OsPathRealPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathRealPathCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathRealPathAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathRealPathCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathRealPathCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.realpath", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.realpath", span, context, pathScratch));
        return PyString.FromString(PathOps.Normalize(path, context.Host.Cwd), context.MemoryGovernor, span);
    }

    private static object OsPathExpandVars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathExpandVarsCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathExpandVarsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathExpandVarsCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathExpandVarsCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.expandvars", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.expandvars", span, context, pathScratch));
        return PyString.FromString(ExpandVars(path, context.State.Environment), context.MemoryGovernor, span);
    }

    private static object OsPathSplitDrive(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitDriveCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathSplitDriveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitDriveCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathSplitDriveCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.splitdrive", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.splitdrive", span, context, pathScratch));
        return PyTuple.FromOwnedArray([PyString.Empty, PyString.FromString(path, context.MemoryGovernor, span)], context.MemoryGovernor, span);
    }

    private static object OsPathSplitRoot(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitRootCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathSplitRootAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathSplitRootCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathSplitRootCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.splitroot", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.splitroot", span, context, pathScratch));
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
            [PyString.Empty, PyString.FromString(root, context.MemoryGovernor, span), PyString.FromString(tail, context.MemoryGovernor, span)],
            context.MemoryGovernor,
            span);
    }

    private static object OsPathIsMount(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathIsMountCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsPathIsMountAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsPathIsMountCore(arguments, span, context, true);
    private static async ValueTask<object> OsPathIsMountCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * context.Host.Cwd.Length), span);
        var path = (asynchronous ? await GetSinglePathAsync(arguments, "os.path.ismount", span, context, pathScratch).ConfigureAwait(false) : GetSinglePath(arguments, "os.path.ismount", span, context, pathScratch));
        return PathOps.Normalize(path, context.Host.Cwd) == "/";
    }

    private static string GetSinglePath(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        => GetPath(arguments[0], owner, span, context, scratch);
    private static ValueTask<string> GetSinglePathAsync(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        => GetPathAsync(arguments[0], owner, span, context, scratch);
    private static string GetPathOrDefault(object[] arguments, string owner, LythonSourceSpan span, string defaultPath, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
    {
        if (arguments.Length != 0) return GetPath(arguments[0], owner, span, context, scratch);
        scratch.Grow(checked(256L + 32L * defaultPath.Length), span); return defaultPath;
    }
    private static async ValueTask<string> GetPathOrDefaultAsync(object[] arguments, string owner, LythonSourceSpan span, string defaultPath, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
    {
        if (arguments.Length != 0) return await GetPathAsync(arguments[0], owner, span, context, scratch).ConfigureAwait(false);
        scratch.Grow(checked(256L + 32L * defaultPath.Length), span); return defaultPath;
    }
    private static string GetPath(object value, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        => DecodePath(CoercePathLike(value, context, span, owner), context, span, scratch);
    private static async ValueTask<string> GetPathAsync(object value, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        => DecodePath(await CoercePathLikeAsync(value, context, span, owner).ConfigureAwait(false), context, span, scratch);
    private static async ValueTask<string> GetRetainedNativePathAsync(object value, string owner, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var text = asynchronous ? await CoercePathLikeAsync(value, context, span, owner).ConfigureAwait(false) : CoercePathLike(value, context, span, owner);
        var bytes = PathOps.WorkingBytes(text, context, span);
        context.MemoryGovernor.Reserve(bytes, span); context.MemoryGovernor.Commit(bytes);
        return text.AsString();
    }

    private static string DecodePath(PyString path, ExecutionContext context, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch)
    {
        scratch.Grow(PathOps.WorkingBytes(path, context, span), span);
        return path.AsString();
    }

    private static bool IsFileSystemFailure(HostOperationException error)
        => error.InnerException is LythonHostException { Kind: LythonHostFailureKind.NotFound
            or LythonHostFailureKind.NotDirectory or LythonHostFailureKind.PermissionDenied
            or LythonHostFailureKind.IsDirectory or LythonHostFailureKind.AlreadyExists
            or LythonHostFailureKind.BrokenPipe or LythonHostFailureKind.Transport };

    private static LythonPathStat RequireExistingPath(LythonPathStat stat, LythonSourceSpan span)
        => stat.Exists ? stat : throw new LythonRuntimeException("FileNotFoundError", "No such file or directory", span);

    private static LythonPathKind HostPathKind(string path, ExecutionContext context, LythonSourceSpan? span, bool osPath = false)
    {
        try { return HostStat(path, context, span).Kind; }
        catch (HostOperationException error) when (error.InnerException is LythonHostException failure &&
            (failure.Kind is LythonHostFailureKind.NotFound or LythonHostFailureKind.NotDirectory ||
             (osPath && IsFileSystemFailure(error))))
        { return LythonPathKind.Missing; }
    }

    private static async ValueTask<LythonPathKind> HostPathKindAsync(string path, ExecutionContext context, LythonSourceSpan? span, bool osPath = false)
    {
        try { return (await HostStatAsync(path, context, span).ConfigureAwait(false)).Kind; }
        catch (HostOperationException error) when (error.InnerException is LythonHostException failure &&
            (failure.Kind is LythonHostFailureKind.NotFound or LythonHostFailureKind.NotDirectory ||
             (osPath && IsFileSystemFailure(error))))
        { return LythonPathKind.Missing; }
    }

    private static LythonPathStat HostStat(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.CheckExecution(span);
        return context.HostStat(path, span);
    }

    private static async ValueTask<LythonPathStat> HostStatAsync(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.CheckExecution(span);
        return await context.HostStatAsync(path, span).ConfigureAwait(false);
    }

    private static double PathModifiedAtSeconds(DateTimeOffset? modifiedAt, LythonSourceSpan? span)
    {
        if (modifiedAt is not { } timestamp)
        {
            throw new LythonRuntimeException("ValueError", "host stat has no modification timestamp.", span);
        }

        return (timestamp - DateTimeOffset.UnixEpoch).TotalSeconds;
    }

    private static IEnumerable<string> EnumerateDirectoryParents(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        var end = path.TrimEnd('/').Length;
        for (var index = 0; index < end; index++)
        {
            if ((index & 4095) == 0) context.CheckExecution(span);
            if (index != 0 && path[index] == '/' && path[index - 1] != '/') yield return path[..index];
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

    private static string[] SplitNormalizedPath(string normalized)
    {
        if (normalized == "/" || normalized == ".")
        {
            return [];
        }

        return normalized.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static PathSplit SplitPath(string path)
    {
        if (path.Length == 0)
        {
            return new PathSplit(string.Empty, string.Empty);
        }

        var slash = path.LastIndexOf('/') + 1;
        if (slash < 0)
        {
            return new PathSplit(string.Empty, path);
        }

        var head = path[..slash];
        var tail = path[slash..];
        if (head.Length != 0 && head.Any(character => character != '/'))
        {
            head = head.TrimEnd('/');
        }

        return new PathSplit(head, tail);
    }

    private static ExtensionSplit SplitExt(string path)
    {
        if (path.Length == 0)
        {
            return new ExtensionSplit(string.Empty, string.Empty);
        }

        var slash = path.LastIndexOf('/');
        var segmentStart = slash + 1;
        var dot = path.LastIndexOf('.');
        if (dot <= segmentStart)
        {
            return new ExtensionSplit(path, string.Empty);
        }

        return new ExtensionSplit(path[..dot], path[dot..]);
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

    private static string JoinChild(string directory, string name) => directory.EndsWith('/') ? directory + name : directory + "/" + name;

    private static ICallable UnsupportedOsCallable(string name, string message)
        => new UnsupportedOsCallableObject(name, message);

}
