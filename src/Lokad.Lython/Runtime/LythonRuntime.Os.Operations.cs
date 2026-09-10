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
        var path = GetPathOrDefault(arguments, "os.listdir", span, context.Host.Cwd);
        context.RegisterHostCall(span);
        var result = new PyList(
            context.HostListDir(path, span).Select<string, object>(item => PyString.FromString(item, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> OsListDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.listdir", span, context.Host.Cwd);
        context.RegisterHostCall(span);
        var names = await context.HostListDirAsync(path, span).ConfigureAwait(false);
        var result = new PyList(
            names.Select<string, object>(item => PyString.FromString(item, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object OsWalk(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 0 or > 4)
        {
            throw new LythonRuntimeException("TypeError", "os.walk(top='.', topdown=True, onerror=None, followlinks=False) expects up to four arguments.", span);
        }

        var path = arguments.Length >= 1 ? GetPath(arguments[0], "os.walk", span) : context.Host.Cwd;
        var topdown = arguments.Length >= 2
            ? arguments[1] switch
            {
                PyNone => true,
                bool value => value,
                _ => throw new LythonRuntimeException("TypeError", "os.walk(..., topdown=...) expects topdown to be a bool.", span)
            }
            : true;

        ICallable? onerror = arguments.Length >= 3
            ? arguments[2] switch
            {
                PyNone => null,
                ICallable callable => callable,
                _ => throw new LythonRuntimeException("TypeError", "os.walk(..., onerror=...) expects a callable or None.", span)
            }
            : null;

        _ = arguments.Length >= 4
            ? arguments[3] switch
            {
                PyNone => false,
                bool value => value,
                _ => throw new LythonRuntimeException("TypeError", "os.walk(..., followlinks=...) expects followlinks to be a bool.", span)
            }
            : false;

        return new PyWalkIterator(
            PathOps.Normalize(path, context.Host.Cwd),
            topdown,
            onerror,
            context,
            span);
    }

    private static object OsGetCwd(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "os.getcwd() expects no arguments.", span);
        }

        context.RegisterHostCall(span);
        return PyString.FromString(context.Host.Cwd);
    }

    private static object OsFspath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.fspath(path) expects one path-like argument.", span);
        }

        return CoercePathLike(arguments[0], context, span, "os.fspath()");
    }

    private static object OsFsEncode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.fsencode(filename) expects one path-like argument.", span);
        }

        if (arguments[0] is PyBytes bytes)
        {
            return bytes;
        }

        var path = GetPath(arguments[0], "os.fsencode", span);
        return CreateBytes(Encoding.UTF8.GetBytes(path), context, span);
    }

    private static object OsFsDecode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.fsdecode(filename) expects one path-like argument.", span);
        }

        if (arguments[0] is PyBytes bytes)
        {
            return PyString.FromUtf8(bytes.ToArray(), context.MemoryGovernor, span);
        }

        return PyString.FromString(GetPath(arguments[0], "os.fsdecode", span), context.MemoryGovernor, span);
    }

    private static object OsGetEnv(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "os.getenv(key, default=None) expects one or two string arguments.", span);
        }

        var key = GetEnvironmentKey(arguments[0], "os.getenv", span);
        return context.State.Environment.TryGetValue(key, out var value)
            ? PyString.FromString(value)
            : arguments.Length == 2 ? arguments[1] : PyNone.Instance;
    }

    private static object OsPutEnv(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.putenv(key, value) expects two string arguments.", span);
        }

        var key = GetEnvironmentKey(arguments[0], "os.putenv", span);
        var value = GetEnvironmentValue(arguments[1], "os.putenv", span);
        context.State.Environment[key] = value;
        return PyNone.Instance;
    }

    private static object OsUnsetEnv(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.unsetenv(key) expects one string argument.", span);
        }

        var key = GetEnvironmentKey(arguments[0], "os.unsetenv", span);
        _ = context.State.Environment.Remove(key);
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
            : path.Split(':').Select<string, object>(PyString.FromString);
        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
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

    private static object OsStat(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.stat", span);
        return HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span);
    }

    private static async ValueTask<object> OsStatAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.stat", span);
        return await HostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false);
    }

    private static object OsScandir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.scandir", span, context.Host.Cwd);
        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        context.RegisterHostCall(span);
        var entries = context.HostListDir(normalized, span)
            .Select(name => new PyDirEntryObject(name, JoinChild(normalized, name), context.MemoryGovernor, span))
            .ToArray();
        context.ObserveCollectionCount(entries.Length, span);
        // Own the iterator plus one entry object and array slot per entry.
        var scandirBytes = checked(160L + 80L * entries.Length);
        context.MemoryGovernor.Reserve(scandirBytes, span);
        context.MemoryGovernor.Commit(scandirBytes);
        return new PyScandirIterator(entries);
    }

    private static async ValueTask<object> OsScandirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.scandir", span, context.Host.Cwd);
        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        context.RegisterHostCall(span);
        var names = await context.HostListDirAsync(normalized, span).ConfigureAwait(false);
        var entries = names
            .Select(name => new PyDirEntryObject(name, JoinChild(normalized, name), context.MemoryGovernor, span))
            .ToArray();
        context.ObserveCollectionCount(entries.Length, span);
        // Own the iterator plus one entry object and array slot per entry.
        var scandirBytes = checked(160L + 80L * entries.Length);
        context.MemoryGovernor.Reserve(scandirBytes, span);
        context.MemoryGovernor.Commit(scandirBytes);
        return new PyScandirIterator(entries);
    }

    private static object OsMkDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.mkdir", span);
        context.RegisterHostCall(span);
        context.HostMkDir(PathOps.Normalize(path, context.Host.Cwd), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsMkDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.mkdir", span);
        context.RegisterHostCall(span);
        await context.HostMkDirAsync(PathOps.Normalize(path, context.Host.Cwd), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsMkDirs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "os.makedirs(path[, exist_ok]) expects one or two arguments.", span);
        }

        var path = GetPath(arguments[0], "os.makedirs", span);
        var existOk = arguments.Length == 2
            ? arguments[1] switch
            {
                bool value => value,
                _ => throw new LythonRuntimeException("TypeError", "os.makedirs(path, exist_ok) expects exist_ok to be a bool.", span)
            }
            : false;

        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        var stat = HostStat(normalized, context, span);
        if (stat.Exists)
        {
            if (stat.IsDir && existOk)
            {
                return PyNone.Instance;
            }

            throw new LythonRuntimeException("RuntimeError", $"os.makedirs() target already exists: {normalized}", span);
        }

        foreach (var current in EnumerateMissingDirectories(normalized))
        {
            var currentStat = HostStat(current, context, span);
            if (currentStat.Exists)
            {
                if (!currentStat.IsDir)
                {
                    throw new LythonRuntimeException("RuntimeError", $"os.makedirs() path component is not a directory: {current}", span);
                }

                continue;
            }

            context.RegisterHostCall(span);
            context.HostMkDir(current, span);
        }

        return PyNone.Instance;
    }

    private static async ValueTask<object> OsMkDirsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "os.makedirs(path[, exist_ok]) expects one or two arguments.", span);
        }

        var path = GetPath(arguments[0], "os.makedirs", span);
        var existOk = arguments.Length == 2
            ? arguments[1] switch
            {
                bool value => value,
                _ => throw new LythonRuntimeException("TypeError", "os.makedirs(path, exist_ok) expects exist_ok to be a bool.", span)
            }
            : false;

        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        var stat = await HostStatAsync(normalized, context, span).ConfigureAwait(false);
        if (stat.Exists)
        {
            if (stat.IsDir && existOk)
            {
                return PyNone.Instance;
            }

            throw new LythonRuntimeException("RuntimeError", $"os.makedirs() target already exists: {normalized}", span);
        }

        foreach (var current in EnumerateMissingDirectories(normalized))
        {
            var currentStat = await HostStatAsync(current, context, span).ConfigureAwait(false);
            if (currentStat.Exists)
            {
                if (!currentStat.IsDir)
                {
                    throw new LythonRuntimeException("RuntimeError", $"os.makedirs() path component is not a directory: {current}", span);
                }

                continue;
            }

            context.RegisterHostCall(span);
            await context.HostMkDirAsync(current, span).ConfigureAwait(false);
        }

        return PyNone.Instance;
    }

    private static object OsRemove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.remove", span);
        context.RegisterHostCall(span);
        context.HostRemove(PathOps.Normalize(path, context.Host.Cwd), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsRemoveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.remove", span);
        context.RegisterHostCall(span);
        await context.HostRemoveAsync(PathOps.Normalize(path, context.Host.Cwd), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsRename(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.rename(src, dst) expects two string arguments.", span);
        }

        var source = GetPath(arguments[0], "os.rename", span);
        var destination = GetPath(arguments[1], "os.rename", span);
        context.RegisterHostCall(span);
        context.HostMove(PathOps.Normalize(source, context.Host.Cwd), PathOps.Normalize(destination, context.Host.Cwd), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsRenameAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.rename(src, dst) expects two string arguments.", span);
        }

        var source = GetPath(arguments[0], "os.rename", span);
        var destination = GetPath(arguments[1], "os.rename", span);
        context.RegisterHostCall(span);
        await context.HostMoveAsync(PathOps.Normalize(source, context.Host.Cwd), PathOps.Normalize(destination, context.Host.Cwd), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsReplace(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.replace(src, dst) expects two string arguments.", span);
        }

        var source = PathOps.Normalize(GetPath(arguments[0], "os.replace", span), context.Host.Cwd);
        var destination = PathOps.Normalize(GetPath(arguments[1], "os.replace", span), context.Host.Cwd);
        var destinationStat = HostStat(destination, context, span);
        if (destinationStat.Exists)
        {
            context.RegisterHostCall(span);
            context.HostRemove(destination, span);
        }

        context.RegisterHostCall(span);
        context.HostMove(source, destination, span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsReplaceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "os.replace(src, dst) expects two string arguments.", span);
        }

        var source = PathOps.Normalize(GetPath(arguments[0], "os.replace", span), context.Host.Cwd);
        var destination = PathOps.Normalize(GetPath(arguments[1], "os.replace", span), context.Host.Cwd);
        var destinationStat = await HostStatAsync(destination, context, span).ConfigureAwait(false);
        if (destinationStat.Exists)
        {
            context.RegisterHostCall(span);
            await context.HostRemoveAsync(destination, span).ConfigureAwait(false);
        }

        context.RegisterHostCall(span);
        await context.HostMoveAsync(source, destination, span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsRmDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.rmdir", span);
        context.RegisterHostCall(span);
        context.HostRemove(PathOps.Normalize(path, context.Host.Cwd), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> OsRmDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.rmdir", span);
        context.RegisterHostCall(span);
        await context.HostRemoveAsync(PathOps.Normalize(path, context.Host.Cwd), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object OsRmDirs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = PathOps.Normalize(GetSinglePath(arguments, "os.removedirs", span), context.Host.Cwd);
        var stat = HostStat(path, context, span);
        if (!stat.Exists || !stat.IsDir)
        {
            context.RegisterHostCall(span);
            context.HostRemove(path, span);
            return PyNone.Instance;
        }

        context.RegisterHostCall(span);
        context.HostRemove(path, span);

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
                context.RegisterHostCall(span);
                context.HostRemove(current, span);
            }
            catch (LythonRuntimeException ex) when (IsHostRuntimeFailure(ex))
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
        var path = PathOps.Normalize(GetSinglePath(arguments, "os.removedirs", span), context.Host.Cwd);
        var stat = await HostStatAsync(path, context, span).ConfigureAwait(false);
        if (!stat.Exists || !stat.IsDir)
        {
            context.RegisterHostCall(span);
            await context.HostRemoveAsync(path, span).ConfigureAwait(false);
            return PyNone.Instance;
        }

        context.RegisterHostCall(span);
        await context.HostRemoveAsync(path, span).ConfigureAwait(false);

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
                context.RegisterHostCall(span);
                await context.HostRemoveAsync(current, span).ConfigureAwait(false);
            }
            catch (LythonRuntimeException ex) when (IsHostRuntimeFailure(ex))
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
