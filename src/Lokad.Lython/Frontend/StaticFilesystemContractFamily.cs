using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticFilesystemContractFamily
{
    internal static bool AnalyzePathlibKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!IsPathlibPathConstructor(targetName))
        {
            return false;
        }

        var emitted = false;
        for (var i = 0; i < arguments.PositionalCount; i++)
        {
            emitted |= AnalyzePathLikeArgument(arguments, i, string.Empty, $"{targetName}([path][, ...]) expects string or Path path segments.", diagnostics, bindings);
        }

        return emitted;
    }

    private static bool IsPathlibPathConstructor(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.Pathlib.Path.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Pathlib.PurePath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Pathlib.PurePosixPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Pathlib.PosixPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Pathlib.PureWindowsPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Pathlib.WindowsPath.Name, StringComparison.Ordinal);

    internal static bool AnalyzeOsKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.ListDir.Name, StringComparison.Ordinal))
        {
            return AnalyzePathLikeArgument(arguments, 0, "path", "os.listdir([path]) expects path to be path-like.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.Walk.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "onerror", "os.walk(..., onerror=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (IsSinglePathKnownCall(targetName))
        {
            return AnalyzePathLikeArgument(arguments, 0, "path", $"{targetName}(path) expects a path-like argument.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.Makedirs.Name, StringComparison.Ordinal))
        {
            return emitted;
        }

        if (IsTwoPathKnownCall(targetName))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "src", $"{targetName}(src, dst) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "dst", $"{targetName}(src, dst) expects path-like arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.PathJoin.Name, StringComparison.Ordinal))
        {
            for (var i = 0; i < arguments.PositionalCount; i++)
            {
                emitted |= AnalyzePathLikeArgument(arguments, i, string.Empty, "os.path.join(path, *paths) expects path-like arguments.", diagnostics, bindings);
            }

            return emitted;
        }

        if (IsOsPathSinglePathKnownCall(targetName))
        {
            return AnalyzePathLikeArgument(arguments, 0, "path", $"{targetName}(path) expects a path-like argument.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.PathRelPath.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "path", "os.path.relpath(path[, start]) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "start", "os.path.relpath(path[, start]) expects path-like arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.PathSameFile.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "path1", "os.path.samefile(path1, path2) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "path2", "os.path.samefile(path1, path2) expects path-like arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Os.PathCommonPath.Name, StringComparison.Ordinal))
        {
            return AnalyzePathLikeCollectionArgument(arguments, 0, "paths", "os.path.commonpath(paths) expects a non-empty iterable of path-like values, not a single path.", diagnostics, bindings, PathLikeArgumentPolicy.RequireNonEmpty);
        }

        return false;
    }

    internal static bool AnalyzeShutilKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.Shutil.CopyFile.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.Shutil.Copy.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.Shutil.Copy2.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "src", $"{targetName}(src, dst, *, follow_symlinks=True) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "dst", $"{targetName}(src, dst, *, follow_symlinks=True) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 2, "follow_symlinks", $"{targetName}(..., follow_symlinks=...) expects a bool.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Shutil.Move.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "src", "shutil.move(src, dst, copy_function=copy) expects path-like source and destination arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "dst", "shutil.move(src, dst, copy_function=copy) expects path-like source and destination arguments.", diagnostics, bindings);
            emitted |= AnalyzeUnsupportedShutilMoveCopyFunction(arguments, diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool AnalyzeUnsupportedShutilMoveCopyFunction(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 2, "copy_function", bindings, out var expression, out var value) ||
            IsUnknown(value))
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", "shutil.move(..., copy_function=...) is not supported by Lython.", expression.Span);
        return true;
    }

    internal static bool AnalyzeGlobKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!string.Equals(targetName, LythonKnownCallableSignatures.Glob.Find.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.Glob.IGlob.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.Glob.Escape.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.Glob.HasMagic.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.Glob.Translate.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Glob.Find.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.Glob.IGlob.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeArgument(arguments, 0, "pathname", $"{targetName}(pathname, *, root_dir=None, dir_fd=None, recursive=False, include_hidden=False) expects pathname to be path-like.", diagnostics, bindings);
            emitted |= AnalyzePathLikeOrNoneArgument(arguments, 1, "root_dir", $"{targetName}(..., root_dir=...) expects root_dir to be path-like or None.", diagnostics, bindings);
            emitted |= AnalyzeUnsupportedDirFd(arguments, targetName, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Glob.Translate.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeArgument(arguments, 0, "pathname", "glob.translate(pathname, *, recursive=False, include_hidden=False, seps=None) expects pathname to be path-like.", diagnostics, bindings);
            emitted |= AnalyzeStringOrNoneArgument(arguments, 3, "seps", "glob.translate(..., seps=...) expects seps to be a string or None.", diagnostics, bindings);
            return emitted;
        }

        return AnalyzePathLikeArgument(arguments, 0, "pathname", $"{targetName}(pathname) expects pathname to be path-like.", diagnostics, bindings);
    }

    private static bool AnalyzeUnsupportedDirFd(
        ConcreteCallArguments arguments,
        string targetName,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 2, "dir_fd", bindings, out var expression, out var value) ||
            IsUnknown(value) ||
            value.Kind == AbstractValueKind.None)
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", $"{targetName}(..., dir_fd=...) is not supported by Lython; raw file descriptors are outside the host path model.", expression.Span);
        return true;
    }

    internal static bool AnalyzeFnmatchKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.Fnmatch.FnMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.Fnmatch.FnMatchCase.Name, StringComparison.Ordinal))
        {
            var signature = string.Equals(targetName, LythonKnownCallableSignatures.Fnmatch.FnMatchCase.Name, StringComparison.Ordinal)
                ? "fnmatch.fnmatchcase(name, pattern)"
                : "fnmatch.fnmatch(name, pattern)";
            emitted |= AnalyzeStringArgument(arguments, 0, "name", $"{signature} expects two string arguments.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "pattern", $"{signature} expects two string arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Fnmatch.FnMatchFilter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 0, "names", "fnmatch.filter(names, pattern) expects an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "pattern", "fnmatch.filter(names, pattern) expects a string pattern.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Fnmatch.FnMatchTranslate.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "pattern", "fnmatch.translate(pattern) expects a string pattern.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool IsSinglePathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.Os.Fspath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Stat.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Lstat.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Scandir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Mkdir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Remove.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Unlink.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Rmdir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Removedirs.Name, StringComparison.Ordinal);

    private static bool IsTwoPathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.Os.Rename.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.Replace.Name, StringComparison.Ordinal);

    private static bool IsOsPathSinglePathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.Os.PathSplit.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathSplitExt.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathBasename.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathDirname.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathIsAbs.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathNormPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathAbsPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathExists.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathLexists.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathIsFile.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathIsDir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathGetSize.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathGetMTime.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Os.PathRealPath.Name, StringComparison.Ordinal);

}
