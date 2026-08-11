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

internal readonly record struct StaticCallShapeContract(CallableParameterLayout Parameters)
{
    public StaticCallShapeContract(LythonCallableSignature signature)
        : this(signature.Parameters)
    {
    }

    public static StaticCallShapeContract Positional(int minimumArgumentCount, ArgumentCountLimit maximumArgumentCount)
        => new(new PositionalCallableParameterLayout(minimumArgumentCount, maximumArgumentCount));

    public static StaticCallShapeContract Named(
        string callableName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string[] parameterNames,
        bool allowsExtraKeywords)
        => new(new NamedCallableParameterLayout(
            callableName,
            parameterNames,
            minimumArgumentCount,
            maximumArgumentCount,
            ArgumentCountLimit.AtMost(parameterNames.Length),
            allowsExtraKeywords ? LythonVariadicParameters.Keywords : LythonVariadicParameters.None,
            0));

    public bool AcceptsArgumentCount(int count)
        => count >= Parameters.MinimumArgumentCount && Parameters.MaximumArgumentCount.Accepts(count);

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

        if (Parameters is PositionalCallableParameterLayout)
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

        var named = (NamedCallableParameterLayout)Parameters;
        if (!named.MaximumPositionalArgumentCount.Accepts(arguments.Positional.Count))
        {
            reason = "callable argument contract rejected too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var matchedKeywordCount = 0;
        for (var index = 0; index < named.ParameterNames.Length; index++)
        {
            var parameterName = named.ParameterNames[index];
            if (!arguments.Keywords.TryGetValue(parameterName, out var keywordExpression))
            {
                continue;
            }

            matchedKeywordCount++;
            if (index < named.PositionalOnlyCount)
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

        if (!named.AllowsExtraKeywords && matchedKeywordCount != arguments.Keywords.Count)
        {
            foreach (var keyword in arguments.Keywords)
            {
                // This second scan runs only for an invalid call. Valid calls bind in
                // O(parameters + keywords) without allocating an assignment bitmap.
                if (!named.ParameterIndices.ContainsKey(keyword.Key))
                {
                    reason = $"callable argument contract rejected unexpected keyword '{keyword.Key}'";
                    offendingExpression = keyword.Value;
                    return true;
                }
            }
        }

        for (var index = arguments.Positional.Count; index < named.RequiredCount; index++)
        {
            if (arguments.Keywords.ContainsKey(named.ParameterNames[index]))
            {
                continue;
            }

            reason = $"callable argument contract rejected missing required argument '{named.ParameterNames[index]}'";
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
    string DiagnosticCode,
    string Message,
    StaticMutationKind Mutation,
    StaticCallShapeContract Shape)
{
    public StaticCallableContract(
        AbstractValueKind receiverKind,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string diagnosticCode,
        string message)
        : this(
            receiverKind,
            memberName,
            diagnosticCode,
            message,
            StaticMutationKind.None,
            StaticCallShapeContract.Positional(minimumArgumentCount, maximumArgumentCount))
    {
    }

    public StaticCallableContract(
        AbstractValueKind receiverKind,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string diagnosticCode,
        string message,
        StaticMutationKind mutation)
        : this(
            receiverKind,
            memberName,
            diagnosticCode,
            message,
            mutation,
            StaticCallShapeContract.Positional(minimumArgumentCount, maximumArgumentCount))
    {
    }

    public StaticCallableContract(
        AbstractValueKind receiverKind,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string diagnosticCode,
        string message,
        string[] parameterNames)
        : this(
            receiverKind,
            memberName,
            diagnosticCode,
            message,
            StaticMutationKind.None,
            StaticCallShapeContract.Named(memberName, minimumArgumentCount, maximumArgumentCount, parameterNames, false))
    {
    }

    public StaticCallableContract(
        AbstractValueKind receiverKind,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string diagnosticCode,
        string message,
        StaticMutationKind mutation,
        string[] parameterNames)
        : this(
            receiverKind,
            memberName,
            diagnosticCode,
            message,
            mutation,
            StaticCallShapeContract.Named(memberName, minimumArgumentCount, maximumArgumentCount, parameterNames, false))
    {
    }

    public StaticCallableContract(
        AbstractValueKind receiverKind,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string diagnosticCode,
        string message,
        StaticMutationKind mutation,
        string[] parameterNames,
        bool allowsExtraKeywords)
        : this(
            receiverKind,
            memberName,
            diagnosticCode,
            message,
            mutation,
            StaticCallShapeContract.Named(memberName, minimumArgumentCount, maximumArgumentCount, parameterNames, allowsExtraKeywords))
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
