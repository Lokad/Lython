using System.Globalization;
using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static partial class StaticDataModuleContractFamily
{
    private static bool AnalyzeDifflibKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibIsLineJunk.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "line", "difflib.IS_LINE_JUNK(line) expects a string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibIsCharacterJunk.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "ch", "difflib.IS_CHARACTER_JUNK(ch) expects a string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibUnifiedDiff.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.DifflibContextDiff.Name, StringComparison.Ordinal))
        {
            var owner = targetName.EndsWith("unified_diff", StringComparison.Ordinal) ? "difflib.unified_diff" : "difflib.context_diff";
            var emitted = AnalyzeDifflibLineIterable(arguments, 0, "a", $"{owner}(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            emitted |= AnalyzeDifflibLineIterable(arguments, 1, "b", $"{owner}(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 2, "fromfile", $"{owner}(..., fromfile=...) expects a string or None.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 3, "tofile", $"{owner}(..., tofile=...) expects a string or None.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 4, "fromfiledate", $"{owner}(..., fromfiledate=...) expects a string or None.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 5, "tofiledate", $"{owner}(..., tofiledate=...) expects a string or None.", diagnostics, bindings);
            AnalyzeIntegerArgument(arguments, 6, "n", $"{owner}(..., n=...) expects an integer.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 7, "lineterm", $"{owner}(..., lineterm=...) expects a string or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibNdiff.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeDifflibLineIterable(arguments, 0, "a", "difflib.ndiff(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            emitted |= AnalyzeDifflibLineIterable(arguments, 1, "b", "difflib.ndiff(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            AnalyzeCallableOrNoneArgument(arguments, 2, "linejunk", "difflib.ndiff(..., linejunk=...) expects a callable or None.", diagnostics, bindings);
            AnalyzeCallableOrNoneArgument(arguments, 3, "charjunk", "difflib.ndiff(..., charjunk=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibRestore.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeDifflibLineIterable(arguments, 0, "delta", "difflib.restore(delta, which) expects delta to be an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeRestoreWhich(arguments, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibGetCloseMatches.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 0, "word", "difflib.get_close_matches(word, possibilities) expects word to be a string.", diagnostics, bindings);
            emitted |= AnalyzeDifflibLineIterable(arguments, 1, "possibilities", "difflib.get_close_matches(word, possibilities) expects possibilities to be an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzePositiveIntegerArgument(arguments, 2, "n", "difflib.get_close_matches(..., n=...) expects n to be positive.", diagnostics, bindings);
            emitted |= AnalyzeCutoffArgument(arguments, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibDiffBytes.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeArgument(arguments, 0, "dfunc", "difflib.diff_bytes(dfunc, ...) expects dfunc to be callable.", diagnostics, bindings, static value => !StaticAbstractFacts.IsDefinitelyNonCallable(value));
            emitted |= AnalyzeIterableOfBytesArgument(arguments, 1, "a", "difflib.diff_bytes(..., a=...) expects an iterable of bytes.", diagnostics, bindings);
            emitted |= AnalyzeIterableOfBytesArgument(arguments, 2, "b", "difflib.diff_bytes(..., b=...) expects an iterable of bytes.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 3, "fromfile", "difflib.diff_bytes(..., fromfile=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 4, "tofile", "difflib.diff_bytes(..., tofile=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 5, "fromfiledate", "difflib.diff_bytes(..., fromfiledate=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 6, "tofiledate", "difflib.diff_bytes(..., tofiledate=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 7, "n", "difflib.diff_bytes(..., n=...) expects an integer.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 8, "lineterm", "difflib.diff_bytes(..., lineterm=...) expects bytes or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibDiffer.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCallableOrNoneArgument(arguments, 0, "linejunk", "difflib.Differ(..., linejunk=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 1, "charjunk", "difflib.Differ(..., charjunk=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibHtmlDiff.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeIntegerArgument(arguments, 0, "tabsize", "difflib.HtmlDiff(..., tabsize=...) expects an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "wrapcolumn", "difflib.HtmlDiff(..., wrapcolumn=...) expects an integer or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "linejunk", "difflib.HtmlDiff(..., linejunk=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 3, "charjunk", "difflib.HtmlDiff(..., charjunk=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibSequenceMatcher.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCallableOrNoneArgument(arguments, 0, "isjunk", "difflib.SequenceMatcher(..., isjunk=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 3, "autojunk", "difflib.SequenceMatcher(..., autojunk=...) expects a bool.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool AnalyzeDifflibLineIterable(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeIterableOfStringsArgument(arguments, position, keyword, message, diagnostics, bindings);

    private static bool AnalyzePkgutilKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilModuleInfo.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 1, "name", "pkgutil.ModuleInfo(..., name, ...) expects a string name.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 2, "ispkg", "pkgutil.ModuleInfo(..., ispkg) expects a bool.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterModules.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeCollectionArgument(arguments, 0, "path", "pkgutil.iter_modules(..., path=...) expects None, a path string, Path, or iterable of path strings.", diagnostics, bindings, PathLikeArgumentPolicy.AllowNone | PathLikeArgumentPolicy.AllowSinglePath);
            emitted |= AnalyzeStringArgument(arguments, 1, "prefix", "pkgutil.iter_modules(..., prefix=...) expects a string.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilWalkPackages.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeCollectionArgument(arguments, 0, "path", "pkgutil.walk_packages(..., path=...) expects None, a path string, Path, or iterable of path strings.", diagnostics, bindings, PathLikeArgumentPolicy.AllowNone | PathLikeArgumentPolicy.AllowSinglePath);
            emitted |= AnalyzeStringArgument(arguments, 1, "prefix", "pkgutil.walk_packages(..., prefix=...) expects a string.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "onerror", "pkgutil.walk_packages(..., onerror=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilFindLoader.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.PkgutilResolveName.Name, StringComparison.Ordinal))
        {
            var owner = targetName.EndsWith("resolve_name", StringComparison.Ordinal) ? "pkgutil.resolve_name" : "pkgutil.find_loader";
            return AnalyzeStringArgument(arguments, 0, targetName.EndsWith("resolve_name", StringComparison.Ordinal) ? "name" : "fullname", $"{owner}(...) expects a module name string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilGetLoader.Name, StringComparison.Ordinal))
        {
            return AnalyzeArgument(
                arguments,
                0,
                "module_or_name",
                "pkgutil.get_loader(module_or_name) expects a module object or module name string.",
                diagnostics,
                bindings,
                static value => value.IsStringLike || value.Kind == AbstractValueKind.Module);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilExtendPath.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePathLikeCollectionArgument(arguments, 0, "path", "pkgutil.extend_path(path, name) expects None, a path string, Path, or iterable of path strings.", diagnostics, bindings, PathLikeArgumentPolicy.AllowNone | PathLikeArgumentPolicy.AllowSinglePath);
            emitted |= AnalyzeStringArgument(arguments, 1, "name", "pkgutil.extend_path(path, name) expects a package name string.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilGetData.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 0, "package", "pkgutil.get_data(package, resource) expects a string package name.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "resource", "pkgutil.get_data(package, resource) expects a string resource name.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterImporters.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "fullname", "pkgutil.iter_importers([fullname]) expects a module name string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterImporterModules.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterZipimportModules.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 1, "prefix", "pkgutil importer helpers expect prefix to be a string.", diagnostics, bindings);
        }

        return false;
    }

    private static void AnalyzePkgutilModuleInfoMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (memberName)
        {
            case "_replace":
                AnalyzeStringArgument(arguments, 1, "name", "ModuleInfo._replace(..., name=...) expects a string.", diagnostics, bindings);
                AnalyzeBooleanArgument(arguments, 2, "ispkg", "ModuleInfo._replace(..., ispkg=...) expects a bool.", diagnostics, bindings);
                break;
            case "index":
                AnalyzeStrictIntegerArgument(arguments, 1, "start", "ModuleInfo.index(value[, start[, stop]]) expects integer start/stop bounds.", diagnostics, bindings);
                AnalyzeStrictIntegerArgument(arguments, 2, "stop", "ModuleInfo.index(value[, start[, stop]]) expects integer start/stop bounds.", diagnostics, bindings);
                break;
        }
    }

    private static void AnalyzePkgutilLoaderMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (memberName)
        {
            case "is_package":
                AnalyzeStringOrNoneArgument(arguments, 0, "fullname", "loader.is_package([fullname]) expects a string or None.", diagnostics, bindings);
                break;
            case "get_source":
                AnalyzeStringOrNoneArgument(arguments, 0, "fullname", "loader.get_source([fullname]) expects a string or None.", diagnostics, bindings);
                break;
        }
    }

    private static bool AnalyzeIterableOfBytesArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.Bytes or AbstractValueKind.BytesType)
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            foreach (var item in value.RequireSequenceItems())
            {
                if (!StaticAbstractFacts.IsBytesLike(item) && !IsUnknown(item))
                {
                    AddDiagnostic(diagnostics, "LA3158", message, item.Span);
                    return true;
                }
            }

            return false;
        }

        if (value.Kind is AbstractValueKind.ListType or AbstractValueKind.SetType)
        {
            var item = value.RequireNestedValue();
            if (!StaticAbstractFacts.IsBytesLike(item) && !IsUnknown(item))
            {
                AddDiagnostic(diagnostics, "LA3158", message, item.Span);
                return true;
            }

            return false;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIterable(value))
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        return false;
    }

    private static bool AnalyzeBytesOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || StaticAbstractFacts.IsBytesLike(value));

    private static bool AnalyzeRestoreWhich(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 1, "which", bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (TryGetInt32(value, out var which) && which is 1 or 2)
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.Integer or AbstractValueKind.IntegerType)
        {
            AddDiagnostic(diagnostics, "LA3158", "difflib.restore(delta, which) expects which to be 1 or 2.", expression.Span);
            return true;
        }

        AddDiagnostic(diagnostics, "LA3158", "difflib.restore(delta, which) expects which to be an integer 1 or 2.", expression.Span);
        return true;
    }

    private static bool AnalyzeCutoffArgument(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 3, "cutoff", bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (!TryGetDouble(value, out var cutoff))
        {
            if (StaticAbstractFacts.IsNumericLike(value))
            {
                return false;
            }

            AddDiagnostic(diagnostics, "LA3158", "difflib.get_close_matches(..., cutoff=...) expects a number between 0 and 1.", expression.Span);
            return true;
        }

        if (cutoff < 0.0 || cutoff > 1.0)
        {
            AddDiagnostic(diagnostics, "LA3158", "difflib.get_close_matches(..., cutoff=...) expects cutoff between 0 and 1.", expression.Span);
            return true;
        }

        return false;
    }

    private static bool AnalyzePositiveIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.Boolean)
        {
            if (value.RequireBoolean())
            {
                return false;
            }

            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (TryGetInt32(value, out var integer))
        {
            if (integer > 0)
            {
                return false;
            }

            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (value.Kind is AbstractValueKind.IntegerType)
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
        return true;
    }

    private static void AnalyzeHtmlDiffCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        AnalyzeDifflibLineIterable(arguments, 0, "fromlines", $"HtmlDiff.{memberName}(fromlines, tolines) expects iterables of strings.", diagnostics, bindings);
        AnalyzeDifflibLineIterable(arguments, 1, "tolines", $"HtmlDiff.{memberName}(fromlines, tolines) expects iterables of strings.", diagnostics, bindings);
        AnalyzeStringOrNoneArgument(arguments, 2, "fromdesc", $"HtmlDiff.{memberName}(..., fromdesc=...) expects a string or None.", diagnostics, bindings);
        AnalyzeStringOrNoneArgument(arguments, 3, "todesc", $"HtmlDiff.{memberName}(..., todesc=...) expects a string or None.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, 4, "context", $"HtmlDiff.{memberName}(..., context=...) expects a bool.", diagnostics, bindings);
        AnalyzeIntegerArgument(arguments, 5, "numlines", $"HtmlDiff.{memberName}(..., numlines=...) expects an integer.", diagnostics, bindings);
        if (memberName == "make_file")
        {
            AnalyzeStringOrNoneArgument(arguments, 6, "charset", "HtmlDiff.make_file(..., charset=...) expects a string or None.", diagnostics, bindings);
        }
    }

    private static void AnalyzeSequenceMatcherMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (memberName)
        {
            case "find_longest_match":
                AnalyzeIntegerOrNoneArgument(arguments, 0, "alo", "SequenceMatcher.find_longest_match(..., alo=...) expects an integer or None.", diagnostics, bindings);
                AnalyzeIntegerOrNoneArgument(arguments, 1, "ahi", "SequenceMatcher.find_longest_match(..., ahi=...) expects an integer or None.", diagnostics, bindings);
                AnalyzeIntegerOrNoneArgument(arguments, 2, "blo", "SequenceMatcher.find_longest_match(..., blo=...) expects an integer or None.", diagnostics, bindings);
                AnalyzeIntegerOrNoneArgument(arguments, 3, "bhi", "SequenceMatcher.find_longest_match(..., bhi=...) expects an integer or None.", diagnostics, bindings);
                break;
            case "get_grouped_opcodes":
                AnalyzeIntegerArgument(arguments, 0, "n", "SequenceMatcher.get_grouped_opcodes([n]) expects an integer.", diagnostics, bindings);
                break;
        }
    }

    private static bool TryGetDouble(AbstractValue value, out double number)
    {
        if (value.Kind == AbstractValueKind.Integer &&
            int.TryParse(
                (value.RequireText()).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer))
        {
            number = integer;
            return true;
        }

        if (value.Kind == AbstractValueKind.Float &&
            double.TryParse(
                (value.RequireText()).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var floating))
        {
            number = floating;
            return true;
        }

        if (value.Kind == AbstractValueKind.Boolean)
        {
            number = value.RequireBoolean() ? 1.0 : 0.0;
            return true;
        }

        if (value.Kind is AbstractValueKind.IntegerType or AbstractValueKind.FloatType)
        {
            number = 0.0;
            return false;
        }

        number = 0.0;
        return false;
    }

}
