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

internal readonly record struct AbstractValue(
    AbstractValueKind Kind,
    object Value,
    LythonSourceSpan Span)
{
    private static readonly LythonSourceSpan SyntheticSpan = new(0, 0, 0, 0);
    private static readonly IEqualityComparer<AbstractValue> LiteralKeyComparer = new AbstractLiteralKeyComparer();

    public static AbstractValue Unknown() => Unknown(SyntheticSpan);
    public static AbstractValue Unknown(LythonSourceSpan span) => new(AbstractValueKind.Unknown, "unknown", span);
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
    public static AbstractValue None(LythonSourceSpan span) => new(AbstractValueKind.None, new object(), span);
    public static AbstractValue MaybeNone(AbstractValue nonNoneValue, LythonSourceSpan span)
        => nonNoneValue.Kind switch
        {
            AbstractValueKind.None => None(span),
            AbstractValueKind.MaybeNone => nonNoneValue.WithSpan(span),
            _ => new(AbstractValueKind.MaybeNone, nonNoneValue.WithSpan(span), span)
        };
    public static AbstractValue ListOf(AbstractValue item, LythonSourceSpan span) => new(AbstractValueKind.ListType, item, span);
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

    public AbstractValue WithSpan(LythonSourceSpan span) => this with { Span = span };

    public static AbstractValue Join(AbstractValue left, AbstractValue right)
        => Join(left, right, left.Span);

    public static AbstractValue Join(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        // Join is the least upper bound used at control-flow merges. Preserve exact
        // literals only when both paths agree, then widen within a Python value family;
        // unrelated families deliberately become Unknown instead of inventing a union.
        if (left.Kind == AbstractValueKind.Never)
        {
            return right.WithSpan(span);
        }

        if (right.Kind == AbstractValueKind.Never)
        {
            return left.WithSpan(span);
        }

        if (left.Kind == AbstractValueKind.Unknown || right.Kind == AbstractValueKind.Unknown)
        {
            return Unknown(span);
        }

        if (left.Kind == AbstractValueKind.None)
        {
            return right.Kind switch
            {
                AbstractValueKind.None => None(span),
                AbstractValueKind.MaybeRegexMatch => right.WithSpan(span),
                _ => MaybeNone(right, span)
            };
        }

        if (right.Kind == AbstractValueKind.None)
        {
            return left.Kind == AbstractValueKind.MaybeRegexMatch
                ? left.WithSpan(span)
                : MaybeNone(left, span);
        }

        if (left.Kind == AbstractValueKind.MaybeNone || right.Kind == AbstractValueKind.MaybeNone)
        {
            var leftValue = left.Kind == AbstractValueKind.MaybeNone ? (AbstractValue)left.Value : left;
            var rightValue = right.Kind == AbstractValueKind.MaybeNone ? (AbstractValue)right.Value : right;
            return MaybeNone(Join(leftValue, rightValue, span), span);
        }

        if (left.Kind == right.Kind)
        {
            return left.Kind switch
            {
                AbstractValueKind.String => Equals(left.Value, right.Value) ? left.WithSpan(span) : StringType(span),
                AbstractValueKind.Bytes => LiteralValuesEqual(left, right) ? left.WithSpan(span) : BytesType(span),
                AbstractValueKind.Integer => Equals(left.Value, right.Value) ? left.WithSpan(span) : IntegerType(span),
                AbstractValueKind.Float => Equals(left.Value, right.Value) ? left.WithSpan(span) : FloatType(span),
                AbstractValueKind.Boolean => Equals(left.Value, right.Value) ? left.WithSpan(span) : BooleanType(span),
                AbstractValueKind.MaybeNone => MaybeNone(Join((AbstractValue)left.Value, (AbstractValue)right.Value, span), span),
                AbstractValueKind.List => JoinLiteralLists(left, right, span),
                AbstractValueKind.ListType => ListOf(Join((AbstractValue)left.Value, (AbstractValue)right.Value, span), span),
                AbstractValueKind.SetType => SetOf(Join((AbstractValue)left.Value, (AbstractValue)right.Value, span), span),
                AbstractValueKind.Dict => JoinLiteralDictionaries(),
                AbstractValueKind.TextFileHandle => TextFileHandle(JoinTextFileModes((AbstractTextFileMode)left.Value, (AbstractTextFileMode)right.Value), span),
                AbstractValueKind.Module => Equals(left.Value, right.Value) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.KnownCallable => Equals(left.Value, right.Value) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.RegexPattern => JoinRegexPatterns(left, right, span),
                AbstractValueKind.MaybeRegexMatch => JoinRegexMatches(left, right, span, maybe: true),
                AbstractValueKind.RegexMatch => JoinRegexMatches(left, right, span, maybe: false),
                AbstractValueKind.ArgparseParser => ArgparseParser(JoinArgparseParserSummaries((AbstractArgparseParserSummary)left.Value, (AbstractArgparseParserSummary)right.Value, span), span),
                AbstractValueKind.ArgparseNamespace => ArgparseNamespace(JoinArgparseNamespaceSummaries((AbstractArgparseNamespaceSummary)left.Value, (AbstractArgparseNamespaceSummary)right.Value, span), span),
                AbstractValueKind.ArgparseMutuallyExclusiveGroup => JoinArgparseGroups(left, right, span),
                AbstractValueKind.DataclassField => JoinDataclassFields(left, right, span),
                AbstractValueKind.Function => Equals(left.Value, right.Value) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.UserClass => Equals(left.Value, right.Value) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.UserInstance => JoinUserInstances(left, right, span),
                _ => left.WithSpan(span)
            };
        }

        if (left.IsStringLike && right.IsStringLike)
        {
            return StringType(span);
        }

        if (IsIntegerLike(left) && IsIntegerLike(right))
        {
            return IntegerType(span);
        }

        if (IsFloatLike(left) && IsFloatLike(right))
        {
            return FloatType(span);
        }

        if (IsBooleanLike(left) && IsBooleanLike(right))
        {
            return BooleanType(span);
        }

        if (IsBytesLike(left) && IsBytesLike(right))
        {
            return BytesType(span);
        }

        if (TryGetListElement(left, out var leftItem) && TryGetListElement(right, out var rightItem))
        {
            return ListOf(Join(leftItem, rightItem, span), span);
        }

        if (TryGetSetElement(left, out var leftSetItem) && TryGetSetElement(right, out var rightSetItem))
        {
            return SetOf(Join(leftSetItem, rightSetItem, span), span);
        }

        return Unknown(span);

        AbstractValue JoinLiteralDictionaries()
        {
            var leftPairs = (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)left.Value;
            var rightPairs = (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)right.Value;
            if (leftPairs.Count != rightPairs.Count)
            {
                return Unknown(span);
            }

            // A literal dictionary remains useful only when both paths have the same
            // comparable keys. Different shapes widen to Unknown because the analyzer
            // does not model optional dictionary entries.
            var rightValues = new Dictionary<AbstractValue, AbstractValue>(rightPairs.Count, LiteralKeyComparer);
            foreach (var rightPair in rightPairs)
            {
                if (IsComparableLiteralKey(rightPair.Key))
                {
                    rightValues.TryAdd(rightPair.Key, rightPair.Value);
                }
            }

            var joinedPairs = new List<KeyValuePair<AbstractValue, AbstractValue>>(leftPairs.Count);
            foreach (var leftPair in leftPairs)
            {
                if (!IsComparableLiteralKey(leftPair.Key) ||
                    !rightValues.TryGetValue(leftPair.Key, out var rightValue))
                {
                    return Unknown(span);
                }

                joinedPairs.Add(new KeyValuePair<AbstractValue, AbstractValue>(
                    leftPair.Key.WithSpan(span),
                    Join(leftPair.Value, rightValue, span)));
            }

            return Dict(joinedPairs, span);
        }
    }

    private static AbstractArgparseParserSummary JoinArgparseParserSummaries(
        AbstractArgparseParserSummary left,
        AbstractArgparseParserSummary right,
        LythonSourceSpan span)
        => new(JoinMemberMaps(left.Members, right.Members, span), left.IsSealed && right.IsSealed && HaveSameKeys(left.Members, right.Members));

    private static AbstractArgparseNamespaceSummary JoinArgparseNamespaceSummaries(
        AbstractArgparseNamespaceSummary left,
        AbstractArgparseNamespaceSummary right,
        LythonSourceSpan span)
        => new(JoinMemberMaps(left.Members, right.Members, span), left.IsSealed && right.IsSealed && HaveSameKeys(left.Members, right.Members));

    private static IReadOnlyDictionary<string, AbstractValue> JoinMemberMaps(
        IReadOnlyDictionary<string, AbstractValue> left,
        IReadOnlyDictionary<string, AbstractValue> right,
        LythonSourceSpan span)
    {
        var members = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        foreach (var (name, leftValue) in left)
        {
            if (right.TryGetValue(name, out var rightValue))
            {
                members[name] = Join(leftValue, rightValue, span);
            }
        }

        return members;
    }

    private static bool HaveSameKeys(
        IReadOnlyDictionary<string, AbstractValue> left,
        IReadOnlyDictionary<string, AbstractValue> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var name in left.Keys)
        {
            if (!right.ContainsKey(name))
            {
                return false;
            }
        }

        return true;
    }

    private static AbstractValue JoinDataclassFields(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftField = (AbstractDataclassFieldSummary)left.Value;
        var rightField = (AbstractDataclassFieldSummary)right.Value;
        return string.Equals(leftField.Name, rightField.Name, StringComparison.Ordinal)
            ? DataclassField(leftField.Name, span)
            : Unknown(span);
    }

    private static AbstractValue JoinArgparseGroups(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftGroup = (AbstractArgparseGroupSummary)left.Value;
        var rightGroup = (AbstractArgparseGroupSummary)right.Value;
        return string.Equals(leftGroup.ParserName, rightGroup.ParserName, StringComparison.Ordinal)
            ? ArgparseMutuallyExclusiveGroup(leftGroup.ParserName, span)
            : ArgparseMutuallyExclusiveGroup(span);
    }

    private static AbstractValue JoinUserInstances(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftInstance = (AbstractInstanceSummary)left.Value;
        var rightInstance = (AbstractInstanceSummary)right.Value;
        if (!Equals(leftInstance.Class, rightInstance.Class))
        {
            return Unknown(span);
        }

        var fields = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        foreach (var name in leftInstance.Fields.Keys.Concat(rightInstance.Fields.Keys).Distinct(StringComparer.Ordinal))
        {
            if (leftInstance.Fields.TryGetValue(name, out var leftField) &&
                rightInstance.Fields.TryGetValue(name, out var rightField))
            {
                fields[name] = Join(leftField, rightField, span);
            }
        }

        return fields.Count == 0
            ? Unknown(span)
            : UserInstance(new AbstractInstanceSummary(leftInstance.Class, fields, span), span);
    }

    private static AbstractValue JoinLiteralLists(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftItems = (IReadOnlyList<AbstractValue>)left.Value;
        var rightItems = (IReadOnlyList<AbstractValue>)right.Value;
        if (leftItems.Count == rightItems.Count)
        {
            var allSame = true;
            for (var i = 0; i < leftItems.Count; i++)
            {
                if (!Equals(leftItems[i], rightItems[i]))
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                return left.WithSpan(span);
            }
        }

        return ListOf(JoinListItems(leftItems, rightItems, span), span);
    }

    private static AbstractValue JoinRegexPatterns(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftSummary = (AbstractRegexPatternSummary)left.Value;
        var rightSummary = (AbstractRegexPatternSummary)right.Value;
        return RegexPatternSummariesEqual(leftSummary, rightSummary)
            ? RegexPattern(leftSummary, span)
            : RegexPattern(span);
    }

    private static AbstractValue JoinRegexMatches(AbstractValue left, AbstractValue right, LythonSourceSpan span, bool maybe)
    {
        var leftSummary = (AbstractRegexMatchSummary)left.Value;
        var rightSummary = (AbstractRegexMatchSummary)right.Value;
        if (!RegexMatchSummariesEqual(leftSummary, rightSummary))
        {
            return maybe ? MaybeRegexMatch(span) : RegexMatch(span);
        }

        return maybe ? MaybeRegexMatch(leftSummary, span) : RegexMatch(leftSummary, span);
    }

    private static AbstractValue JoinListItems(IReadOnlyList<AbstractValue> leftItems, IReadOnlyList<AbstractValue> rightItems, LythonSourceSpan span)
    {
        var result = Never(span);
        foreach (var item in leftItems)
        {
            result = Join(result, item, span);
        }

        foreach (var item in rightItems)
        {
            result = Join(result, item, span);
        }

        return result.Kind == AbstractValueKind.Never ? Unknown(span) : result;
    }

    private static bool TryGetListElement(AbstractValue value, out AbstractValue item)
    {
        if (value.Kind == AbstractValueKind.ListType)
        {
            item = (AbstractValue)value.Value;
            return true;
        }

        if (value.Kind == AbstractValueKind.List)
        {
            item = JoinListItems((IReadOnlyList<AbstractValue>)value.Value, Array.Empty<AbstractValue>(), value.Span);
            return true;
        }

        item = default;
        return false;
    }

    private static bool TryGetSetElement(AbstractValue value, out AbstractValue item)
    {
        if (value.Kind == AbstractValueKind.SetType)
        {
            item = (AbstractValue)value.Value;
            return true;
        }

        if (value.Kind == AbstractValueKind.Set)
        {
            item = JoinListItems((IReadOnlyList<AbstractValue>)value.Value, Array.Empty<AbstractValue>(), value.Span);
            return true;
        }

        item = default;
        return false;
    }

    private static AbstractTextFileMode JoinTextFileModes(AbstractTextFileMode left, AbstractTextFileMode right)
        => left == right ? left : AbstractTextFileMode.Unknown;

    private static bool IsIntegerLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or AbstractValueKind.IntegerType;

    private static bool IsFloatLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Float or AbstractValueKind.FloatType;

    private static bool IsBooleanLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Boolean or AbstractValueKind.BooleanType;

    private static bool IsBytesLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Bytes or AbstractValueKind.BytesType;

    private static AbstractRegexPatternSummary CreateUnknownRegexPatternSummary()
        => new(null, null, new Dictionary<string, int>(StringComparer.Ordinal));

    private static AbstractRegexMatchSummary CreateUnknownRegexMatchSummary()
        => new(null, new Dictionary<string, int>(StringComparer.Ordinal));

    private static bool RegexPatternSummariesEqual(AbstractRegexPatternSummary left, AbstractRegexPatternSummary right)
        => string.Equals(left.PatternText, right.PatternText, StringComparison.Ordinal) &&
           left.CaptureSlotCount == right.CaptureSlotCount &&
           IntegerMapsEqual(left.NamedGroups, right.NamedGroups);

    private static bool RegexMatchSummariesEqual(AbstractRegexMatchSummary left, AbstractRegexMatchSummary right)
        => left.CaptureSlotCount == right.CaptureSlotCount &&
           IntegerMapsEqual(left.NamedGroups, right.NamedGroups);

    private static bool IntegerMapsEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue) || value != rightValue)
            {
                return false;
            }
        }

        return true;
    }

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
            AbstractValueKind.Boolean => Equals(left.Value, right.Value),
            AbstractValueKind.Bytes => ((byte[])left.Value).AsSpan().SequenceEqual((byte[])right.Value),
            AbstractValueKind.None => true,
            _ => false
        };
    }

    public static bool TryGetExactSequenceLength(AbstractValue value, out int length)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.String:
                length = ((string)value.Value).Length;
                return true;
            case AbstractValueKind.Bytes:
                length = ((byte[])value.Value).Length;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                length = ((IReadOnlyList<AbstractValue>)value.Value).Count;
                return true;
            default:
                length = 0;
                return false;
        }
    }

    private static bool IsComparableLiteralKey(AbstractValue value)
        => value.Kind is AbstractValueKind.String or
            AbstractValueKind.Integer or
            AbstractValueKind.Float or
            AbstractValueKind.Boolean or
            AbstractValueKind.Bytes or
            AbstractValueKind.None;

    private sealed class AbstractLiteralKeyComparer : IEqualityComparer<AbstractValue>
    {
        public bool Equals(AbstractValue left, AbstractValue right) => LiteralValuesEqual(left, right);

        public int GetHashCode(AbstractValue value)
        {
            var hash = new HashCode();
            hash.Add(value.Kind);
            if (value.Kind == AbstractValueKind.Bytes)
            {
                hash.AddBytes((byte[])value.Value);
            }
            else if (value.Kind != AbstractValueKind.None)
            {
                hash.Add(value.Value);
            }

            return hash.ToHashCode();
        }
    }

}
