using System.Globalization;
using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static partial class StaticDataModuleContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonLoad.Name, StringComparison.Ordinal))
        {
            AnalyzeJsonReadableFileArgument(arguments, 0, "fp", diagnostics, bindings);
            AnalyzeJsonLoadOptions(arguments, diagnostics, bindings, optionOffset: 1);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonLoads.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 0, "s", "json.loads(s, *, ...) expects a string argument.", diagnostics, bindings);
            AnalyzeJsonLoadOptions(arguments, diagnostics, bindings, optionOffset: 1);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonDump.Name, StringComparison.Ordinal))
        {
            AnalyzeJsonWritableFileArgument(arguments, 1, "fp", diagnostics, bindings);
            AnalyzeJsonDumpOptions(arguments, diagnostics, bindings, optionOffset: 2);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonDumps.Name, StringComparison.Ordinal))
        {
            AnalyzeJsonDumpOptions(arguments, diagnostics, bindings, optionOffset: 1);
            return false;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvReader.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCsvInputArgument(arguments, 0, "csvfile", "csv.reader(csvfile) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
            AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Standard, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvWriter.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeOptionalCsvWriterFileArgument(arguments, 0, "fileobj", diagnostics, bindings);
            AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Standard, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvDictReader.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCsvInputArgument(arguments, 0, "f", "csv.DictReader(f) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 1, "fieldnames", "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", diagnostics, bindings);
            AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Dictionary, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvDictWriter.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeRequiredCsvWriterFileArgument(arguments, 0, "f", diagnostics, bindings);
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 1, "fieldnames", "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeDictWriterExtrasAction(arguments, diagnostics, bindings);
            AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Dictionary, diagnostics, bindings);
            return emitted;
        }

        if (AnalyzeDifflibKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (AnalyzePkgutilKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (AnalyzeCopyKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (AnalyzeRandomKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
        }

        return false;
    }

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "csv" },
                MemberName: "reader"
            })
        {
            AnalyzeCsvReaderCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "csv" },
                MemberName: "DictReader"
            })
        {
            AnalyzeCsvDictReaderCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "csv" },
                MemberName: "DictWriter"
            })
        {
            AnalyzeCsvDictWriterCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: var dictWriterReceiver,
                MemberName: "writerow"
            } &&
            StaticAbstractValueResolver.TryResolve(dictWriterReceiver, bindings, out var dictWriterValue) &&
            dictWriterValue.Kind == AbstractValueKind.CsvDictWriter)
        {
            AnalyzeDictWriterRowCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "fnmatch" },
                MemberName: "filter"
            })
        {
            AnalyzeFnmatchFilterCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "functools" },
                MemberName: "update_wrapper"
            })
        {
            AnalyzeFunctoolsUpdateWrapperCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "random" },
                MemberName: "SystemRandom"
            })
        {
            AddDiagnostic(diagnostics, "LA3158", "random.SystemRandom(...) is unsupported by Lython because system entropy is not exposed.", call.Span);
            return true;
        }

        if (call.Target is MemberExpressionSyntax { Target: var receiverExpression, MemberName: var memberName } &&
            StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver))
        {
            if (receiver.Kind == AbstractValueKind.DifflibDiffer &&
                memberName == "compare")
            {
                AnalyzeDifflibLineIterable(arguments, 0, "a", "Differ.compare(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
                AnalyzeDifflibLineIterable(arguments, 1, "b", "Differ.compare(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.DifflibHtmlDiff &&
                memberName is "make_table" or "make_file")
            {
                AnalyzeHtmlDiffCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.DifflibSequenceMatcher)
            {
                AnalyzeSequenceMatcherMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.PkgutilModuleInfo)
            {
                AnalyzePkgutilModuleInfoMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.PkgutilLoader)
            {
                AnalyzePkgutilLoaderMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.Random)
            {
                AnalyzeRandomMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }
        }

        return false;
    }

}
