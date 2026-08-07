using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal enum StaticReturnShape
{
    String,
    Bytes,
    ListOfUnknown,
    ListOfString,
    ListOfFloat,
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
    DateTimeTimedelta,
    DateTimeDate,
    DateTimeTime,
    DateTimeDateTime,
    DateTimeTimezone,
    StatisticsLinearRegression,
    StatisticsNormalDist,
    Random,
    DifflibDiffer,
    DifflibHtmlDiff,
    DifflibMatch,
    DifflibSequenceMatcher,
    PkgutilModuleInfo,
    PkgutilLoader,
    ListOfPkgutilModuleInfo,
    ListOfBytes,
    SubprocessCompletedProcess,
    SubprocessPopen,
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

internal readonly record struct StaticMemberContractKey(
    AbstractValueKind ReceiverKind,
    string MemberName);

internal readonly record struct StaticCallShapeContract(
    int MinArgumentCount,
    int? MaxArgumentCount,
    string[]? ParameterNames,
    int? MaxPositionalCount,
    bool AllowsExtraKeywords,
    bool AllowsExtraPositional,
    int PositionalOnlyCount)
{
    public StaticCallShapeContract(int MinArgumentCount, int? MaxArgumentCount)
        : this(MinArgumentCount, MaxArgumentCount, null, null, false, false, 0)
    {
    }

    public StaticCallShapeContract(int MinArgumentCount, int? MaxArgumentCount, string[]? ParameterNames)
        : this(MinArgumentCount, MaxArgumentCount, ParameterNames, null, false, false, 0)
    {
    }

    public StaticCallShapeContract(int MinArgumentCount, int? MaxArgumentCount, string[]? ParameterNames, int? MaxPositionalCount)
        : this(MinArgumentCount, MaxArgumentCount, ParameterNames, MaxPositionalCount, false, false, 0)
    {
    }

    public StaticCallShapeContract(int MinArgumentCount, int? MaxArgumentCount, string[]? ParameterNames, int? MaxPositionalCount, bool AllowsExtraKeywords)
        : this(MinArgumentCount, MaxArgumentCount, ParameterNames, MaxPositionalCount, AllowsExtraKeywords, false, 0)
    {
    }

    public StaticCallShapeContract(int MinArgumentCount, int? MaxArgumentCount, string[]? ParameterNames, int? MaxPositionalCount, bool AllowsExtraKeywords, bool AllowsExtraPositional)
        : this(MinArgumentCount, MaxArgumentCount, ParameterNames, MaxPositionalCount, AllowsExtraKeywords, AllowsExtraPositional, 0)
    {
    }

    public StaticCallShapeContract(LythonCallableSignature signature)
        : this(
            signature.MinimumArgumentCount,
            signature.MaximumArgumentCount,
            signature.ParameterNames,
            signature.AllowsExtraPositional ? null : signature.MaxPositionalCount ?? signature.ParameterNames?.Length,
            signature.AllowsExtraKeywords,
            signature.AllowsExtraPositional,
            signature.PositionalOnlyCount)
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

        if (!AllowsExtraPositional && MaxPositionalCount.HasValue && arguments.Positional.Count > MaxPositionalCount.Value)
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

        if (!AllowsExtraPositional && arguments.Positional.Count > ParameterNames.Length)
        {
            reason = "callable argument contract rejected too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var matchedKeywordCount = 0;
        for (var index = 0; index < ParameterNames.Length; index++)
        {
            var parameterName = ParameterNames[index];
            if (!arguments.Keywords.TryGetValue(parameterName, out var keywordExpression))
            {
                continue;
            }

            matchedKeywordCount++;
            if (index < PositionalOnlyCount)
            {
                reason = $"callable argument contract rejected positional-only keyword '{parameterName}'";
                offendingExpression = keywordExpression;
                return true;
            }

            if (index < arguments.Positional.Count)
            {
                reason = $"callable argument contract rejected duplicate binding for '{parameterName}'";
                offendingExpression = keywordExpression;
                return true;
            }
        }

        if (!AllowsExtraKeywords && matchedKeywordCount != arguments.Keywords.Count)
        {
            foreach (var keyword in arguments.Keywords)
            {
                // This second scan runs only for an invalid call. Valid calls bind in
                // O(parameters + keywords) without allocating an assignment bitmap.
                if (!ParameterNames.Contains(keyword.Key, StringComparer.Ordinal))
                {
                    reason = $"callable argument contract rejected unexpected keyword '{keyword.Key}'";
                    offendingExpression = keyword.Value;
                    return true;
                }
            }
        }

        for (var index = arguments.Positional.Count; index < MinArgumentCount; index++)
        {
            if (arguments.Keywords.ContainsKey(ParameterNames[index]))
            {
                continue;
            }

            reason = $"callable argument contract rejected missing required argument '{ParameterNames[index]}'";
            offendingExpression = null;
            return true;
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
    StaticMutationKind Mutation,
    string[]? ParameterNames,
    bool AllowsExtraKeywords)
{
    public StaticCallableContract(
        AbstractValueKind ReceiverKind,
        string MemberName,
        int MinArgumentCount,
        int? MaxArgumentCount,
        string DiagnosticCode,
        string Message)
        : this(ReceiverKind, MemberName, MinArgumentCount, MaxArgumentCount, DiagnosticCode, Message, StaticMutationKind.None, null, false)
    {
    }

    public StaticCallableContract(
        AbstractValueKind ReceiverKind,
        string MemberName,
        int MinArgumentCount,
        int? MaxArgumentCount,
        string DiagnosticCode,
        string Message,
        StaticMutationKind Mutation)
        : this(ReceiverKind, MemberName, MinArgumentCount, MaxArgumentCount, DiagnosticCode, Message, Mutation, null, false)
    {
    }

    public StaticCallableContract(
        AbstractValueKind ReceiverKind,
        string MemberName,
        int MinArgumentCount,
        int? MaxArgumentCount,
        string DiagnosticCode,
        string Message,
        string[]? ParameterNames)
        : this(ReceiverKind, MemberName, MinArgumentCount, MaxArgumentCount, DiagnosticCode, Message, StaticMutationKind.None, ParameterNames, false)
    {
    }

    public StaticCallableContract(
        AbstractValueKind ReceiverKind,
        string MemberName,
        int MinArgumentCount,
        int? MaxArgumentCount,
        string DiagnosticCode,
        string Message,
        StaticMutationKind Mutation,
        string[]? ParameterNames)
        : this(ReceiverKind, MemberName, MinArgumentCount, MaxArgumentCount, DiagnosticCode, Message, Mutation, ParameterNames, false)
    {
    }

    public bool AcceptsArgumentCount(int count)
        => Shape.AcceptsArgumentCount(count);

    public bool AcceptsArgumentShape(ConcreteCallArguments arguments)
        => Shape.AcceptsArgumentShape(arguments);

    public bool TryGetArgumentShapeFailure(
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
        => Shape.TryGetArgumentShapeFailure(arguments, out reason, out offendingExpression);

    private StaticCallShapeContract Shape => new(MinArgumentCount, MaxArgumentCount, ParameterNames, MaxPositionalCount: null, AllowsExtraKeywords: AllowsExtraKeywords);
}

internal readonly record struct StaticKnownCallContract(
    LythonCallableSignature Signature,
    string DiagnosticCode,
    string Message,
    StaticReturnShape ReturnShape)
{
    public StaticKnownCallContract(LythonCallableSignature Signature, string DiagnosticCode, string Message)
        : this(Signature, DiagnosticCode, Message, StaticReturnShape.Unknown)
    {
    }

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
