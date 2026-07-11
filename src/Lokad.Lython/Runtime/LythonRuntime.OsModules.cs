using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class OsModule : PyModule, IPyContextualDynamicAttributes
    {
        public static readonly OsModule Instance = new();
        private static readonly string[] Names =
        [
            "path",
            "name",
            "sep",
            "curdir",
            "pardir",
            "linesep",
            "pathsep",
            "altsep",
            "extsep",
            "devnull",
            "F_OK",
            "R_OK",
            "W_OK",
            "X_OK",
            "environ",
            "listdir",
            "walk",
            "getcwd",
            "fspath",
            "fsencode",
            "fsdecode",
            "getenv",
            "putenv",
            "unsetenv",
            "get_exec_path",
            "stat",
            "lstat",
            "scandir",
            "mkdir",
            "makedirs",
            "remove",
            "unlink",
            "rename",
            "replace",
            "rmdir",
            "removedirs",
            "access",
            "chdir",
            "system",
            "popen",
            "open",
            "read",
            "write",
            "close",
            "dup",
            "fork",
            "execv",
            "execve",
            "spawnv",
            "spawnve",
            "chmod",
            "chown",
            "symlink",
            "link",
            "getpid",
            "kill",
        ];

        private OsModule() : base("os")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Names;

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "path" => OsPathModule.Instance,
                "name" => PyString.FromString("posix"),
                "sep" => PyStringOps.SlashLiteral,
                "curdir" => PyStringOps.DotLiteral,
                "pardir" => PyString.FromString(".."),
                "linesep" => PyString.FromString("\n"),
                "pathsep" => PyString.FromString(":"),
                "altsep" => PyNone.Instance,
                "extsep" => PyString.FromString("."),
                "devnull" => PyString.FromString("/dev/null"),
                "F_OK" => BigInteger.Zero,
                "R_OK" => new BigInteger(4),
                "W_OK" => new BigInteger(2),
                "X_OK" => BigInteger.One,
                "listdir" => new BuiltinCallable(LythonKnownCallableSignatures.OsListDir, OsListDir, OsListDirAsync),
                "walk" => new BuiltinCallable(LythonKnownCallableSignatures.OsWalk, OsWalk),
                "getcwd" => new BuiltinCallable(LythonKnownCallableSignatures.OsGetCwd, OsGetCwd),
                "fspath" => new BuiltinCallable(LythonKnownCallableSignatures.OsFspath, OsFspath),
                "fsencode" => new BuiltinCallable(LythonKnownCallableSignatures.OsFsEncode, OsFsEncode),
                "fsdecode" => new BuiltinCallable(LythonKnownCallableSignatures.OsFsDecode, OsFsDecode),
                "getenv" => new BuiltinCallable(LythonKnownCallableSignatures.OsGetEnv, OsGetEnv),
                "putenv" => new BuiltinCallable(LythonKnownCallableSignatures.OsPutEnv, OsPutEnv),
                "unsetenv" => new BuiltinCallable(LythonKnownCallableSignatures.OsUnsetEnv, OsUnsetEnv),
                "get_exec_path" => new BuiltinCallable(LythonKnownCallableSignatures.OsGetExecPath, OsGetExecPath),
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
                "access" => UnsupportedOsCallable("os.access", "os.access() is not supported because Lython's host path model does not expose permissions."),
                "chdir" => UnsupportedOsCallable("os.chdir", "os.chdir() is not supported because Lython keeps the host-provided cwd immutable for a run."),
                "system" => UnsupportedOsCallable("os.system", "os.system() is not supported; use the host-mediated subprocess module where available."),
                "popen" => UnsupportedOsCallable("os.popen", "os.popen() is not supported; streaming process handles are outside Lython's contained host surface."),
                "open" => UnsupportedOsCallable("os.open", "os.open() is not supported because raw file descriptors are outside Lython's contained host surface."),
                "read" => UnsupportedOsCallable("os.read", "os.read() is not supported because raw file descriptors are outside Lython's contained host surface."),
                "write" => UnsupportedOsCallable("os.write", "os.write() is not supported because raw file descriptors are outside Lython's contained host surface."),
                "close" => UnsupportedOsCallable("os.close", "os.close() is not supported because raw file descriptors are outside Lython's contained host surface."),
                "dup" => UnsupportedOsCallable("os.dup", "os.dup() is not supported because raw file descriptors are outside Lython's contained host surface."),
                "fork" => UnsupportedOsCallable("os.fork", "os.fork() is not supported by Lython."),
                "execv" => UnsupportedOsCallable("os.execv", "os.execv() is not supported by Lython."),
                "execve" => UnsupportedOsCallable("os.execve", "os.execve() is not supported by Lython."),
                "spawnv" => UnsupportedOsCallable("os.spawnv", "os.spawnv() is not supported by Lython."),
                "spawnve" => UnsupportedOsCallable("os.spawnve", "os.spawnve() is not supported by Lython."),
                "chmod" => UnsupportedOsCallable("os.chmod", "os.chmod() is not supported because Lython's host path model does not expose permissions."),
                "chown" => UnsupportedOsCallable("os.chown", "os.chown() is not supported because Lython's host path model does not expose ownership."),
                "symlink" => UnsupportedOsCallable("os.symlink", "os.symlink() is not supported because Lython's host path model does not expose symlinks."),
                "link" => UnsupportedOsCallable("os.link", "os.link() is not supported because Lython's host path model does not expose hard links."),
                "getpid" => UnsupportedOsCallable("os.getpid", "os.getpid() is not supported because process identity is outside Lython's contained host surface."),
                "kill" => UnsupportedOsCallable("os.kill", "os.kill() is not supported because signals are outside Lython's contained host surface."),
                _ => null!,
            };

            return value is not null;
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, out object value)
        {
            _ = span;
            if (name == "environ")
            {
                value = new PyEnvironmentMapping(context.State.Environment);
                return true;
            }

            return TryGetMember(name, out value);
        }
    }

    private sealed class OsPathModule : PyModule
    {
        public static readonly OsPathModule Instance = new();
        private static readonly string[] Names =
        [
            "join",
            "split",
            "splitext",
            "basename",
            "dirname",
            "isabs",
            "normpath",
            "normcase",
            "abspath",
            "relpath",
            "commonpath",
            "commonprefix",
            "exists",
            "lexists",
            "isfile",
            "isdir",
            "getsize",
            "getmtime",
            "getatime",
            "getctime",
            "samefile",
            "realpath",
            "expandvars",
            "expanduser",
            "splitdrive",
            "splitroot",
            "ismount",
            "islink",
            "supports_unicode_filenames",
        ];

        private OsPathModule() : base("os.path")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Names;

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
                "normcase" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathNormCase, OsPathNormCase),
                "abspath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathAbsPath, OsPathAbsPath),
                "relpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathRelPath, OsPathRelPath),
                "commonpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathCommonPath, OsPathCommonPath),
                "commonprefix" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathCommonPrefix, OsPathCommonPrefix),
                "exists" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathExists, OsPathExists, OsPathExistsAsync),
                "lexists" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathLexists, OsPathExists, OsPathExistsAsync),
                "isfile" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathIsFile, OsPathIsFile, OsPathIsFileAsync),
                "isdir" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathIsDir, OsPathIsDir, OsPathIsDirAsync),
                "getsize" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathGetSize, OsPathGetSize, OsPathGetSizeAsync),
                "getmtime" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathGetMTime, OsPathGetMTime, OsPathGetMTimeAsync),
                "getatime" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathGetATime, OsPathGetMTime, OsPathGetMTimeAsync),
                "getctime" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathGetCTime, OsPathGetMTime, OsPathGetMTimeAsync),
                "samefile" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathSameFile, OsPathSameFile),
                "realpath" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathRealPath, OsPathRealPath),
                "expandvars" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathExpandVars, OsPathExpandVars),
                "expanduser" => UnsupportedOsCallable("os.path.expanduser", "os.path.expanduser() is not supported because Lython has no ambient home directory."),
                "splitdrive" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathSplitDrive, OsPathSplitDrive),
                "splitroot" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathSplitRoot, OsPathSplitRoot),
                "ismount" => new BuiltinCallable(LythonKnownCallableSignatures.OsPathIsMount, OsPathIsMount),
                "islink" => UnsupportedOsCallable("os.path.islink", "os.path.islink() is not supported because Lython's host path model does not expose symlinks."),
                "supports_unicode_filenames" => true,
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
            .Select(name => new PyDirEntryObject(name, JoinChild(normalized, name)))
            .ToArray();
        context.ObserveCollectionCount(entries.Length, span);
        return new PyScandirIterator(entries);
    }

    private static async ValueTask<object> OsScandirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var path = GetPathOrDefault(arguments, "os.scandir", span, context.Host.Cwd);
        var normalized = PathOps.Normalize(path, context.Host.Cwd);
        context.RegisterHostCall(span);
        var names = await context.HostListDirAsync(normalized, span).ConfigureAwait(false);
        var entries = names
            .Select(name => new PyDirEntryObject(name, JoinChild(normalized, name)))
            .ToArray();
        context.ObserveCollectionCount(entries.Length, span);
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
            var next = GetPath(arguments[i], "os.path.join", span);
            if (next.StartsWith("/", StringComparison.Ordinal) || current.Length == 0)
            {
                current = next;
            }
            else if (current.EndsWith("/", StringComparison.Ordinal))
            {
                current += next;
            }
            else
            {
                current += "/" + next;
            }
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

    private sealed class UnsupportedOsCallableObject : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        private readonly string _message;

        public UnsupportedOsCallableObject(string name, string message)
        {
            Name = name;
            _message = message;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", _message, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static string GetEnvironmentKey(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var key))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(...) expects environment keys to be strings.", span);
        }

        return key.AsString();
    }

    private static string GetEnvironmentValue(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(...) expects environment values to be strings.", span);
        }

        return text.AsString();
    }

    private static string? GetEnvironmentMappingValue(object mapping, string key, LythonSourceSpan span)
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

    private static string ExpandVars(string path, IReadOnlyDictionary<string, string> environment)
    {
        if (path.Length == 0)
        {
            return path;
        }

        var builder = new StringBuilder(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            var ch = path[i];
            if (ch == '$')
            {
                if (i + 1 < path.Length && path[i + 1] == '{')
                {
                    var end = path.IndexOf('}', i + 2);
                    if (end >= 0)
                    {
                        var name = path[(i + 2)..end];
                        builder.Append(environment.TryGetValue(name, out var value) ? value : path[i..(end + 1)]);
                        i = end;
                        continue;
                    }
                }
                else
                {
                    var start = i + 1;
                    var end = start;
                    while (end < path.Length && IsEnvironmentNameChar(path[end]))
                    {
                        end++;
                    }

                    if (end > start)
                    {
                        var name = path[start..end];
                        builder.Append(environment.TryGetValue(name, out var value) ? value : path[i..end]);
                        i = end - 1;
                        continue;
                    }
                }
            }
            else if (ch == '%')
            {
                var end = path.IndexOf('%', i + 1);
                if (end > i + 1)
                {
                    var name = path[(i + 1)..end];
                    builder.Append(environment.TryGetValue(name, out var value) ? value : path[i..(end + 1)]);
                    i = end;
                    continue;
                }
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool IsEnvironmentNameChar(char ch)
        => char.IsAsciiLetterOrDigit(ch) || ch == '_';

    private sealed class PyEnvironmentMapping :
        IMutablePySubscriptableValue,
        IDeletablePySubscriptableValue,
        IPyTruthyValue,
        IPyIterableValue,
        IPyRenderableValue,
        IPyDynamicAttributes,
        IEnumerable<object>
    {
        private readonly Dictionary<string, string> _items;

        public PyEnvironmentMapping(Dictionary<string, string> items)
        {
            _items = items;
        }

        public bool TryGetString(string key, out string value) => _items.TryGetValue(key, out value!);

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var key = GetEnvironmentKey(index, "os.environ.__getitem__", span);
            if (!_items.TryGetValue(key, out var value))
            {
                throw new LythonRuntimeException("KeyError", $"Key '{key}' was not found.", span);
            }

            return PyString.FromString(value);
        }

        public void SetSubscript(object index, object value, LythonSourceSpan span)
        {
            var key = GetEnvironmentKey(index, "os.environ.__setitem__", span);
            _items[key] = GetEnvironmentValue(value, "os.environ.__setitem__", span);
        }

        public void DeleteSubscript(object index, LythonSourceSpan span)
        {
            var key = GetEnvironmentKey(index, "os.environ.__delitem__", span);
            if (!_items.Remove(key))
            {
                throw new LythonRuntimeException("KeyError", $"Key '{key}' was not found.", span);
            }
        }

        public bool IsTruthy() => _items.Count != 0;

        public IEnumerable<object> Iterate() => _items.Keys.Select<string, object>(PyString.FromString);

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "get" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = GetEnvironmentKey(arguments[0], "os.environ.get", span);
                    return _items.TryGetValue(key, out var found)
                        ? PyString.FromString(found)
                        : arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                }, "os.environ.get", ["key", "default"], 1),
                "keys" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.keys() expects no arguments.", span);
                    }

                    var result = new PyList(_items.Keys.Select<string, object>(PyString.FromString), context.MemoryGovernor, span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.keys", []),
                "values" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.values() expects no arguments.", span);
                    }

                    var result = new PyList(_items.Values.Select<string, object>(PyString.FromString), context.MemoryGovernor, span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.values", []),
                "items" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.items() expects no arguments.", span);
                    }

                    var result = new PyList(
                        _items.Select(pair => (object)new PyTuple(
                            [PyString.FromString(pair.Key), PyString.FromString(pair.Value)],
                            context.MemoryGovernor,
                            span)),
                        context.MemoryGovernor,
                        span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.items", []),
                "copy" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.copy() expects no arguments.", span);
                    }

                    var result = ToPyDict(context, span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.copy", []),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.clear() expects no arguments.", span);
                    }

                    _items.Clear();
                    return PyNone.Instance;
                }, "os.environ.clear", []),
                "update" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || arguments[0] is not PyDict source)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.update(mapping) expects one dictionary argument.", span);
                    }

                    foreach (var pair in source)
                    {
                        var key = GetEnvironmentKey(pair.Key, "os.environ.update", span);
                        _items[key] = GetEnvironmentValue(pair.Value, "os.environ.update", span);
                    }

                    return PyNone.Instance;
                }, "os.environ.update", ["mapping"]),
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

        public PyString RenderPython(PyRenderingContext context) => ToPyDict(context.Context, null).RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private PyDict ToPyDict(ExecutionContext context, LythonSourceSpan? span)
        {
            var result = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in _items)
            {
                result.SetItem(PyString.FromString(pair.Key), PyString.FromString(pair.Value));
            }

            return result;
        }
    }

    private sealed class PyScandirIterator : PyIteratorBase, IPyContextManager, IPyDynamicAttributes
    {
        private readonly PyDirEntryObject[] _entries;
        private int _index;
        private bool _closed;

        public PyScandirIterator(PyDirEntryObject[] entries)
        {
            _entries = entries;
        }

        public override bool TryMoveNext(out object value)
        {
            if (_closed || _index >= _entries.Length)
            {
                value = PyNone.Instance;
                return false;
            }

            value = _entries[_index++];
            return true;
        }

        public object Enter() => this;

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            _closed = true;
            return false;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "close" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "ScandirIterator.close() expects no arguments.", span);
                    }

                    _closed = true;
                    return PyNone.Instance;
                }, "ScandirIterator.close", []),
                "__enter__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "ScandirIterator.__enter__() expects no arguments.", span);
                    }

                    return this;
                }, "ScandirIterator.__enter__", []),
                "__exit__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "ScandirIterator.__exit__(exc_type, exc, tb) expects three arguments.", span);
                    }

                    _closed = true;
                    return false;
                }, "ScandirIterator.__exit__", ["exc_type", "exc", "tb"]),
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

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<ScandirIterator>");
        }
    }

    internal sealed class PyDirEntryObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly string _name;
        private readonly string _path;
        private LythonPathStat? _stat;

        public PyDirEntryObject(string name, string path)
        {
            _name = name;
            _path = path;
        }

        public string Path => _path;

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(_name),
                "path" => PyString.FromString(_path),
                "__fspath__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.__fspath__() expects no arguments.", span);
                    }

                    return PyString.FromString(_path);
                }, "DirEntry.__fspath__", []),
                "is_file" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_file() expects no arguments.", span);
                    }

                    return GetCachedStat(context, span).IsFile;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_file() expects no arguments.", span);
                    }

                    return (await GetCachedStatAsync(context, span).ConfigureAwait(false)).IsFile;
                },
                "DirEntry.is_file",
                []),
                "is_dir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_dir() expects no arguments.", span);
                    }

                    return GetCachedStat(context, span).IsDir;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_dir() expects no arguments.", span);
                    }

                    return (await GetCachedStatAsync(context, span).ConfigureAwait(false)).IsDir;
                },
                "DirEntry.is_dir",
                []),
                "stat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.stat() expects no arguments.", span);
                    }

                    return GetCachedStat(context, span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.stat() expects no arguments.", span);
                    }

                    return await GetCachedStatAsync(context, span).ConfigureAwait(false);
                },
                "DirEntry.stat",
                []),
                "inode" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.inode() expects no arguments.", span);
                    }

                    throw new LythonRuntimeException("NotImplementedError", "DirEntry.inode() is not supported because Lython's host path model does not expose inode metadata.", span);
                }, "DirEntry.inode", []),
                "is_symlink" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_symlink() expects no arguments.", span);
                    }

                    throw new LythonRuntimeException("NotImplementedError", "DirEntry.is_symlink() is not supported because Lython's host path model does not expose symlinks.", span);
                }, "DirEntry.is_symlink", []),
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

        private LythonPathStat GetCachedStat(ExecutionContext context, LythonSourceSpan span)
            => _stat ??= HostStat(_path, context, span);

        private async ValueTask<LythonPathStat> GetCachedStatAsync(ExecutionContext context, LythonSourceSpan span)
            => _stat ??= await OsHostStatAsync(_path, context, span).ConfigureAwait(false);
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

        public override async ValueTask<(bool HasValue, object Value)> TryMoveNextAsync()
        {
            while (_frames.Count != 0)
            {
                var frame = _frames.Peek();
                if (!frame.Scanned)
                {
                    if (!await TryScanAsync(frame).ConfigureAwait(false))
                    {
                        _frames.Pop();
                        continue;
                    }

                    if (_topdown)
                    {
                        frame.Yielded = true;
                        return (true, CreateTuple(frame));
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
                        var stat = await _context.HostStatAsync(childPath, _span).ConfigureAwait(false);
                        if (!stat.Exists || !stat.IsDir)
                        {
                            continue;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        await HandleWalkErrorAsync(ex.Message).ConfigureAwait(false);
                        continue;
                    }

                    _frames.Push(new WalkFrame(childPath));
                    goto ContinueTraversal;
                }

                if (!_topdown && !frame.Yielded)
                {
                    frame.Yielded = true;
                    return (true, CreateTuple(frame));
                }

                _frames.Pop();

            ContinueTraversal:
                continue;
            }

            return (false, PyNone.Instance);
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

        private async ValueTask<bool> TryScanAsync(WalkFrame frame)
        {
            try
            {
                _context.RegisterHostCall(_span);
                var names = await _context.HostListDirAsync(frame.DirectoryPath, _span).ConfigureAwait(false);
                var directories = new List<string>();
                var files = new List<string>();
                foreach (var name in names)
                {
                    var child = JoinChild(frame.DirectoryPath, name);
                    _context.RegisterHostCall(_span);
                    var stat = await _context.HostStatAsync(child, _span).ConfigureAwait(false);
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
                await HandleWalkErrorAsync(ex.Message).ConfigureAwait(false);
                return false;
            }
            catch (LythonRuntimeException ex) when (IsHostRuntimeFailure(ex))
            {
                await HandleWalkErrorAsync(ex.Message).ConfigureAwait(false);
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

        private async ValueTask HandleWalkErrorAsync(string message)
        {
            if (_onerror is null)
            {
                return;
            }

            var payload = PyString.FromString(message, _governor, _span);
            var callbackSpan = _span ?? new LythonSourceSpan(0, 0, 0, 0);
            _ = await InvokeCallableTargetAsync(
                _onerror,
                callbackSpan,
                callbackSpan,
                _context,
                () => ValueTask.FromResult(new[] { new CallArgumentValue(null, new PyException("RuntimeError", message, payload)) })).ConfigureAwait(false);
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
