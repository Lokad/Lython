namespace Lokad.Lython.Frontend;

internal enum AbstractValueKind
{
    Unknown,
    Never,
    String,
    StringType,
    Bytes,
    BytesType,
    Integer,
    IntegerType,
    Float,
    FloatType,
    Boolean,
    BooleanType,
    None,
    Ellipsis,
    MaybeNone,
    List,
    ListType,
    Tuple,
    Dict,
    Set,
    SetType,
    Path,
    TextFileHandle,
    Module,
    KnownCallable,
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
    SubprocessCompletedProcess,
    SubprocessPopen,
    DataclassField,
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
    Function,
    UserClass,
    UserInstance,
}

internal enum AbstractTextFileMode
{
    Unknown,
    Read,
    Write,
    Append,
}

internal sealed record AbstractFunctionSummary(
    IReadOnlyList<FunctionParameterSyntax> Parameters,
    IReadOnlyList<StatementSyntax> Body,
    AbstractState CapturedBindings,
    LythonSourceSpan Span);

internal sealed record AbstractArgparseParserSummary(
    IReadOnlyDictionary<string, AbstractValue> Members,
    bool IsSealed);

internal sealed record AbstractArgparseNamespaceSummary(
    IReadOnlyDictionary<string, AbstractValue> Members,
    bool IsSealed);

internal sealed record AbstractArgparseGroupSummary(
    string ParserName);

internal sealed record AbstractRegexPatternSummary(
    string? PatternText,
    int? CaptureSlotCount,
    IReadOnlyDictionary<string, int> NamedGroups);

internal sealed record AbstractRegexMatchSummary(
    int? CaptureSlotCount,
    IReadOnlyDictionary<string, int> NamedGroups);

internal sealed record AbstractClassSummary(
    string Name,
    IReadOnlyList<AbstractClassFieldSummary> Fields,
    IReadOnlyDictionary<string, AbstractFunctionSummary> Methods,
    bool IsDataclass,
    LythonSourceSpan Span)
{
    public IReadOnlyDictionary<string, AbstractClassFieldSummary> FieldsByName { get; } =
        Fields.ToDictionary(static field => field.Name, StringComparer.Ordinal);

    public AbstractClassFieldSummary[] InitFields { get; } =
        Fields.Where(static field => field.IncludeInInit).ToArray();

    public AbstractClassFieldSummary[] PositionalInitFields { get; } =
        Fields.Where(static field => field.IncludeInInit && !field.KeywordOnly).ToArray();

    public AbstractClassFieldSummary[] StoredFields { get; } =
        Fields.Where(static field => field.StoreOnInstance).ToArray();
}

internal readonly record struct AbstractClassFieldSummary(
    string Name,
    AbstractValue DefaultValue,
    bool HasDefault,
    bool IncludeInInit,
    bool KeywordOnly,
    bool StoreOnInstance,
    LythonSourceSpan Span);

internal sealed record AbstractInstanceSummary(
    AbstractClassSummary Class,
    IReadOnlyDictionary<string, AbstractValue> Fields,
    LythonSourceSpan Span);

internal sealed record AbstractDataclassFieldSummary(
    string Name,
    LythonSourceSpan Span);

internal readonly record struct AbstractValue
{
    private static readonly LythonSourceSpan SyntheticSpan = new(0, 0, 0, 0);
    private readonly AbstractPayload _payload;

    private AbstractValue(AbstractValueKind kind, AbstractPayload payload, LythonSourceSpan span)
    {
        Kind = kind;
        _payload = payload;
        Span = span;
    }

    public AbstractValueKind Kind { get; }

    public LythonSourceSpan Span { get; }

    public string RequireText() => _payload is TextPayload payload ? payload.Value : ThrowPayloadMismatch<string>();
    public byte[] RequireBytes() => _payload is BytesPayload payload ? payload.Value : ThrowPayloadMismatch<byte[]>();
    public bool RequireBoolean() => _payload is BooleanPayload payload ? payload.Value : ThrowPayloadMismatch<bool>();
    public AbstractValue RequireNestedValue() => _payload is NestedValuePayload payload ? payload.Value : ThrowPayloadMismatch<AbstractValue>();
    public IReadOnlyList<AbstractValue> RequireSequenceItems() => _payload is SequencePayload payload ? payload.Items : ThrowPayloadMismatch<IReadOnlyList<AbstractValue>>();
    public IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>> RequireDictionaryItems() => _payload is DictionaryPayload payload ? payload.Items : ThrowPayloadMismatch<IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>>();
    public AbstractTextFileMode RequireTextFileMode() => _payload is TextFileModePayload payload ? payload.Mode : ThrowPayloadMismatch<AbstractTextFileMode>();
    public AbstractRegexPatternSummary RequireRegexPatternSummary() => _payload is RegexPatternPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractRegexPatternSummary>();
    public AbstractRegexMatchSummary RequireRegexMatchSummary() => _payload is RegexMatchPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractRegexMatchSummary>();
    public AbstractArgparseParserSummary RequireArgparseParserSummary() => _payload is ArgparseParserPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractArgparseParserSummary>();
    public AbstractArgparseNamespaceSummary RequireArgparseNamespaceSummary() => _payload is ArgparseNamespacePayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractArgparseNamespaceSummary>();
    public AbstractArgparseGroupSummary RequireArgparseGroupSummary() => _payload is ArgparseGroupPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractArgparseGroupSummary>();
    public AbstractDataclassFieldSummary RequireDataclassFieldSummary() => _payload is DataclassFieldPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractDataclassFieldSummary>();
    public AbstractFunctionSummary RequireFunctionSummary() => _payload is FunctionPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractFunctionSummary>();
    public AbstractClassSummary RequireClassSummary() => _payload is ClassPayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractClassSummary>();
    public AbstractInstanceSummary RequireInstanceSummary() => _payload is InstancePayload payload ? payload.Summary : ThrowPayloadMismatch<AbstractInstanceSummary>();

    public bool HasSamePayload(AbstractValue other) => Equals(_payload, other._payload);

    public static AbstractValue Unknown() => Unknown(SyntheticSpan);
    public static AbstractValue Unknown(LythonSourceSpan span) => Marker(AbstractValueKind.Unknown, span);
    public static AbstractValue Never() => Never(SyntheticSpan);
    public static AbstractValue Never(LythonSourceSpan span) => Marker(AbstractValueKind.Never, span);
    public static AbstractValue String(string value, LythonSourceSpan span) => new(AbstractValueKind.String, new TextPayload(value), span);
    public static AbstractValue StringType(LythonSourceSpan span) => Marker(AbstractValueKind.StringType, span);
    public static AbstractValue Bytes(byte[] value, LythonSourceSpan span) => new(AbstractValueKind.Bytes, new BytesPayload(value), span);
    public static AbstractValue BytesType(LythonSourceSpan span) => Marker(AbstractValueKind.BytesType, span);
    public static AbstractValue Integer(string valueText, LythonSourceSpan span) => new(AbstractValueKind.Integer, new TextPayload(valueText), span);
    public static AbstractValue IntegerType(LythonSourceSpan span) => Marker(AbstractValueKind.IntegerType, span);
    public static AbstractValue Float(string valueText, LythonSourceSpan span) => new(AbstractValueKind.Float, new TextPayload(valueText), span);
    public static AbstractValue FloatType(LythonSourceSpan span) => Marker(AbstractValueKind.FloatType, span);
    public static AbstractValue Boolean(bool value, LythonSourceSpan span) => new(AbstractValueKind.Boolean, new BooleanPayload(value), span);
    public static AbstractValue BooleanType(LythonSourceSpan span) => Marker(AbstractValueKind.BooleanType, span);
    public static AbstractValue None(LythonSourceSpan span) => Marker(AbstractValueKind.None, span);
    public static AbstractValue Ellipsis(LythonSourceSpan span) => Marker(AbstractValueKind.Ellipsis, span);
    public static AbstractValue MaybeNone(AbstractValue nonNoneValue, LythonSourceSpan span)
        => nonNoneValue.Kind switch
        {
            AbstractValueKind.None => None(span),
            AbstractValueKind.MaybeNone => nonNoneValue.WithSpan(span),
            _ => new(AbstractValueKind.MaybeNone, new NestedValuePayload(nonNoneValue.WithSpan(span)), span)
        };
    public static AbstractValue ListOf(AbstractValue item, LythonSourceSpan span) => new(AbstractValueKind.ListType, new NestedValuePayload(item), span);
    public static AbstractValue List(IReadOnlyList<AbstractValue> items, LythonSourceSpan span) => new(AbstractValueKind.List, new SequencePayload(items), span);
    public static AbstractValue Tuple(IReadOnlyList<AbstractValue> items, LythonSourceSpan span) => new(AbstractValueKind.Tuple, new SequencePayload(items), span);
    public static AbstractValue Set(IReadOnlyList<AbstractValue> items, LythonSourceSpan span) => new(AbstractValueKind.Set, new SequencePayload(items), span);
    public static AbstractValue SetOf(AbstractValue item, LythonSourceSpan span) => new(AbstractValueKind.SetType, new NestedValuePayload(item), span);
    public static AbstractValue Dict(IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>> pairs, LythonSourceSpan span) => new(AbstractValueKind.Dict, new DictionaryPayload(pairs), span);
    public static AbstractValue Path(LythonSourceSpan span) => Marker(AbstractValueKind.Path, span);
    public static AbstractValue TextFileHandle(AbstractTextFileMode mode, LythonSourceSpan span) => new(AbstractValueKind.TextFileHandle, new TextFileModePayload(mode), span);
    public static AbstractValue Module(string name, LythonSourceSpan span) => new(AbstractValueKind.Module, new TextPayload(name), span);
    public static AbstractValue KnownCallable(string targetName, LythonSourceSpan span) => new(AbstractValueKind.KnownCallable, new TextPayload(targetName), span);
    public static AbstractValue RegexPattern(LythonSourceSpan span) => RegexPattern(CreateUnknownRegexPatternSummary(), span);
    public static AbstractValue RegexPattern(AbstractRegexPatternSummary summary, LythonSourceSpan span) => new(AbstractValueKind.RegexPattern, new RegexPatternPayload(summary), span);
    public static AbstractValue MaybeRegexMatch(LythonSourceSpan span) => MaybeRegexMatch(CreateUnknownRegexMatchSummary(), span);
    public static AbstractValue MaybeRegexMatch(AbstractRegexMatchSummary summary, LythonSourceSpan span) => new(AbstractValueKind.MaybeRegexMatch, new RegexMatchPayload(summary), span);
    public static AbstractValue RegexMatch(LythonSourceSpan span) => RegexMatch(CreateUnknownRegexMatchSummary(), span);
    public static AbstractValue RegexMatch(AbstractRegexMatchSummary summary, LythonSourceSpan span) => new(AbstractValueKind.RegexMatch, new RegexMatchPayload(summary), span);
    public static AbstractValue ArgparseParser(LythonSourceSpan span) => ArgparseParser(new AbstractArgparseParserSummary(new Dictionary<string, AbstractValue>(StringComparer.Ordinal), IsSealed: true), span);
    public static AbstractValue ArgparseParser(AbstractArgparseParserSummary summary, LythonSourceSpan span) => new(AbstractValueKind.ArgparseParser, new ArgparseParserPayload(summary), span);
    public static AbstractValue ArgparseMutuallyExclusiveGroup(LythonSourceSpan span) => ArgparseMutuallyExclusiveGroup(string.Empty, span);
    public static AbstractValue ArgparseMutuallyExclusiveGroup(string parserName, LythonSourceSpan span) => new(AbstractValueKind.ArgparseMutuallyExclusiveGroup, new ArgparseGroupPayload(new AbstractArgparseGroupSummary(parserName)), span);
    public static AbstractValue ArgparseNamespace(LythonSourceSpan span) => ArgparseNamespace(new AbstractArgparseNamespaceSummary(new Dictionary<string, AbstractValue>(StringComparer.Ordinal), IsSealed: false), span);
    public static AbstractValue ArgparseNamespace(AbstractArgparseNamespaceSummary summary, LythonSourceSpan span) => new(AbstractValueKind.ArgparseNamespace, new ArgparseNamespacePayload(summary), span);
    public static AbstractValue CsvReader(LythonSourceSpan span) => Marker(AbstractValueKind.CsvReader, span);
    public static AbstractValue CsvDictReader(LythonSourceSpan span) => Marker(AbstractValueKind.CsvDictReader, span);
    public static AbstractValue CsvWriter(LythonSourceSpan span) => Marker(AbstractValueKind.CsvWriter, span);
    public static AbstractValue CsvDictWriter(LythonSourceSpan span) => Marker(AbstractValueKind.CsvDictWriter, span);
    public static AbstractValue CollectionsDefaultDict(LythonSourceSpan span) => Marker(AbstractValueKind.CollectionsDefaultDict, span);
    public static AbstractValue CollectionsCounter(LythonSourceSpan span) => Marker(AbstractValueKind.CollectionsCounter, span);
    public static AbstractValue CollectionsDeque(LythonSourceSpan span) => Marker(AbstractValueKind.CollectionsDeque, span);
    public static AbstractValue CollectionsChainMap(LythonSourceSpan span) => Marker(AbstractValueKind.CollectionsChainMap, span);
    public static AbstractValue Decimal(LythonSourceSpan span) => Marker(AbstractValueKind.Decimal, span);
    public static AbstractValue DecimalContext(LythonSourceSpan span) => Marker(AbstractValueKind.DecimalContext, span);
    public static AbstractValue DecimalTuple(LythonSourceSpan span) => Marker(AbstractValueKind.DecimalTuple, span);
    public static AbstractValue DateTimeTimedelta(LythonSourceSpan span) => Marker(AbstractValueKind.DateTimeTimedelta, span);
    public static AbstractValue DateTimeDate(LythonSourceSpan span) => Marker(AbstractValueKind.DateTimeDate, span);
    public static AbstractValue DateTimeTime(LythonSourceSpan span) => Marker(AbstractValueKind.DateTimeTime, span);
    public static AbstractValue DateTimeDateTime(LythonSourceSpan span) => Marker(AbstractValueKind.DateTimeDateTime, span);
    public static AbstractValue DateTimeTimezone(LythonSourceSpan span) => Marker(AbstractValueKind.DateTimeTimezone, span);
    public static AbstractValue StatisticsLinearRegression(LythonSourceSpan span) => Marker(AbstractValueKind.StatisticsLinearRegression, span);
    public static AbstractValue StatisticsNormalDist(LythonSourceSpan span) => Marker(AbstractValueKind.StatisticsNormalDist, span);
    public static AbstractValue Random(LythonSourceSpan span) => Marker(AbstractValueKind.Random, span);
    public static AbstractValue DifflibDiffer(LythonSourceSpan span) => Marker(AbstractValueKind.DifflibDiffer, span);
    public static AbstractValue DifflibHtmlDiff(LythonSourceSpan span) => Marker(AbstractValueKind.DifflibHtmlDiff, span);
    public static AbstractValue DifflibMatch(LythonSourceSpan span) => Marker(AbstractValueKind.DifflibMatch, span);
    public static AbstractValue DifflibSequenceMatcher(LythonSourceSpan span) => Marker(AbstractValueKind.DifflibSequenceMatcher, span);
    public static AbstractValue PkgutilModuleInfo(LythonSourceSpan span) => Marker(AbstractValueKind.PkgutilModuleInfo, span);
    public static AbstractValue PkgutilLoader(LythonSourceSpan span) => Marker(AbstractValueKind.PkgutilLoader, span);
    public static AbstractValue SubprocessCompletedProcess(LythonSourceSpan span) => Marker(AbstractValueKind.SubprocessCompletedProcess, span);
    public static AbstractValue SubprocessPopen(LythonSourceSpan span) => Marker(AbstractValueKind.SubprocessPopen, span);
    public static AbstractValue DataclassField(string name, LythonSourceSpan span) => new(AbstractValueKind.DataclassField, new DataclassFieldPayload(new AbstractDataclassFieldSummary(name, span)), span);
    public static AbstractValue OpenPyxlWorkbook(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlWorkbook, span);
    public static AbstractValue OpenPyxlWorksheet(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlWorksheet, span);
    public static AbstractValue OpenPyxlCell(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlCell, span);
    public static AbstractValue OpenPyxlHyperlink(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlHyperlink, span);
    public static AbstractValue OpenPyxlComment(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlComment, span);
    public static AbstractValue OpenPyxlFont(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlFont, span);
    public static AbstractValue OpenPyxlPatternFill(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlPatternFill, span);
    public static AbstractValue OpenPyxlBorder(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlBorder, span);
    public static AbstractValue OpenPyxlSide(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlSide, span);
    public static AbstractValue OpenPyxlAlignment(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlAlignment, span);
    public static AbstractValue OpenPyxlProtection(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlProtection, span);
    public static AbstractValue OpenPyxlNamedStyle(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlNamedStyle, span);
    public static AbstractValue OpenPyxlColor(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlColor, span);
    public static AbstractValue OpenPyxlTable(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlTable, span);
    public static AbstractValue OpenPyxlTableStyleInfo(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlTableStyleInfo, span);
    public static AbstractValue OpenPyxlDataValidation(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlDataValidation, span);
    public static AbstractValue OpenPyxlConditionalFormattingRule(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlConditionalFormattingRule, span);
    public static AbstractValue OpenPyxlAutoFilter(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlAutoFilter, span);
    public static AbstractValue OpenPyxlSheetProtection(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlSheetProtection, span);
    public static AbstractValue OpenPyxlWorkbookProtection(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlWorkbookProtection, span);
    public static AbstractValue OpenPyxlDrawing(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlDrawing, span);
    public static AbstractValue OpenPyxlChart(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlChart, span);
    public static AbstractValue OpenPyxlImage(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlImage, span);
    public static AbstractValue OpenPyxlSheetView(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlSheetView, span);
    public static AbstractValue OpenPyxlSelection(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlSelection, span);
    public static AbstractValue OpenPyxlPageMargins(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlPageMargins, span);
    public static AbstractValue OpenPyxlPageSetup(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlPageSetup, span);
    public static AbstractValue OpenPyxlTableCollection(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlTableCollection, span);
    public static AbstractValue OpenPyxlDataValidationList(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlDataValidationList, span);
    public static AbstractValue OpenPyxlConditionalFormattingCollection(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlConditionalFormattingCollection, span);
    public static AbstractValue OpenPyxlColumnDimension(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlColumnDimension, span);
    public static AbstractValue OpenPyxlRowDimension(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlRowDimension, span);
    public static AbstractValue OpenPyxlMergedCellSet(LythonSourceSpan span) => Marker(AbstractValueKind.OpenPyxlMergedCellSet, span);
    public static AbstractValue Function(AbstractFunctionSummary summary, LythonSourceSpan span) => new(AbstractValueKind.Function, new FunctionPayload(summary), span);
    public static AbstractValue UserClass(AbstractClassSummary summary, LythonSourceSpan span) => new(AbstractValueKind.UserClass, new ClassPayload(summary), span);
    public static AbstractValue UserInstance(AbstractInstanceSummary summary, LythonSourceSpan span) => new(AbstractValueKind.UserInstance, new InstancePayload(summary), span);

    public bool IsLiteralLike =>
        Kind is AbstractValueKind.String or
            AbstractValueKind.Bytes or
            AbstractValueKind.Integer or
            AbstractValueKind.Float or
            AbstractValueKind.Boolean or
            AbstractValueKind.None or
            AbstractValueKind.List or
            AbstractValueKind.Tuple or
            AbstractValueKind.Dict or
            AbstractValueKind.Set;

    public bool IsStringLike => Kind is AbstractValueKind.String or AbstractValueKind.StringType;

    public bool IsDefinitelyNonStringLike =>
        Kind is not AbstractValueKind.Unknown and
            not AbstractValueKind.Never and
            not AbstractValueKind.MaybeNone and
            not AbstractValueKind.String and
            not AbstractValueKind.StringType;

    public AbstractValue WithSpan(LythonSourceSpan span) => new(Kind, _payload, span);

    public static AbstractValue Join(AbstractValue left, AbstractValue right)
        => AbstractValueJoin.Join(left, right, left.Span);

    public static AbstractValue Join(AbstractValue left, AbstractValue right, LythonSourceSpan span)
        => AbstractValueJoin.Join(left, right, span);

    private static AbstractRegexPatternSummary CreateUnknownRegexPatternSummary()
        => new(null, null, new Dictionary<string, int>(StringComparer.Ordinal));

    private static AbstractRegexMatchSummary CreateUnknownRegexMatchSummary()
        => new(null, new Dictionary<string, int>(StringComparer.Ordinal));

    public static bool LiteralValuesEqual(AbstractValue left, AbstractValue right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            AbstractValueKind.String or
            AbstractValueKind.Integer or
            AbstractValueKind.Float or
            AbstractValueKind.Boolean => left.HasSamePayload(right),
            AbstractValueKind.Bytes => left.RequireBytes().AsSpan().SequenceEqual(right.RequireBytes()),
            AbstractValueKind.None => true,
            _ => false
        };
    }

    public static bool TryGetExactSequenceLength(AbstractValue value, out int length)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.String:
                length = value.RequireText().Length;
                return true;
            case AbstractValueKind.Bytes:
                length = value.RequireBytes().Length;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                length = value.RequireSequenceItems().Count;
                return true;
            default:
                length = 0;
                return false;
        }
    }

    private static AbstractValue Marker(AbstractValueKind kind, LythonSourceSpan span)
        => new(kind, MarkerPayload.Instance, span);

    private T ThrowPayloadMismatch<T>()
        => throw new InvalidOperationException($"Abstract value {Kind} does not carry a {typeof(T).Name} payload.");

    private abstract record AbstractPayload;

    private sealed record MarkerPayload : AbstractPayload
    {
        public static MarkerPayload Instance { get; } = new();
    }

    private sealed record TextPayload(string Value) : AbstractPayload;
    private sealed record BytesPayload(byte[] Value) : AbstractPayload;
    private sealed record BooleanPayload(bool Value) : AbstractPayload;
    private sealed record NestedValuePayload(AbstractValue Value) : AbstractPayload;
    private sealed record SequencePayload(IReadOnlyList<AbstractValue> Items) : AbstractPayload;
    private sealed record DictionaryPayload(IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>> Items) : AbstractPayload;
    private sealed record TextFileModePayload(AbstractTextFileMode Mode) : AbstractPayload;
    private sealed record RegexPatternPayload(AbstractRegexPatternSummary Summary) : AbstractPayload;
    private sealed record RegexMatchPayload(AbstractRegexMatchSummary Summary) : AbstractPayload;
    private sealed record ArgparseParserPayload(AbstractArgparseParserSummary Summary) : AbstractPayload;
    private sealed record ArgparseNamespacePayload(AbstractArgparseNamespaceSummary Summary) : AbstractPayload;
    private sealed record ArgparseGroupPayload(AbstractArgparseGroupSummary Summary) : AbstractPayload;
    private sealed record DataclassFieldPayload(AbstractDataclassFieldSummary Summary) : AbstractPayload;
    private sealed record FunctionPayload(AbstractFunctionSummary Summary) : AbstractPayload;
    private sealed record ClassPayload(AbstractClassSummary Summary) : AbstractPayload;
    private sealed record InstancePayload(AbstractInstanceSummary Summary) : AbstractPayload;

}
