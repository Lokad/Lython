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
    private static readonly object NonePayload = new();
    private readonly object? _payload;

    private AbstractValue(AbstractValueKind kind, object? payload, LythonSourceSpan span)
    {
        Kind = kind;
        _payload = payload;
        Span = span;
    }

    public AbstractValueKind Kind { get; }

    public LythonSourceSpan Span { get; }

    public T RequirePayload<T>() where T : notnull
        => _payload is T payload
            ? payload
            : throw new InvalidOperationException($"Abstract value {Kind} does not carry a {typeof(T).Name} payload.");

    public bool HasSamePayload(AbstractValue other) => Equals(_payload, other._payload);

    public static AbstractValue Unknown() => Unknown(SyntheticSpan);
    public static AbstractValue Unknown(LythonSourceSpan span) => new(AbstractValueKind.Unknown, null, span);
    public static AbstractValue Never() => Never(SyntheticSpan);
    public static AbstractValue Never(LythonSourceSpan span) => new(AbstractValueKind.Never, "never", span);
    public static AbstractValue String(string value, LythonSourceSpan span) => new(AbstractValueKind.String, value, span);
    public static AbstractValue StringType(LythonSourceSpan span) => new(AbstractValueKind.StringType, "str", span);
    public static AbstractValue Bytes(byte[] value, LythonSourceSpan span) => new(AbstractValueKind.Bytes, value, span);
    public static AbstractValue BytesType(LythonSourceSpan span) => new(AbstractValueKind.BytesType, "bytes", span);
    public static AbstractValue Integer(string valueText, LythonSourceSpan span) => new(AbstractValueKind.Integer, valueText, span);
    public static AbstractValue IntegerType(LythonSourceSpan span) => new(AbstractValueKind.IntegerType, "int", span);
    public static AbstractValue Float(string valueText, LythonSourceSpan span) => new(AbstractValueKind.Float, valueText, span);
    public static AbstractValue FloatType(LythonSourceSpan span) => new(AbstractValueKind.FloatType, "float", span);
    public static AbstractValue Boolean(bool value, LythonSourceSpan span) => new(AbstractValueKind.Boolean, value, span);
    public static AbstractValue BooleanType(LythonSourceSpan span) => new(AbstractValueKind.BooleanType, "bool", span);
    public static AbstractValue None(LythonSourceSpan span) => new(AbstractValueKind.None, NonePayload, span);
    public static AbstractValue MaybeNone(AbstractValue nonNoneValue, LythonSourceSpan span)
        => nonNoneValue.Kind switch
        {
            AbstractValueKind.None => None(span),
            AbstractValueKind.MaybeNone => nonNoneValue.WithSpan(span),
            _ => new(AbstractValueKind.MaybeNone, nonNoneValue.WithSpan(span), span)
        };
    public static AbstractValue ListOf(AbstractValue item, LythonSourceSpan span) => new(AbstractValueKind.ListType, item, span);
    public static AbstractValue List(IReadOnlyList<AbstractValue> items, LythonSourceSpan span) => new(AbstractValueKind.List, items, span);
    public static AbstractValue Tuple(IReadOnlyList<AbstractValue> items, LythonSourceSpan span) => new(AbstractValueKind.Tuple, items, span);
    public static AbstractValue Set(IReadOnlyList<AbstractValue> items, LythonSourceSpan span) => new(AbstractValueKind.Set, items, span);
    public static AbstractValue SetOf(AbstractValue item, LythonSourceSpan span) => new(AbstractValueKind.SetType, item, span);
    public static AbstractValue Dict(IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>> pairs, LythonSourceSpan span) => new(AbstractValueKind.Dict, pairs, span);
    public static AbstractValue Path(LythonSourceSpan span) => new(AbstractValueKind.Path, "pathlib.Path", span);
    public static AbstractValue TextFileHandle(AbstractTextFileMode mode, LythonSourceSpan span) => new(AbstractValueKind.TextFileHandle, mode, span);
    public static AbstractValue Module(string name, LythonSourceSpan span) => new(AbstractValueKind.Module, name, span);
    public static AbstractValue KnownCallable(string targetName, LythonSourceSpan span) => new(AbstractValueKind.KnownCallable, targetName, span);
    public static AbstractValue RegexPattern(LythonSourceSpan span) => RegexPattern(CreateUnknownRegexPatternSummary(), span);
    public static AbstractValue RegexPattern(AbstractRegexPatternSummary summary, LythonSourceSpan span) => new(AbstractValueKind.RegexPattern, summary, span);
    public static AbstractValue MaybeRegexMatch(LythonSourceSpan span) => MaybeRegexMatch(CreateUnknownRegexMatchSummary(), span);
    public static AbstractValue MaybeRegexMatch(AbstractRegexMatchSummary summary, LythonSourceSpan span) => new(AbstractValueKind.MaybeRegexMatch, summary, span);
    public static AbstractValue RegexMatch(LythonSourceSpan span) => RegexMatch(CreateUnknownRegexMatchSummary(), span);
    public static AbstractValue RegexMatch(AbstractRegexMatchSummary summary, LythonSourceSpan span) => new(AbstractValueKind.RegexMatch, summary, span);
    public static AbstractValue ArgparseParser(LythonSourceSpan span) => ArgparseParser(new AbstractArgparseParserSummary(new Dictionary<string, AbstractValue>(StringComparer.Ordinal), IsSealed: true), span);
    public static AbstractValue ArgparseParser(AbstractArgparseParserSummary summary, LythonSourceSpan span) => new(AbstractValueKind.ArgparseParser, summary, span);
    public static AbstractValue ArgparseMutuallyExclusiveGroup(LythonSourceSpan span) => ArgparseMutuallyExclusiveGroup(string.Empty, span);
    public static AbstractValue ArgparseMutuallyExclusiveGroup(string parserName, LythonSourceSpan span) => new(AbstractValueKind.ArgparseMutuallyExclusiveGroup, new AbstractArgparseGroupSummary(parserName), span);
    public static AbstractValue ArgparseNamespace(LythonSourceSpan span) => ArgparseNamespace(new AbstractArgparseNamespaceSummary(new Dictionary<string, AbstractValue>(StringComparer.Ordinal), IsSealed: false), span);
    public static AbstractValue ArgparseNamespace(AbstractArgparseNamespaceSummary summary, LythonSourceSpan span) => new(AbstractValueKind.ArgparseNamespace, summary, span);
    public static AbstractValue CsvReader(LythonSourceSpan span) => new(AbstractValueKind.CsvReader, "csv.reader", span);
    public static AbstractValue CsvDictReader(LythonSourceSpan span) => new(AbstractValueKind.CsvDictReader, "csv.DictReader", span);
    public static AbstractValue CsvWriter(LythonSourceSpan span) => new(AbstractValueKind.CsvWriter, "csv.writer", span);
    public static AbstractValue CsvDictWriter(LythonSourceSpan span) => new(AbstractValueKind.CsvDictWriter, "csv.DictWriter", span);
    public static AbstractValue CollectionsDefaultDict(LythonSourceSpan span) => new(AbstractValueKind.CollectionsDefaultDict, "collections.defaultdict", span);
    public static AbstractValue CollectionsCounter(LythonSourceSpan span) => new(AbstractValueKind.CollectionsCounter, "collections.Counter", span);
    public static AbstractValue CollectionsDeque(LythonSourceSpan span) => new(AbstractValueKind.CollectionsDeque, "collections.deque", span);
    public static AbstractValue CollectionsChainMap(LythonSourceSpan span) => new(AbstractValueKind.CollectionsChainMap, "collections.ChainMap", span);
    public static AbstractValue Decimal(LythonSourceSpan span) => new(AbstractValueKind.Decimal, "decimal.Decimal", span);
    public static AbstractValue DecimalContext(LythonSourceSpan span) => new(AbstractValueKind.DecimalContext, "decimal.Context", span);
    public static AbstractValue DecimalTuple(LythonSourceSpan span) => new(AbstractValueKind.DecimalTuple, "decimal.DecimalTuple", span);
    public static AbstractValue DateTimeTimedelta(LythonSourceSpan span) => new(AbstractValueKind.DateTimeTimedelta, "datetime.timedelta", span);
    public static AbstractValue DateTimeDate(LythonSourceSpan span) => new(AbstractValueKind.DateTimeDate, "datetime.date", span);
    public static AbstractValue DateTimeTime(LythonSourceSpan span) => new(AbstractValueKind.DateTimeTime, "datetime.time", span);
    public static AbstractValue DateTimeDateTime(LythonSourceSpan span) => new(AbstractValueKind.DateTimeDateTime, "datetime.datetime", span);
    public static AbstractValue DateTimeTimezone(LythonSourceSpan span) => new(AbstractValueKind.DateTimeTimezone, "datetime.timezone", span);
    public static AbstractValue StatisticsLinearRegression(LythonSourceSpan span) => new(AbstractValueKind.StatisticsLinearRegression, "statistics.LinearRegression", span);
    public static AbstractValue StatisticsNormalDist(LythonSourceSpan span) => new(AbstractValueKind.StatisticsNormalDist, "statistics.NormalDist", span);
    public static AbstractValue Random(LythonSourceSpan span) => new(AbstractValueKind.Random, "random.Random", span);
    public static AbstractValue DifflibDiffer(LythonSourceSpan span) => new(AbstractValueKind.DifflibDiffer, "difflib.Differ", span);
    public static AbstractValue DifflibHtmlDiff(LythonSourceSpan span) => new(AbstractValueKind.DifflibHtmlDiff, "difflib.HtmlDiff", span);
    public static AbstractValue DifflibMatch(LythonSourceSpan span) => new(AbstractValueKind.DifflibMatch, "difflib.Match", span);
    public static AbstractValue DifflibSequenceMatcher(LythonSourceSpan span) => new(AbstractValueKind.DifflibSequenceMatcher, "difflib.SequenceMatcher", span);
    public static AbstractValue PkgutilModuleInfo(LythonSourceSpan span) => new(AbstractValueKind.PkgutilModuleInfo, "pkgutil.ModuleInfo", span);
    public static AbstractValue PkgutilLoader(LythonSourceSpan span) => new(AbstractValueKind.PkgutilLoader, "pkgutil.Loader", span);
    public static AbstractValue SubprocessCompletedProcess(LythonSourceSpan span) => new(AbstractValueKind.SubprocessCompletedProcess, "subprocess.CompletedProcess", span);
    public static AbstractValue SubprocessPopen(LythonSourceSpan span) => new(AbstractValueKind.SubprocessPopen, "subprocess.Popen", span);
    public static AbstractValue DataclassField(string name, LythonSourceSpan span) => new(AbstractValueKind.DataclassField, new AbstractDataclassFieldSummary(name, span), span);
    public static AbstractValue OpenPyxlWorkbook(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlWorkbook, "openpyxl.Workbook", span);
    public static AbstractValue OpenPyxlWorksheet(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlWorksheet, "openpyxl.worksheet.worksheet.Worksheet", span);
    public static AbstractValue OpenPyxlCell(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlCell, "openpyxl.cell.cell.Cell", span);
    public static AbstractValue OpenPyxlHyperlink(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlHyperlink, "openpyxl.worksheet.hyperlink.Hyperlink", span);
    public static AbstractValue OpenPyxlComment(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlComment, "openpyxl.comments.Comment", span);
    public static AbstractValue OpenPyxlFont(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlFont, "openpyxl.styles.Font", span);
    public static AbstractValue OpenPyxlPatternFill(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlPatternFill, "openpyxl.styles.PatternFill", span);
    public static AbstractValue OpenPyxlBorder(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlBorder, "openpyxl.styles.Border", span);
    public static AbstractValue OpenPyxlSide(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlSide, "openpyxl.styles.Side", span);
    public static AbstractValue OpenPyxlAlignment(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlAlignment, "openpyxl.styles.Alignment", span);
    public static AbstractValue OpenPyxlProtection(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlProtection, "openpyxl.styles.Protection", span);
    public static AbstractValue OpenPyxlNamedStyle(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlNamedStyle, "openpyxl.styles.NamedStyle", span);
    public static AbstractValue OpenPyxlColor(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlColor, "openpyxl.styles.colors.Color", span);
    public static AbstractValue OpenPyxlTable(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlTable, "openpyxl.worksheet.table.Table", span);
    public static AbstractValue OpenPyxlTableStyleInfo(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlTableStyleInfo, "openpyxl.worksheet.table.TableStyleInfo", span);
    public static AbstractValue OpenPyxlDataValidation(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlDataValidation, "openpyxl.worksheet.datavalidation.DataValidation", span);
    public static AbstractValue OpenPyxlConditionalFormattingRule(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlConditionalFormattingRule, "openpyxl.formatting.rule.Rule", span);
    public static AbstractValue OpenPyxlAutoFilter(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlAutoFilter, "openpyxl.worksheet.filters.AutoFilter", span);
    public static AbstractValue OpenPyxlSheetProtection(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlSheetProtection, "openpyxl.worksheet.protection.SheetProtection", span);
    public static AbstractValue OpenPyxlWorkbookProtection(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlWorkbookProtection, "openpyxl.workbook.protection.WorkbookProtection", span);
    public static AbstractValue OpenPyxlDrawing(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlDrawing, "openpyxl.drawing.spreadsheet_drawing.SpreadsheetDrawing", span);
    public static AbstractValue OpenPyxlChart(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlChart, "openpyxl.chart._chart.ChartBase", span);
    public static AbstractValue OpenPyxlImage(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlImage, "openpyxl.drawing.image.Image", span);
    public static AbstractValue OpenPyxlSheetView(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlSheetView, "openpyxl.worksheet.views.SheetView", span);
    public static AbstractValue OpenPyxlSelection(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlSelection, "openpyxl.worksheet.views.Selection", span);
    public static AbstractValue OpenPyxlPageMargins(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlPageMargins, "openpyxl.worksheet.page.PageMargins", span);
    public static AbstractValue OpenPyxlPageSetup(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlPageSetup, "openpyxl.worksheet.page.PrintPageSetup", span);
    public static AbstractValue OpenPyxlTableCollection(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlTableCollection, "openpyxl.worksheet.table.TableList", span);
    public static AbstractValue OpenPyxlDataValidationList(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlDataValidationList, "openpyxl.worksheet.datavalidation.DataValidationList", span);
    public static AbstractValue OpenPyxlConditionalFormattingCollection(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlConditionalFormattingCollection, "openpyxl.formatting.formatting.ConditionalFormattingList", span);
    public static AbstractValue OpenPyxlColumnDimension(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlColumnDimension, "openpyxl.worksheet.dimensions.ColumnDimension", span);
    public static AbstractValue OpenPyxlRowDimension(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlRowDimension, "openpyxl.worksheet.dimensions.RowDimension", span);
    public static AbstractValue OpenPyxlMergedCellSet(LythonSourceSpan span) => new(AbstractValueKind.OpenPyxlMergedCellSet, "openpyxl.worksheet.cell_range.MultiCellRange", span);
    public static AbstractValue Function(AbstractFunctionSummary summary, LythonSourceSpan span) => new(AbstractValueKind.Function, summary, span);
    public static AbstractValue UserClass(AbstractClassSummary summary, LythonSourceSpan span) => new(AbstractValueKind.UserClass, summary, span);
    public static AbstractValue UserInstance(AbstractInstanceSummary summary, LythonSourceSpan span) => new(AbstractValueKind.UserInstance, summary, span);

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
            AbstractValueKind.Bytes => (left.RequirePayload<byte[]>()).AsSpan().SequenceEqual(right.RequirePayload<byte[]>()),
            AbstractValueKind.None => true,
            _ => false
        };
    }

    public static bool TryGetExactSequenceLength(AbstractValue value, out int length)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.String:
                length = (value.RequirePayload<string>()).Length;
                return true;
            case AbstractValueKind.Bytes:
                length = (value.RequirePayload<byte[]>()).Length;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                length = (value.RequirePayload<IReadOnlyList<AbstractValue>>()).Count;
                return true;
            default:
                length = 0;
                return false;
        }
    }

}
