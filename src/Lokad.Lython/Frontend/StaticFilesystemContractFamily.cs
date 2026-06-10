using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticFilesystemContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (IsPathlibPathConstructor(targetName))
        {
            for (var i = 0; i < arguments.Positional.Count; i++)
            {
                emitted |= AnalyzePathLikeArgument(arguments, i, string.Empty, $"{targetName}([path][, ...]) expects string or Path path segments.", diagnostics, bindings);
            }

            return emitted;
        }

        emitted |= AnalyzeOsKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        emitted |= AnalyzeGlobKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        emitted |= AnalyzeFnmatchKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        return emitted;
    }

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "os" }, MemberName: "walk" })
        {
            AnalyzeOsWalkCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: "os" },
                    MemberName: "path"
                },
                MemberName: "commonpath"
            })
        {
            AnalyzeOsPathCommonPathCall(arguments, diagnostics, bindings);
            return true;
        }

        return false;
    }

    private static bool IsPathlibPathConstructor(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.PathlibPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.PathlibPurePath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.PathlibPurePosixPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.PathlibPosixPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.PathlibPureWindowsPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.PathlibWindowsPath.Name, StringComparison.Ordinal);

    private static bool AnalyzeOsKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.OsListDir.Name, StringComparison.Ordinal))
        {
            return AnalyzePathLikeArgument(arguments, 0, "path", "os.listdir([path]) expects path to be path-like.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsWalk.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "top", "os.walk([top][, topdown][, onerror][, followlinks]) expects top to be path-like.", diagnostics, bindings);
            emitted |= AnalyzeBooleanOrNoneArgument(arguments, 1, "topdown", "os.walk(..., topdown=...) expects a bool or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "onerror", "os.walk(..., onerror=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeBooleanOrNoneArgument(arguments, 3, "followlinks", "os.walk(..., followlinks=...) expects a bool or None.", diagnostics, bindings);
            return emitted;
        }

        if (IsSinglePathKnownCall(targetName))
        {
            return AnalyzePathLikeArgument(arguments, 0, "path", $"{targetName}(path) expects a path-like argument.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsMakedirs.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "path", "os.makedirs(path[, exist_ok]) expects path to be path-like.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 1, "exist_ok", "os.makedirs(path[, exist_ok]) expects exist_ok to be a bool.", diagnostics, bindings);
            return emitted;
        }

        if (IsTwoPathKnownCall(targetName))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "src", $"{targetName}(src, dst) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "dst", $"{targetName}(src, dst) expects path-like arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathJoin.Name, StringComparison.Ordinal))
        {
            for (var i = 0; i < arguments.Positional.Count; i++)
            {
                emitted |= AnalyzePathLikeArgument(arguments, i, string.Empty, "os.path.join(path, *paths) expects path-like arguments.", diagnostics, bindings);
            }

            return emitted;
        }

        if (IsOsPathSinglePathKnownCall(targetName))
        {
            return AnalyzePathLikeArgument(arguments, 0, "path", $"{targetName}(path) expects a path-like argument.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathRelPath.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "path", "os.path.relpath(path[, start]) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "start", "os.path.relpath(path[, start]) expects path-like arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathSameFile.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzePathLikeArgument(arguments, 0, "path1", "os.path.samefile(path1, path2) expects path-like arguments.", diagnostics, bindings);
            emitted |= AnalyzePathLikeArgument(arguments, 1, "path2", "os.path.samefile(path1, path2) expects path-like arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathCommonPath.Name, StringComparison.Ordinal))
        {
            return AnalyzeIterableOfPathLikeArgument(arguments, 0, "paths", "os.path.commonpath(paths) expects a non-empty iterable of path-like values, not a single path.", diagnostics, bindings, rejectSinglePathLike: true, requireNonEmpty: true);
        }

        return false;
    }

    private static bool AnalyzeGlobKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!string.Equals(targetName, LythonKnownCallableSignatures.Glob.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.IGlob.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.GlobEscape.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.GlobHasMagic.Name, StringComparison.Ordinal) &&
            !string.Equals(targetName, LythonKnownCallableSignatures.GlobTranslate.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Glob.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.IGlob.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeArgument(arguments, 0, "pathname", $"{targetName}(pathname, *, root_dir=None, dir_fd=None, recursive=False, include_hidden=False) expects pathname to be path-like.", diagnostics, bindings);
            emitted |= AnalyzePathLikeOrNoneArgument(arguments, 1, "root_dir", $"{targetName}(..., root_dir=...) expects root_dir to be path-like or None.", diagnostics, bindings);
            emitted |= AnalyzeUnsupportedDirFd(arguments, targetName, diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 3, "recursive", $"{targetName}(..., recursive=...) expects recursive to be a bool.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 4, "include_hidden", $"{targetName}(..., include_hidden=...) expects include_hidden to be a bool.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.GlobTranslate.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeArgument(arguments, 0, "pathname", "glob.translate(pathname, *, recursive=False, include_hidden=False, seps=None) expects pathname to be path-like.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 1, "recursive", "glob.translate(..., recursive=...) expects recursive to be a bool.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 2, "include_hidden", "glob.translate(..., include_hidden=...) expects include_hidden to be a bool.", diagnostics, bindings);
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

    private static bool AnalyzeFnmatchKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.FnMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.FnMatchCase.Name, StringComparison.Ordinal))
        {
            var signature = string.Equals(targetName, LythonKnownCallableSignatures.FnMatchCase.Name, StringComparison.Ordinal)
                ? "fnmatch.fnmatchcase(name, pattern)"
                : "fnmatch.fnmatch(name, pattern)";
            emitted |= AnalyzeStringArgument(arguments, 0, "name", $"{signature} expects two string arguments.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "pattern", $"{signature} expects two string arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.FnMatchFilter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 0, "names", "fnmatch.filter(names, pattern) expects an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "pattern", "fnmatch.filter(names, pattern) expects a string pattern.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.FnMatchTranslate.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "pattern", "fnmatch.translate(pattern) expects a string pattern.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool IsSinglePathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.OsFspath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsStat.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsLstat.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsScandir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsMkdir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsRemove.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsUnlink.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsRmdir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsRemovedirs.Name, StringComparison.Ordinal);

    private static bool IsTwoPathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.OsRename.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsReplace.Name, StringComparison.Ordinal);

    private static bool IsOsPathSinglePathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.OsPathSplit.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathSplitExt.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathBasename.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathDirname.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathIsAbs.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathNormPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathAbsPath.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathExists.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathLexists.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathIsFile.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathIsDir.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathGetSize.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathGetMTime.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathRealPath.Name, StringComparison.Ordinal);

    private static void AnalyzeOsWalkCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            1,
            "topdown",
            "LA3010",
            "os.walk(..., topdown=...) expects a bool or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeCallableOrNoneArgument(
            arguments,
            2,
            "onerror",
            "LA3011",
            "os.walk(..., onerror=...) expects a callable or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            3,
            "followlinks",
            "LA3012",
            "os.walk(..., followlinks=...) expects a bool or None.",
            diagnostics,
            bindings);
    }

    private static void AnalyzeOsPathCommonPathCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "paths", out var pathsExpression))
        {
            return;
        }

        var pathsValue = StaticAbstractValueResolver.ResolveOrUnknown(pathsExpression, bindings);
        if (IsPathLike(pathsValue))
        {
            AddDiagnostic(diagnostics, "LA3043", "os.path.commonpath(paths) expects an iterable of path-like values, not a single path.", pathsExpression.Span);
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(pathsExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", pathsExpression.Span);
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
