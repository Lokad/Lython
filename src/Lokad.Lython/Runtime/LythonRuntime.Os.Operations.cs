using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object OsListDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = GetPathOrDefault(arguments, "os.listdir", span, context.Host.Cwd, context, pathScratch);
        context.CheckExecution(span);
        var result = new PyList(
            context.HostListDir(path, span).Select<string, object>(item => PyString.FromString(item, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);
        context.CheckExecution(span);

        return result;
    }

    private static async ValueTask<object> OsListDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = await GetPathOrDefaultAsync(arguments, "os.listdir", span, context.Host.Cwd, context, pathScratch).ConfigureAwait(false);
        context.CheckExecution(span);
        var names = await context.HostListDirAsync(path, span).ConfigureAwait(false);
        var result = new PyList(
            names.Select<string, object>(item => PyString.FromString(item, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);
        context.CheckExecution(span);

        return result;
    }

    private static object OsWalk(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
        => OsWalkCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsWalkAsync(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
        => OsWalkCore(arguments, span, context, true);
    private static async ValueTask<object> OsWalkCore(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        bool Supplied(int index) => index < arguments.Assigned.Length && arguments.Assigned[index];
        var path = Supplied(0) ? (asynchronous ? await CoercePathLikeAsync(arguments.Values[0], context, span, "os.walk").ConfigureAwait(false)
            : CoercePathLike(arguments.Values[0], context, span, "os.walk")) : PyString.FromString(context.Host.Cwd, context.MemoryGovernor, span);
        var topdown = !Supplied(1) || (asynchronous ? await IsTruthyAsync(arguments.Values[1], context, span).ConfigureAwait(false) : IsTruthy(arguments.Values[1], context, span));
        ICallable? onerror = Supplied(2) ? arguments.Values[2] switch
        { PyNone => null, ICallable callable => callable, _ => throw RuntimeErrors.Type("os.walk onerror expects a callable or None", span) } : null;
        if (Supplied(3))
        {
            if (asynchronous) await IsTruthyAsync(arguments.Values[3], context, span).ConfigureAwait(false);
            else IsTruthy(arguments.Values[3], context, span);
        }
        using var scratch = PathOps.ReserveWorking(path, context, span);
        var retained = checked(64L + 4L * (path.Utf8Bytes.Length + context.Host.Cwd.Length));
        context.MemoryGovernor.Reserve(retained, span); context.MemoryGovernor.Commit(retained);
        return new PyWalkIterator(path.AsString(), topdown, onerror, context, span);
    }

    private static object OsGetCwd(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "os.getcwd() expects no arguments.", span);
        }

        context.CheckExecution(span);
        return PyString.FromString(context.Host.Cwd, context.MemoryGovernor, span);
    }

    private static object OsFspath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => CoercePathLike(arguments[0], context, span, "os.fspath");
    private static async ValueTask<object> OsFspathAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await CoercePathLikeAsync(arguments[0], context, span, "os.fspath").ConfigureAwait(false);

    private static object OsFsEncode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsFsEncodeCore(arguments[0], span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsFsEncodeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsFsEncodeCore(arguments[0], span, context, true);
    private static async ValueTask<object> OsFsEncodeCore(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (value is PyBytes bytes) return bytes;
        var path = asynchronous ? await CoercePathLikeAsync(value, context, span, "os.fsencode").ConfigureAwait(false) : CoercePathLike(value, context, span, "os.fsencode");
        using var reservation = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(path.Utf8Bytes.Length), span);
        return new PyBytes(path.Utf8Bytes.ToArray(), context.MemoryGovernor, span, reservation);
    }

    private static object OsFsDecode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsFsDecodeCore(arguments[0], span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> OsFsDecodeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsFsDecodeCore(arguments[0], span, context, true);
    private static async ValueTask<object> OsFsDecodeCore(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (value is PyBytes bytes) return PyString.CopyFromUtf8(bytes.Bytes, context.MemoryGovernor, span);
        return asynchronous ? await CoercePathLikeAsync(value, context, span, "os.fsdecode").ConfigureAwait(false) : CoercePathLike(value, context, span, "os.fsdecode");
    }

    private static object OsGetEnv(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "os.getenv(key, default=None) expects one or two string arguments.", span);
        }

        return GetEnvironmentMapping(context, span).GetDefault(arguments[0], arguments.Length == 2 ? arguments[1] : PyNone.Instance, span);
    }

    private static object OsPutEnv(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.putenv(key, value) expects two string arguments.", span);
        }

        GetEnvironmentMapping(context, span).SetSubscript(arguments[0], arguments[1], span);
        return PyNone.Instance;
    }

    private static object OsUnsetEnv(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.unsetenv(key) expects one string argument.", span);
        }

        GetEnvironmentMapping(context, span).RemoveKey(arguments[0], span);
        return PyNone.Instance;
    }

    private static object OsGetExecPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 0 or > 1)
        {
            throw new LythonRuntimeException("TypeError", "os.get_exec_path(env=None) expects zero or one argument.", span);
        }

        var path = arguments.Length == 0 || arguments[0] is PyNone
            ? context.State.Environment.GetValueOrDefault("PATH")
            : GetEnvironmentMappingValue(arguments[0], "PATH", span);

        var items = path is null
            ? Array.Empty<object>()
            : path.Split(':').Select<string, object>(item => PyString.FromString(item, context.MemoryGovernor, span));
        var result = new PyList(items, context.MemoryGovernor, span);
        context.CheckExecution(span);
        return result;

        static string? GetEnvironmentMappingValue(object mapping, string key, LythonSourceSpan span)
        {
            if (mapping is PyEnvironmentMapping environment)
            {
                return environment.TryGetString(key, out var value) ? value : null;
            }

            if (mapping is PyDict dict)
            {
                return dict.TryGetValue(PyString.FromString(key), out var value)
                    ? GetEnvironmentValue(value, "os.get_exec_path", span)
                    : null;
            }

            throw new LythonRuntimeException("TypeError", "os.get_exec_path(env) expects a mapping or None.", span);
        }
    }

    // os.stat results escape to the guest as fresh heap records on every call;
    // charge one box unit per call. Entry stats instead ride entry counts, and
    // internal HostStat uses stay transient and uncharged.
    private const long StatValueBytes = 64;

    private static object OsStat(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = GetSinglePath(arguments, "os.stat", span, context, pathScratch);
        var stat = RequireExistingPath(HostStat(path, context, span), span);
        context.MemoryGovernor.Reserve(StatValueBytes, span);
        context.MemoryGovernor.Commit(StatValueBytes);
        return stat;
    }

    private static async ValueTask<object> OsStatAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = await GetSinglePathAsync(arguments, "os.stat", span, context, pathScratch).ConfigureAwait(false);
        var stat = RequireExistingPath(await HostStatAsync(path, context, span).ConfigureAwait(false), span);
        context.MemoryGovernor.Reserve(StatValueBytes, span);
        context.MemoryGovernor.Commit(StatValueBytes);
        return stat;
    }

    private static object OsScandir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = GetPathOrDefault(arguments, "os.scandir", span, context.Host.Cwd, context, pathScratch);
        var normalized = path;
        context.CheckExecution(span);
        var names = context.HostListDir(normalized, span);
        var backingBytes = checked(32L + 8L * names.Count);
        context.MemoryGovernor.Reserve(backingBytes, span); context.MemoryGovernor.Commit(backingBytes);
        var entries = new PyDirEntryObject[names.Count];
        for (var i = 0; i < names.Count; i++)
        {
            context.CheckExecution(span);
            var entryBytes = checked(144L + 2L * normalized.Length + 4L * names[i].Length);
            context.MemoryGovernor.Reserve(entryBytes, span); context.MemoryGovernor.Commit(entryBytes);
            var entry = new PyDirEntryObject(names[i], JoinChild(normalized, names[i]), context.MemoryGovernor, span);
            entries[i] = entry;
        }

        context.CheckExecution(span);
        // Own the iterator shell; entries carry their own storage charges above.
        context.MemoryGovernor.Reserve(160L, span);
        context.MemoryGovernor.Commit(160L);
        var iterator = new PyScandirIterator(entries);
        return iterator;
    }

    private static async ValueTask<object> OsScandirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = await GetPathOrDefaultAsync(arguments, "os.scandir", span, context.Host.Cwd, context, pathScratch).ConfigureAwait(false);
        var normalized = path;
        context.CheckExecution(span);
        var names = await context.HostListDirAsync(normalized, span).ConfigureAwait(false);
        var backingBytes = checked(32L + 8L * names.Count);
        context.MemoryGovernor.Reserve(backingBytes, span); context.MemoryGovernor.Commit(backingBytes);
        var entries = new PyDirEntryObject[names.Count];
        for (var i = 0; i < names.Count; i++)
        {
            context.CheckExecution(span);
            var entryBytes = checked(144L + 2L * normalized.Length + 4L * names[i].Length);
            context.MemoryGovernor.Reserve(entryBytes, span); context.MemoryGovernor.Commit(entryBytes);
            var entry = new PyDirEntryObject(names[i], JoinChild(normalized, names[i]), context.MemoryGovernor, span);
            entries[i] = entry;
        }

        context.CheckExecution(span);
        // Own the iterator shell; entries carry their own storage charges above.
        context.MemoryGovernor.Reserve(160L, span);
        context.MemoryGovernor.Commit(160L);
        var iterator = new PyScandirIterator(entries);
        return iterator;
    }

    private static object OsMkDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = GetSinglePath(arguments, "os.mkdir", span, context, pathScratch);
        context.CheckExecution(span);
        context.HostMkDir(path, span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsMkDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = await GetSinglePathAsync(arguments, "os.mkdir", span, context, pathScratch).ConfigureAwait(false);
        context.CheckExecution(span);
        await context.HostMkDirAsync(path, span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsMkDirs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsMkDirsCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> OsMkDirsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => OsMkDirsCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> OsMkDirsCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var scratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = asynchronous ? await GetPathAsync(arguments[0], "os.makedirs", span, context, scratch).ConfigureAwait(false)
            : GetPath(arguments[0], "os.makedirs", span, context, scratch);
        var existOk = arguments.Length == 2 && (asynchronous ? await IsTruthyAsync(arguments[1], context, span).ConfigureAwait(false)
            : IsTruthy(arguments[1], context, span));
        await CreateDirectoriesAsync(path, existOk, context, span, asynchronous).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static async ValueTask CreateDirectoriesAsync(string path, bool existOk, ExecutionContext context, LythonSourceSpan? span, bool asynchronous)
    {
        context.CheckExecution(span);
        if (existOk && (asynchronous ? await HostPathKindAsync(path, context, span).ConfigureAwait(false)
            : HostPathKind(path, context, span)) == LythonPathKind.Directory) return;
        var trimmed = path.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        var dotLeaf = trimmed.AsSpan(lastSlash + 1).SequenceEqual(".");
        var parentExisted = true;
        if (dotLeaf)
        {
            var parent = lastSlash < 0 ? "." : lastSlash == 0 ? "/" : trimmed[..lastSlash];
            parentExisted = (asynchronous ? await HostPathKindAsync(parent, context, span).ConfigureAwait(false)
                : HostPathKind(parent, context, span)) == LythonPathKind.Directory;
        }
        foreach (var parent in EnumerateDirectoryParents(path, context, span))
        {
            context.CheckExecution(span);
            var kind = asynchronous ? await HostPathKindAsync(parent, context, span).ConfigureAwait(false) : HostPathKind(parent, context, span);
            if (kind == LythonPathKind.Directory) continue;
            if (kind == LythonPathKind.File) throw new LythonRuntimeException("NotADirectoryError", "Path component is not a directory", span);
            await CreateSingleDirectoryAsync(parent, true, context, span, asynchronous).ConfigureAwait(false);
        }
        // os.makedirs stops after creating a previously missing dot-leaf parent.
        if (dotLeaf && !parentExisted) return;
        await CreateSingleDirectoryAsync(path, existOk, context, span, asynchronous).ConfigureAwait(false);
    }

    private static async ValueTask CreateSingleDirectoryAsync(string path, bool existOk, ExecutionContext context, LythonSourceSpan? span, bool asynchronous)
    {
        context.CheckExecution(span);
        try
        {
            if (asynchronous) await context.HostMkDirAsync(path, span).ConfigureAwait(false);
            else context.HostMkDir(path, span);
        }
        catch (HostOperationException error) when (error.InnerException is LythonHostException { Kind: LythonHostFailureKind.AlreadyExists })
        {
            if (!existOk || (asynchronous ? await HostPathKindAsync(path, context, span).ConfigureAwait(false)
                : HostPathKind(path, context, span)) != LythonPathKind.Directory) throw;
        }
        context.CheckExecution(span);
    }

    private static object OsRemove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = GetSinglePath(arguments, "os.remove", span, context, pathScratch);
        context.CheckExecution(span);
        context.HostRemoveFile(path, false, span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsRemoveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = await GetSinglePathAsync(arguments, "os.remove", span, context, pathScratch).ConfigureAwait(false);
        context.CheckExecution(span);
        await context.HostRemoveFileAsync(path, false, span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsRename(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.rename(src, dst) expects two string arguments.", span);
        }

        var source = GetPath(arguments[0], "os.rename", span, context, pathScratch);
        var destination = GetPath(arguments[1], "os.rename", span, context, pathScratch);
        context.CheckExecution(span);
        context.HostMove(source, destination, span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsRenameAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.rename(src, dst) expects two string arguments.", span);
        }

        var source = await GetPathAsync(arguments[0], "os.rename", span, context, pathScratch).ConfigureAwait(false);
        var destination = await GetPathAsync(arguments[1], "os.rename", span, context, pathScratch).ConfigureAwait(false);
        context.CheckExecution(span);
        await context.HostMoveAsync(source, destination, span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsReplace(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        context.CheckExecution(span);
        context.HostReplace(GetPath(arguments[0], "os.replace", span, context, pathScratch), GetPath(arguments[1], "os.replace", span, context, pathScratch), span);
        return PyNone.Instance;
    }
    private static async ValueTask<object> OsReplaceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        context.CheckExecution(span);
        await context.HostReplaceAsync(await GetPathAsync(arguments[0], "os.replace", span, context, pathScratch).ConfigureAwait(false), await GetPathAsync(arguments[1], "os.replace", span, context, pathScratch).ConfigureAwait(false), span).ConfigureAwait(false);
        return PyNone.Instance;
    }
    private static object OsRmDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        context.CheckExecution(span);
        context.HostRemoveDirectory(GetSinglePath(arguments, "os.rmdir", span, context, pathScratch), span);
        return PyNone.Instance;
    }
    private static async ValueTask<object> OsRmDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        context.CheckExecution(span);
        await context.HostRemoveDirectoryAsync(await GetSinglePathAsync(arguments, "os.rmdir", span, context, pathScratch).ConfigureAwait(false), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsRmDirs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = GetSinglePath(arguments, "os.removedirs", span, context, pathScratch);
        var stat = HostStat(path, context, span);
        if (!stat.Exists || !stat.IsDir)
        {
            context.CheckExecution(span);
            context.HostRemoveDirectory(path, span);
            return PyNone.Instance;
        }

        context.CheckExecution(span);
        context.HostRemoveDirectory(path, span);

        var current = ParentDirectory(path);
        while (current is not "/" and not ".")
        {
            var currentStat = HostStat(current, context, span);
            if (!currentStat.Exists || !currentStat.IsDir)
            {
                break;
            }

            try
            {
                context.CheckExecution(span);
                context.HostRemoveDirectory(current, span);
            }
            catch (HostOperationException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            current = ParentDirectory(current);
        }

        return PyNone.Instance;
    }

    private static async ValueTask<object> OsRmDirsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var pathScratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var path = await GetSinglePathAsync(arguments, "os.removedirs", span, context, pathScratch).ConfigureAwait(false);
        var stat = await HostStatAsync(path, context, span).ConfigureAwait(false);
        if (!stat.Exists || !stat.IsDir)
        {
            context.CheckExecution(span);
            await context.HostRemoveDirectoryAsync(path, span).ConfigureAwait(false);
            return PyNone.Instance;
        }

        context.CheckExecution(span);
        await context.HostRemoveDirectoryAsync(path, span).ConfigureAwait(false);

        var current = ParentDirectory(path);
        while (current is not "/" and not ".")
        {
            var currentStat = await HostStatAsync(current, context, span).ConfigureAwait(false);
            if (!currentStat.Exists || !currentStat.IsDir)
            {
                break;
            }

            try
            {
                context.CheckExecution(span);
                await context.HostRemoveDirectoryAsync(current, span).ConfigureAwait(false);
            }
            catch (HostOperationException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            current = ParentDirectory(current);
        }

        return PyNone.Instance;
    }

}
