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

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
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

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

}
