using System.Globalization;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class OsModule : PyModule
    {
        public static readonly OsModule Instance = new();

        private OsModule() : base("os")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "path" => OsPathModule.Instance,
                "sep" => PyStringOps.SlashLiteral,
                "curdir" => PyStringOps.DotLiteral,
                "pardir" => PyString.FromString(".."),
                "listdir" => new BuiltinCallable(LythonKnownCallableSignatures.OsListDir, OsListDir, OsListDirAsync),
                "walk" => new BuiltinCallable(LythonKnownCallableSignatures.OsWalk, OsWalk),
                "getcwd" => new BuiltinCallable(LythonKnownCallableSignatures.OsGetCwd, OsGetCwd),
                "fspath" => new BuiltinCallable(LythonKnownCallableSignatures.OsFspath, OsFspath),
                "stat" => new BuiltinCallable(LythonKnownCallableSignatures.OsStat, OsStat, OsStatAsync),
                "lstat" => new BuiltinCallable(LythonKnownCallableSignatures.OsLstat, OsStat, OsStatAsync),
                "scandir" => new BuiltinCallable(LythonKnownCallableSignatures.OsScandir, OsScandir, OsScandirAsync),
                "mkdir" => new BuiltinCallable(LythonKnownCallableSignatures.OsMkdir, OsMkDir, OsMkDirAsync),
                "makedirs" => new BuiltinCallable(LythonKnownCallableSignatures.OsMakedirs, OsMkDirs, OsMkDirsAsync),
                "remove" => new BuiltinCallable(LythonKnownCallableSignatures.OsRemove, OsRemove, OsRemoveAsync),
                "unlink" => new BuiltinCallable(LythonKnownCallableSignatures.OsUnlink, OsRemove, OsRemoveAsync),
                "rename" => new BuiltinCallable(LythonKnownCallableSignatures.OsRename, OsRename, OsRenameAsync),
                "replace" => new BuiltinCallable(LythonKnownCallableSignatures.OsReplace, OsReplace, OsReplaceAsync),
                "rmdir" => new BuiltinCallable(LythonKnownCallableSignatures.OsRmdir, OsRmDir, OsRmDirAsync),
                "removedirs" => new BuiltinCallable(LythonKnownCallableSignatures.OsRemovedirs, OsRmDirs, OsRmDirsAsync),
                _ => null!,
            };

            return value is not null;
        }
    }

    private sealed class OsPathModule : PyModule
    {
        public static readonly OsPathModule Instance = new();

        private OsPathModule() : base("os.path")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "join" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathJoin, OsPathJoin),
                "split" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathSplit, OsPathSplit),
                "splitext" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathSplitExt, OsPathSplitExt),
                "basename" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathBasename, OsPathBaseName),
                "dirname" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathDirname, OsPathDirName),
                "isabs" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathIsAbs, OsPathIsAbs),
                "normpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathNormPath, OsPathNormPath),
                "abspath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathAbsPath, OsPathAbsPath),
                "relpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathRelPath, OsPathRelPath),
                "commonpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathCommonPath, OsPathCommonPath),
                "exists" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathExists, OsPathExists, OsPathExistsAsync),
                "lexists" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathLexists, OsPathExists, OsPathExistsAsync),
                "isfile" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathIsFile, OsPathIsFile, OsPathIsFileAsync),
                "isdir" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathIsDir, OsPathIsDir, OsPathIsDirAsync),
                "getsize" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathGetSize, OsPathGetSize, OsPathGetSizeAsync),
                "getmtime" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathGetMTime, OsPathGetMTime, OsPathGetMTimeAsync),
                "samefile" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathSameFile, OsPathSameFile),
                "realpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathRealPath, OsPathRealPath),
                _ => null!,
            };

            return value is not null;
        }
    }

    private static object OsListDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.listdir", span, context.Host.Cwd);
        context.RegisterHostCall(span);
        var result = new PyList(
            context.HostListDir(path, span).Select<string, object>(item => PyString.FromString(item)),
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
            names.Select<string, object>(item => PyString.FromString(item)),
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
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "os.fspath(path) expects one path-like argument.", span);
        }

        return PyString.FromString(GetPath(arguments[0], "os.fspath", span));
    }

    private static object OsStat(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.stat", span);
        return HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span);
    }

    private static async ValueTask<object> OsStatAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.stat", span);
        return await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false);
    }

    private static object OsScandir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.scandir", span, context.Host.Cwd);
        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        context.RegisterHostCall(span);
        var entries = context.HostListDir(normalized, span)
            .Select<string, object>(name => new PyDirEntryObject(name, JoinChild(normalized, name)));
        return new PyList(entries, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> OsScandirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.scandir", span, context.Host.Cwd);
        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        context.RegisterHostCall(span);
        var names = await context.HostListDirAsync(normalized, span).ConfigureAwait(false);
        var entries = names.Select<string, object>(name => new PyDirEntryObject(name, JoinChild(normalized, name)));
        return new PyList(entries, context.MemoryGovernor, span);
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
        var stat = await OsHostStatAsync(normalized, context, span).ConfigureAwait(false);
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
            var currentStat = await OsHostStatAsync(current, context, span).ConfigureAwait(false);
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
        var destinationStat = await OsHostStatAsync(destination, context, span).ConfigureAwait(false);
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
        var stat = await OsHostStatAsync(path, context, span).ConfigureAwait(false);
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
            var currentStat = await OsHostStatAsync(current, context, span).ConfigureAwait(false);
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

    private static object OsPathJoin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "os.path.join(...) expects one or more string arguments.", span);
        }

        var current = GetPath(arguments[0], "os.path.join", span);
        for (var i = 1; i < arguments.Length; i++)
        {
            current = PathOps.Join(current, GetPath(arguments[i], "os.path.join", span));
        }

        return PyString.FromString(current);
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
        return PathModifiedAtSeconds(HostStat(PathOps.Normalize(path, context.Host.Cwd), context, span).ModifiedAt, span);
    }

    private static async ValueTask<object> OsPathGetMTimeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetSinglePath(arguments, "os.path.getmtime", span);
        var stat = await OsHostStatAsync(PathOps.Normalize(path, context.Host.Cwd), context, span).ConfigureAwait(false);
        return PathModifiedAtSeconds(stat.ModifiedAt, span);
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

    private static double PathModifiedAtSeconds(string modifiedAt, LythonSourceSpan? span)
    {
        if (!DateTimeOffset.TryParse(
            modifiedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            throw new LythonRuntimeException("ValueError", "host modified_at timestamp is not ISO-8601 parseable.", span);
        }

        return (parsed - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds;
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

        if (path == "/")
        {
            return ("/", string.Empty);
        }

        var end = path.Length;
        while (end > 1 && path[end - 1] == '/')
        {
            end--;
        }

        var trimmed = path[..end];
        var slash = trimmed.LastIndexOf('/');
        if (slash < 0)
        {
            return (string.Empty, trimmed);
        }

        if (slash == 0)
        {
            return ("/", trimmed[1..]);
        }

        return (trimmed[..slash], trimmed[(slash + 1)..]);
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

    internal sealed class PyDirEntryObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly string _name;
        private readonly string _path;

        public PyDirEntryObject(string name, string path)
        {
            _name = name;
            _path = path;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(_name),
                "path" => PyString.FromString(_path),
                "is_file" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_file() expects no arguments.", span);
                    }

                    return HostStat(_path, context, span).IsFile;
                }),
                "is_dir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_dir() expects no arguments.", span);
                    }

                    return HostStat(_path, context, span).IsDir;
                }),
                "stat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.stat() expects no arguments.", span);
                    }

                    return HostStat(_path, context, span);
                }),
                _ => null!,
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<DirEntry '{_name}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyWalkIterator : PyIteratorBase
    {
        private readonly bool _topdown;
        private readonly ICallable? _onerror;
        private readonly ExecutionContext _context;
        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan? _span;
        private readonly Stack<WalkFrame> _frames = [];

        public PyWalkIterator(string root, bool topdown, ICallable? onerror, ExecutionContext context, LythonSourceSpan? span)
        {
            _topdown = topdown;
            _onerror = onerror;
            _context = context;
            _governor = context.MemoryGovernor;
            _span = span;
            _frames.Push(new WalkFrame(root));
        }

        public override bool TryMoveNext(out object value)
        {
            while (_frames.Count != 0)
            {
                var frame = _frames.Peek();
                if (!frame.Scanned)
                {
                    if (!TryScan(frame))
                    {
                        _frames.Pop();
                        continue;
                    }

                    if (_topdown)
                    {
                        frame.Yielded = true;
                        value = CreateTuple(frame);
                        return true;
                    }
                }

                var childNames = frame.GetChildNames(_topdown, _span);
                while (frame.NextChildIndex < childNames.Count)
                {
                    var childName = childNames[frame.NextChildIndex++];
                    var childPath = JoinChild(frame.DirectoryPath, childName);
                    try
                    {
                        _context.RegisterHostCall(_span);
                        var stat = _context.HostStat(childPath, _span);
                        if (!stat.Exists || !stat.IsDir)
                        {
                            continue;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        HandleWalkError(ex.Message);
                        continue;
                    }

                    _frames.Push(new WalkFrame(childPath));
                    goto ContinueTraversal;
                }

                if (!_topdown && !frame.Yielded)
                {
                    frame.Yielded = true;
                    value = CreateTuple(frame);
                    return true;
                }

                _frames.Pop();

            ContinueTraversal:
                continue;
            }

            value = PyNone.Instance;
            return false;
        }

        private bool TryScan(WalkFrame frame)
        {
            try
            {
                _context.RegisterHostCall(_span);
                var names = _context.HostListDir(frame.DirectoryPath, _span);
                var directories = new List<string>();
                var files = new List<string>();
                foreach (var name in names)
                {
                    var child = JoinChild(frame.DirectoryPath, name);
                    _context.RegisterHostCall(_span);
                    var stat = _context.HostStat(child, _span);
                    if (!stat.Exists)
                    {
                        continue;
                    }

                    if (stat.IsDir)
                    {
                        directories.Add(name);
                    }
                    else if (stat.IsFile)
                    {
                        files.Add(name);
                    }
                }

                directories.Sort(StringComparer.Ordinal);
                files.Sort(StringComparer.Ordinal);
                frame.SetEntries(CreateStringList(directories), CreateStringList(files));
                return true;
            }
            catch (InvalidOperationException ex)
            {
                HandleWalkError(ex.Message);
                return false;
            }
            catch (LythonRuntimeException ex) when (IsHostRuntimeFailure(ex))
            {
                HandleWalkError(ex.Message);
                return false;
            }
        }

        private PyList CreateStringList(IReadOnlyList<string> items)
        {
            var values = new object[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                values[i] = PyString.FromString(items[i], _governor, _span);
            }

            return new PyList(values, _governor, _span);
        }

        private void HandleWalkError(string message)
        {
            if (_onerror is null)
            {
                return;
            }

            var payload = PyString.FromString(message, _governor, _span);
            var callbackSpan = _span ?? new LythonSourceSpan(0, 0, 0, 0);
            _ = InvokeCallableTarget(
                _onerror,
                callbackSpan,
                callbackSpan,
                _context,
                [new CallArgumentValue(null, new PyException("RuntimeError", message, payload))]);
        }

        private object CreateTuple(WalkFrame frame)
        {
            return new PyTuple(
            [
                PyString.FromString(frame.DirectoryPath, _governor, _span),
                frame.DirectoryNames!,
                frame.FileNames!
            ],
            _governor,
            _span);
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<os.walk>");

        private sealed class WalkFrame
        {
            public WalkFrame(string directoryPath)
            {
                DirectoryPath = directoryPath;
            }

            public string DirectoryPath { get; }

            public bool Scanned { get; private set; }

            public bool Yielded { get; set; }

            public int NextChildIndex { get; set; }

            public PyList? DirectoryNames { get; private set; }

            public PyList? FileNames { get; private set; }

            public void SetEntries(PyList directoryNames, PyList fileNames)
            {
                DirectoryNames = directoryNames;
                FileNames = fileNames;
                Scanned = true;
            }

            public List<string> GetChildNames(bool topdown, LythonSourceSpan? span)
            {
                if (!topdown)
                {
                    var values = new List<string>(DirectoryNames?.Count ?? 0);
                    if (DirectoryNames is not null)
                    {
                        foreach (var item in DirectoryNames)
                        {
                            values.Add(RequireWalkDirName(item, span));
                        }
                    }

                    return values;
                }

                var result = new List<string>(DirectoryNames?.Count ?? 0);
                if (DirectoryNames is null)
                {
                    return result;
                }

                foreach (var item in DirectoryNames)
                {
                    result.Add(RequireWalkDirName(item, span));
                }

                return result;
            }

            private static string RequireWalkDirName(object value, LythonSourceSpan? span)
            {
                if (!PyStringOps.TryAsString(value, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "os.walk(...) expects dirnames to remain a list of strings.", span);
                }

                return text.AsString();
            }
        }
    }
}
