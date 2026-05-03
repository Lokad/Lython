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
        if (string.Equals(targetName, LythonKnownCallableSignatures.PathlibPath.Name, StringComparison.Ordinal))
        {
            for (var i = 0; i < arguments.Positional.Count; i++)
            {
                emitted |= AnalyzeStringArgument(arguments, i, string.Empty, "pathlib.Path(path[, ...]) expects string path segments.", diagnostics, bindings);
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

    private static bool AnalyzeOsKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.OsListDir.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "path", "os.listdir([path]) expects path to be a string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsWalk.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "top", "os.walk([top][, topdown][, onerror][, followlinks]) expects top to be a string.", diagnostics, bindings);
            emitted |= AnalyzeBooleanOrNoneArgument(arguments, 1, "topdown", "os.walk(..., topdown=...) expects a bool or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "onerror", "os.walk(..., onerror=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeBooleanOrNoneArgument(arguments, 3, "followlinks", "os.walk(..., followlinks=...) expects a bool or None.", diagnostics, bindings);
            return emitted;
        }

        if (IsSinglePathKnownCall(targetName))
        {
            return AnalyzeStringArgument(arguments, 0, "path", $"{targetName}(path) expects a string path.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsMakedirs.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "path", "os.makedirs(path[, exist_ok]) expects path to be a string.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 1, "exist_ok", "os.makedirs(path[, exist_ok]) expects exist_ok to be a bool.", diagnostics, bindings);
            return emitted;
        }

        if (IsTwoPathKnownCall(targetName))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "src", $"{targetName}(src, dst) expects string path arguments.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "dst", $"{targetName}(src, dst) expects string path arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathJoin.Name, StringComparison.Ordinal))
        {
            for (var i = 0; i < arguments.Positional.Count; i++)
            {
                emitted |= AnalyzeStringArgument(arguments, i, string.Empty, "os.path.join(path, *paths) expects string path arguments.", diagnostics, bindings);
            }

            return emitted;
        }

        if (IsOsPathSinglePathKnownCall(targetName))
        {
            return AnalyzeStringArgument(arguments, 0, "path", $"{targetName}(path) expects a string path.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathRelPath.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "path", "os.path.relpath(path[, start]) expects string path arguments.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "start", "os.path.relpath(path[, start]) expects string path arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.OsPathCommonPath.Name, StringComparison.Ordinal))
        {
            return AnalyzeIterableOfStringsArgument(arguments, 0, "paths", "os.path.commonpath(paths) expects a non-empty iterable of strings, not a single string.", diagnostics, bindings, rejectSingleString: true, requireNonEmpty: true);
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
            !string.Equals(targetName, LythonKnownCallableSignatures.GlobEscape.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var emitted = AnalyzeStringArgument(arguments, 0, "pathname", $"{targetName}(pathname[, recursive]) expects pathname to be a string.", diagnostics, bindings);
        if (!string.Equals(targetName, LythonKnownCallableSignatures.GlobEscape.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeBooleanArgument(arguments, 1, "recursive", $"{targetName}(pathname[, recursive]) expects recursive to be a bool.", diagnostics, bindings);
        }

        return emitted;
    }

    private static bool AnalyzeFnmatchKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.FnMatch.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "name", "fnmatch.fnmatch(name, pattern) expects two string arguments.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "pattern", "fnmatch.fnmatch(name, pattern) expects two string arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.FnMatchFilter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 0, "names", "fnmatch.filter(names, pattern) expects an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "pattern", "fnmatch.filter(names, pattern) expects a string pattern.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool IsSinglePathKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.OsMkdir.Name, StringComparison.Ordinal) ||
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
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathIsFile.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.OsPathIsDir.Name, StringComparison.Ordinal);

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

        if (StaticAbstractValueResolver.TryResolveKnownString(pathsExpression, bindings, out _))
        {
            AddDiagnostic(diagnostics, "LA3043", "os.path.commonpath(paths) expects an iterable of strings, not a single string.", pathsExpression.Span);
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
