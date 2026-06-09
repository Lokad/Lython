using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal enum StaticReturnShape
{
    String,
    ListOfUnknown,
    ListOfString,
    ListOfOpenPyxlNamedStyle,
    Boolean,
    None,
    Path,
    ListOfPath,
    ListSame,
    ListElement,
    Dict,
    DictSame,
    SetSame,
    Integer,
    Float,
    TupleFloatInteger,
    TupleFloatFloat,
    RegexPattern,
    MaybeRegexMatch,
    RegexMatch,
    ArgparseParser,
    ArgparseMutuallyExclusiveGroup,
    ArgparseNamespace,
    CsvReader,
    CsvDictReader,
    CsvWriter,
    CsvDictWriter,
    CollectionsDefaultDict,
    CollectionsCounter,
    CollectionsDeque,
    CollectionsChainMap,
    Decimal,
    DecimalContext,
    DecimalTuple,
    DifflibDiffer,
    DifflibHtmlDiff,
    DifflibMatch,
    DifflibSequenceMatcher,
    PkgutilModuleInfo,
    PkgutilLoader,
    ListOfPkgutilModuleInfo,
    ListOfBytes,
    SubprocessCompletedProcess,
    ListOfListOfString,
    OpenPyxlWorkbook,
    OpenPyxlWorksheet,
    OpenPyxlCell,
    OpenPyxlHyperlink,
    OpenPyxlComment,
    OpenPyxlFont,
    OpenPyxlPatternFill,
    OpenPyxlBorder,
    OpenPyxlSide,
    OpenPyxlAlignment,
    OpenPyxlProtection,
    OpenPyxlNamedStyle,
    OpenPyxlColor,
    OpenPyxlTable,
    OpenPyxlTableStyleInfo,
    OpenPyxlDataValidation,
    OpenPyxlConditionalFormattingRule,
    OpenPyxlAutoFilter,
    OpenPyxlSheetProtection,
    OpenPyxlWorkbookProtection,
    OpenPyxlDrawing,
    OpenPyxlChart,
    OpenPyxlImage,
    OpenPyxlSheetView,
    OpenPyxlSelection,
    OpenPyxlPageMargins,
    OpenPyxlPageSetup,
    OpenPyxlTableCollection,
    OpenPyxlDataValidationList,
    OpenPyxlConditionalFormattingCollection,
    OpenPyxlColumnDimension,
    OpenPyxlRowDimension,
    OpenPyxlMergedCellSet,
    Unknown,
}

internal enum StaticMutationKind
{
    None,
    MutatesReceiver,
}

internal readonly record struct StaticMemberReturnContract(
    AbstractValueKind ReceiverKind,
    string MemberName,
    StaticReturnShape ReturnShape);

internal readonly record struct StaticMemberValueContract(
    AbstractValueKind ReceiverKind,
    string MemberName,
    StaticReturnShape ValueShape);

internal readonly record struct StaticCallShapeContract(
    int MinArgumentCount,
    int? MaxArgumentCount,
    string[]? ParameterNames = null,
    int? MaxPositionalCount = null,
    bool AllowsExtraKeywords = false)
{
    public StaticCallShapeContract(LythonCallableSignature signature)
        : this(
            signature.MinimumArgumentCount,
            signature.MaximumArgumentCount,
            signature.ParameterNames,
            signature.MaxPositionalCount ?? signature.ParameterNames?.Length,
            signature.AllowsExtraKeywords)
    {
    }

    public bool AcceptsArgumentCount(int count)
        => count >= MinArgumentCount && (MaxArgumentCount is null || count <= MaxArgumentCount.Value);

    public bool AcceptsArgumentShape(ConcreteCallArguments arguments)
        => !TryGetArgumentShapeFailure(arguments, out _, out _);

    public bool TryGetArgumentShapeFailure(
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        var count = arguments.Positional.Count + arguments.Keywords.Count;
        if (!AcceptsArgumentCount(count))
        {
            reason = "callable arity contract rejected the argument count";
            offendingExpression = null;
            return true;
        }

        if (MaxPositionalCount.HasValue && arguments.Positional.Count > MaxPositionalCount.Value)
        {
            reason = "callable argument contract rejected too many positional arguments";
            offendingExpression = null;
            return true;
        }

        if (ParameterNames is null)
        {
            foreach (var keyword in arguments.Keywords)
            {
                reason = $"callable argument contract rejected keyword '{keyword.Key}'";
                offendingExpression = keyword.Value;
                return true;
            }

            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        if (arguments.Positional.Count > ParameterNames.Length)
        {
            reason = "callable argument contract rejected too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var assigned = new bool[ParameterNames.Length];
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            assigned[i] = true;
        }

        foreach (var keyword in arguments.Keywords)
        {
            var index = Array.IndexOf(ParameterNames, keyword.Key);
            if (index < 0)
            {
                if (AllowsExtraKeywords)
                {
                    continue;
                }

                reason = $"callable argument contract rejected unexpected keyword '{keyword.Key}'";
                offendingExpression = keyword.Value;
                return true;
            }

            if (assigned[index])
            {
                reason = $"callable argument contract rejected duplicate binding for '{keyword.Key}'";
                offendingExpression = keyword.Value;
                return true;
            }

            assigned[index] = true;
        }

        for (var i = 0; i < MinArgumentCount; i++)
        {
            if (!assigned[i])
            {
                reason = $"callable argument contract rejected missing required argument '{ParameterNames[i]}'";
                offendingExpression = null;
                return true;
            }
        }

        reason = string.Empty;
        offendingExpression = null;
        return false;
    }
}

internal readonly record struct StaticCallableContract(
    AbstractValueKind ReceiverKind,
    string MemberName,
    int MinArgumentCount,
    int? MaxArgumentCount,
    string DiagnosticCode,
    string Message,
    StaticMutationKind Mutation = StaticMutationKind.None,
    string[]? ParameterNames = null,
    bool AllowsExtraKeywords = false)
{
    public bool AcceptsArgumentCount(int count)
        => Shape.AcceptsArgumentCount(count);

    public bool AcceptsArgumentShape(ConcreteCallArguments arguments)
        => Shape.AcceptsArgumentShape(arguments);

    public bool TryGetArgumentShapeFailure(
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
        => Shape.TryGetArgumentShapeFailure(arguments, out reason, out offendingExpression);

    private StaticCallShapeContract Shape => new(MinArgumentCount, MaxArgumentCount, ParameterNames, AllowsExtraKeywords: AllowsExtraKeywords);
}

internal readonly record struct StaticKnownCallContract(
    LythonCallableSignature Signature,
    string DiagnosticCode,
    string Message,
    StaticReturnShape ReturnShape = StaticReturnShape.Unknown)
{
    public string TargetName => Signature.Name;

    public bool AcceptsArgumentShape(ConcreteCallArguments arguments)
        => Shape.AcceptsArgumentShape(arguments);

    public bool TryGetArgumentShapeFailure(
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
        => Shape.TryGetArgumentShapeFailure(arguments, out reason, out offendingExpression);

    private StaticCallShapeContract Shape => new(Signature);
}
