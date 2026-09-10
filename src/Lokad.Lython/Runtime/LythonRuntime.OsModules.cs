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

        // Fixed os vocabulary: share every constant forever like the slash
        // and dot literals, so per-access reads alias stably instead of
        // allocating fresh strings on every access.
        private static readonly PyString PosixName = PyString.FromString("posix");
        private static readonly PyString ParentDirName = PyString.FromString("..");
        private static readonly PyString LinesepName = PyString.FromString("\n");
        private static readonly PyString PathsepName = PyString.FromString(":");
        private static readonly PyString ExtsepName = PyString.FromString(".");
        private static readonly PyString DevnullName = PyString.FromString("/dev/null");
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

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => OsPathModule.Instance,
                "name" => PosixName,
                "sep" => PyStringOps.SlashLiteral,
                "curdir" => PyStringOps.DotLiteral,
                "pardir" => ParentDirName,
                "linesep" => LinesepName,
                "pathsep" => PathsepName,
                "altsep" => PyNone.Instance,
                "extsep" => ExtsepName,
                "devnull" => DevnullName,
                "F_OK" => BigInteger.Zero,
                "R_OK" => new BigInteger(4),
                "W_OK" => new BigInteger(2),
                "X_OK" => BigInteger.One,
                "listdir" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsListDir, OsListDir, OsListDirAsync),
                "walk" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsWalk, OsWalk),
                "getcwd" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsGetCwd, OsGetCwd),
                "fspath" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsFspath, OsFspath),
                "fsencode" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsFsEncode, OsFsEncode),
                "fsdecode" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsFsDecode, OsFsDecode),
                "getenv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsGetEnv, OsGetEnv),
                "putenv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPutEnv, OsPutEnv),
                "unsetenv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsUnsetEnv, OsUnsetEnv),
                "get_exec_path" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsGetExecPath, OsGetExecPath),
                "stat" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsStat, OsStat, OsStatAsync),
                "lstat" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsLstat, OsStat, OsStatAsync),
                "scandir" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsScandir, OsScandir, OsScandirAsync),
                "mkdir" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsMkdir, OsMkDir, OsMkDirAsync),
                "makedirs" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsMakedirs, OsMkDirs, OsMkDirsAsync),
                "remove" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsRemove, OsRemove, OsRemoveAsync),
                "unlink" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsUnlink, OsRemove, OsRemoveAsync),
                "rename" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsRename, OsRename, OsRenameAsync),
                "replace" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsReplace, OsReplace, OsReplaceAsync),
                "rmdir" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsRmdir, OsRmDir, OsRmDirAsync),
                "removedirs" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsRemovedirs, OsRmDirs, OsRmDirsAsync),
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = span;
            if (name == "environ")
            {
                value = new PyEnvironmentMapping(context.State.Environment, context.MemoryGovernor, span);
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

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "join" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathJoin, OsPathJoin),
                "split" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathSplit, OsPathSplit),
                "splitext" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathSplitExt, OsPathSplitExt),
                "basename" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathBasename, OsPathBaseName),
                "dirname" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathDirname, OsPathDirName),
                "isabs" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathIsAbs, OsPathIsAbs),
                "normpath" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathNormPath, OsPathNormPath),
                "normcase" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathNormCase, OsPathNormCase),
                "abspath" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathAbsPath, OsPathAbsPath),
                "relpath" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathRelPath, OsPathRelPath),
                "commonpath" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathCommonPath, OsPathCommonPath),
                "commonprefix" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathCommonPrefix, OsPathCommonPrefix),
                "exists" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathExists, OsPathExists, OsPathExistsAsync),
                "lexists" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathLexists, OsPathExists, OsPathExistsAsync),
                "isfile" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathIsFile, OsPathIsFile, OsPathIsFileAsync),
                "isdir" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathIsDir, OsPathIsDir, OsPathIsDirAsync),
                "getsize" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathGetSize, OsPathGetSize, OsPathGetSizeAsync),
                "getmtime" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathGetMTime, OsPathGetMTime, OsPathGetMTimeAsync),
                "getatime" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathGetATime, OsPathGetMTime, OsPathGetMTimeAsync),
                "getctime" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathGetCTime, OsPathGetMTime, OsPathGetMTimeAsync),
                "samefile" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathSameFile, OsPathSameFile),
                "realpath" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathRealPath, OsPathRealPath),
                "expandvars" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathExpandVars, OsPathExpandVars),
                "expanduser" => UnsupportedOsCallable("os.path.expanduser", "os.path.expanduser() is not supported because Lython has no ambient home directory."),
                "splitdrive" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathSplitDrive, OsPathSplitDrive),
                "splitroot" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathSplitRoot, OsPathSplitRoot),
                "ismount" => BuiltinCallable.Create(LythonKnownCallableSignatures.OsPathIsMount, OsPathIsMount),
                "islink" => UnsupportedOsCallable("os.path.islink", "os.path.islink() is not supported because Lython's host path model does not expose symlinks."),
                "supports_unicode_filenames" => true,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

}
