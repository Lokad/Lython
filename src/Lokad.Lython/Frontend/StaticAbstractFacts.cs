using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static class StaticAbstractFacts
{
    public static bool TryGetDateTimeBinaryResultKind(
        BinaryOperatorSyntax op,
        AbstractValue left,
        AbstractValue right,
        out AbstractValueKind resultKind)
    {
        resultKind = op switch
        {
            BinaryOperatorSyntax.Add when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Add when left.Kind == AbstractValueKind.DateTimeDate && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeDate,
            BinaryOperatorSyntax.Add when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeDate => AbstractValueKind.DateTimeDate,
            BinaryOperatorSyntax.Add when left.Kind == AbstractValueKind.DateTimeDateTime && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeDateTime,
            BinaryOperatorSyntax.Add when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeDateTime => AbstractValueKind.DateTimeDateTime,
            BinaryOperatorSyntax.Subtract when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Subtract when left.Kind == AbstractValueKind.DateTimeDate && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeDate,
            BinaryOperatorSyntax.Subtract when left.Kind == AbstractValueKind.DateTimeDate && right.Kind == AbstractValueKind.DateTimeDate => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Subtract when left.Kind == AbstractValueKind.DateTimeDateTime && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeDateTime,
            BinaryOperatorSyntax.Subtract when left.Kind == AbstractValueKind.DateTimeDateTime && right.Kind == AbstractValueKind.DateTimeDateTime => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Multiply when left.Kind == AbstractValueKind.DateTimeTimedelta && IsNumericLike(right) => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Multiply when IsNumericLike(left) && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Divide when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.FloatType,
            BinaryOperatorSyntax.Divide when left.Kind == AbstractValueKind.DateTimeTimedelta && IsNumericLike(right) => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.FloorDivide when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.IntegerType,
            BinaryOperatorSyntax.FloorDivide when left.Kind == AbstractValueKind.DateTimeTimedelta && IsNumericLike(right) => AbstractValueKind.DateTimeTimedelta,
            BinaryOperatorSyntax.Modulo when left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeTimedelta => AbstractValueKind.DateTimeTimedelta,
            _ => default
        };

        return resultKind != default;
    }

    public static bool IsNormalDistAdditivePair(AbstractValue left, AbstractValue right)
        => left.Kind == AbstractValueKind.StatisticsNormalDist &&
            (right.Kind == AbstractValueKind.StatisticsNormalDist || IsNumericLike(right)) ||
           (IsNumericLike(left) || left.Kind == AbstractValueKind.StatisticsNormalDist) &&
            right.Kind == AbstractValueKind.StatisticsNormalDist;

    public static bool IsNormalDistNumericPair(AbstractValue left, AbstractValue right)
        => left.Kind == AbstractValueKind.StatisticsNormalDist && IsNumericLike(right) ||
           IsNumericLike(left) && right.Kind == AbstractValueKind.StatisticsNormalDist;

    public static bool IsDefinitelyNonCallable(AbstractValue value)
        => value.Kind == AbstractValueKind.MaybeNone
            ? IsDefinitelyNonCallable(value.RequireNestedValue())
            : AbstractValueTraitFacts.Has(value.Kind, AbstractValueTraits.DefinitelyNonCallable);

    public static bool IsKnownIntegerLiteral(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolveKnownValue(expression, bindings, out var value) &&
           value.Kind == AbstractValueKind.Integer;

    public static bool IsKnownBooleanLiteral(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolveKnownValue(expression, bindings, out var value) &&
           value.Kind == AbstractValueKind.Boolean;

    public static bool IsDefinitelyKnownLiteral(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.IsDefinitelyKnownLiteral(expression, bindings);

    public static bool IsDefinitelyKnownNonCallableLiteral(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolveKnownValue(expression, bindings, out var value) &&
           value.IsLiteralLike &&
           value.Kind != AbstractValueKind.None;

    public static bool IsDefinitelyKnownNonIterableLiteral(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.IsDefinitelyKnownNonIterableLiteral(expression, bindings);

    public static bool IsDefinitelyKnownNonIterable(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) && IsDefinitelyNonIterable(value);

    public static bool IsDefinitelyNonIterable(AbstractValue value)
        => value.Kind == AbstractValueKind.MaybeNone
            ? IsDefinitelyNonIterable(value.RequireNestedValue())
            : AbstractValueTraitFacts.Has(value.Kind, AbstractValueTraits.DefinitelyNonIterable);

    public static bool IsDefinitelyKnownNonSized(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) && IsDefinitelyNonSized(value);

    public static bool IsDefinitelyNonSized(AbstractValue value)
        => value.Kind == AbstractValueKind.MaybeNone
            ? IsDefinitelyNonSized(value.RequireNestedValue())
            : AbstractValueTraitFacts.Has(value.Kind, AbstractValueTraits.DefinitelyNonSized);

    public static bool IsDefinitelySized(AbstractValue value)
        => AbstractValueTraitFacts.Has(value.Kind, AbstractValueTraits.DefinitelySized);

    public static bool IsDefinitelyNonSubscriptable(AbstractValue value)
        => value.Kind == AbstractValueKind.MaybeNone
            ? IsDefinitelyNonSubscriptable(value.RequireNestedValue())
            : AbstractValueTraitFacts.Has(value.Kind, AbstractValueTraits.DefinitelyNonSubscriptable);

    public static bool IsDefinitelyNonSliceable(AbstractValue value)
        => IsDefinitelyNonSubscriptable(value) ||
           value.Kind == AbstractValueKind.Dict;

    public static bool RequiresIntegerIndex(AbstractValue value)
        => value.Kind is AbstractValueKind.String or
            AbstractValueKind.StringType or
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.CollectionsDeque or
            AbstractValueKind.DecimalTuple or
            AbstractValueKind.StatisticsLinearRegression or
            AbstractValueKind.DifflibMatch or
            AbstractValueKind.PkgutilModuleInfo;

    public static bool IsDefinitelyNonIntegerLike(AbstractValue value)
        => value.Kind is not AbstractValueKind.Unknown and
            not AbstractValueKind.Never and
            not AbstractValueKind.MaybeNone and
            not AbstractValueKind.Integer and
            not AbstractValueKind.IntegerType and
            not AbstractValueKind.Boolean and
            not AbstractValueKind.BooleanType;

    public static bool IsIntegerLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType;

    public static bool IsStrictIntegerLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or AbstractValueKind.IntegerType;

    public static bool IsFloatLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Float or AbstractValueKind.FloatType;

    public static bool IsBooleanLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Boolean or AbstractValueKind.BooleanType;

    public static bool IsBytesLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Bytes or AbstractValueKind.BytesType;

    public static bool IsNumericLike(AbstractValue value)
        => IsIntegerLike(value) || IsFloatLike(value) || value.Kind == AbstractValueKind.Decimal;

    public static bool IsListLike(AbstractValue value)
        => value.Kind is AbstractValueKind.List or AbstractValueKind.ListType;

    public static bool IsTupleLike(AbstractValue value)
        => value.Kind == AbstractValueKind.Tuple;

    public static bool IsSetLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Set or AbstractValueKind.SetType;

    public static bool IsDefinitelyNonNone(AbstractValue value)
        => value.Kind is not AbstractValueKind.Unknown and
            not AbstractValueKind.Never and
            not AbstractValueKind.MaybeNone and
            not AbstractValueKind.MaybeRegexMatch and
            not AbstractValueKind.None;

    public static bool TryGetTruthiness(AbstractValue value, out bool truth)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.None:
                truth = false;
                return true;
            case AbstractValueKind.Boolean:
                truth = value.RequireBoolean();
                return true;
            case AbstractValueKind.String:
                truth = (value.RequireText()).Length != 0;
                return true;
            case AbstractValueKind.Bytes:
                truth = (value.RequireBytes()).Length != 0;
                return true;
            case AbstractValueKind.Integer when System.Numerics.BigInteger.TryParse(
                (value.RequireText()).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer):
                truth = !integer.IsZero;
                return true;
            case AbstractValueKind.Float when double.TryParse(
                (value.RequireText()).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var floating):
                truth = floating != 0.0;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            case AbstractValueKind.Set:
                truth = (value.RequireSequenceItems()).Count != 0;
                return true;
            case AbstractValueKind.Dict:
                truth = (value.RequireDictionaryItems()).Count != 0;
                return true;
            case AbstractValueKind.RegexMatch:
                truth = true;
                return true;
            case AbstractValueKind.MaybeNone when TryGetTruthiness(value.RequireNestedValue(), out var innerTruth) && !innerTruth:
                truth = false;
                return true;
            default:
                truth = false;
                return false;
        }
    }

    public static bool TryGetNonNegativeInt32(AbstractValue value, out int integer)
    {
        if (TryGetInt32(value, out integer) && integer >= 0)
        {
            return true;
        }

        integer = 0;
        return false;
    }

    public static bool TryGetInt32(AbstractValue value, out int integer)
    {
        if (value.Kind == AbstractValueKind.Integer &&
            int.TryParse(
                (value.RequireText()).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out integer))
        {
            return true;
        }

        integer = 0;
        return false;
    }

    public static bool TryGetKnownSequenceArity(ExpressionSyntax expression, AbstractState bindings, out int count)
    {
        if (StaticAbstractValueResolver.TryResolveKnownValue(expression, bindings, out var value))
        {
            return TryGetKnownSequenceArity(value, out count);
        }

        switch (expression)
        {
            case StringLiteralExpressionSyntax text:
                count = text.Value.Length;
                return true;
            case BytesLiteralExpressionSyntax bytes:
                count = bytes.Value.Length;
                return true;
            case ListLiteralExpressionSyntax { HasUnpacking: false } list:
                count = list.Items.Count;
                return true;
            case TupleLiteralExpressionSyntax { HasUnpacking: false } tuple:
                count = tuple.Items.Count;
                return true;
            case SetLiteralExpressionSyntax { HasUnpacking: false } set:
                count = set.Items.Count;
                return true;
            default:
                count = 0;
                return false;
        }
    }

    public static bool TryGetKnownSequenceArity(AbstractValue value, out int count)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.String:
                count = (value.RequireText()).Length;
                return true;
            case AbstractValueKind.Bytes:
                count = (value.RequireBytes()).Length;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            case AbstractValueKind.Set:
                count = (value.RequireSequenceItems()).Count;
                return true;
            case AbstractValueKind.DecimalTuple:
                count = 3;
                return true;
            case AbstractValueKind.DifflibMatch:
            case AbstractValueKind.PkgutilModuleInfo:
                count = 3;
                return true;
            default:
                count = 0;
                return false;
        }
    }

    public static bool TryFindNonStringItem(IReadOnlyList<AbstractValue> items, out LythonSourceSpan span)
    {
        foreach (var item in items)
        {
            if (item.Kind != AbstractValueKind.String)
            {
                span = item.Span;
                return true;
            }
        }

        span = new LythonSourceSpan(0, 0, 0, 0);
        return false;
    }

    public static bool TryFindNonStringItem(IReadOnlyList<ExpressionSyntax> items, AbstractState bindings, out LythonSourceSpan span)
    {
        foreach (var item in items)
        {
            if (!StaticAbstractValueResolver.TryResolveKnownString(item, bindings, out _) &&
                IsDefinitelyKnownLiteral(item, bindings))
            {
                span = item.Span;
                return true;
            }
        }

        span = new LythonSourceSpan(0, 0, 0, 0);
        return false;
    }

    public static string DescribeValue(AbstractValue value)
    {
        return value.Kind switch
        {
            AbstractValueKind.String => "str",
            AbstractValueKind.StringType => "str",
            AbstractValueKind.Bytes => "bytes",
            AbstractValueKind.BytesType => "bytes",
            AbstractValueKind.Integer => "int",
            AbstractValueKind.IntegerType => "int",
            AbstractValueKind.Float => "float",
            AbstractValueKind.FloatType => "float",
            AbstractValueKind.Boolean => "bool",
            AbstractValueKind.BooleanType => "bool",
            AbstractValueKind.None => "NoneType",
            AbstractValueKind.List => "list",
            AbstractValueKind.ListType => "list",
            AbstractValueKind.Tuple => "tuple",
            AbstractValueKind.Dict => "dict",
            AbstractValueKind.Set => "set",
            AbstractValueKind.SetType => "set",
            AbstractValueKind.Path => "pathlib.Path",
            AbstractValueKind.TextFileHandle => "file",
            AbstractValueKind.Module => $"module '{value.RequireText()}'",
            AbstractValueKind.KnownCallable => $"callable '{value.RequireText()}'",
            AbstractValueKind.RegexPattern => "re.Pattern",
            AbstractValueKind.MaybeNone => DescribeValue(value.RequireNestedValue()) + " | None",
            AbstractValueKind.MaybeRegexMatch => "re.Match | None",
            AbstractValueKind.RegexMatch => "re.Match",
            AbstractValueKind.ArgparseParser => "argparse.ArgumentParser",
            AbstractValueKind.ArgparseMutuallyExclusiveGroup => "argparse._MutuallyExclusiveGroup",
            AbstractValueKind.ArgparseNamespace => "argparse.Namespace",
            AbstractValueKind.CsvReader => "csv.reader",
            AbstractValueKind.CsvDictReader => "csv.DictReader",
            AbstractValueKind.CsvWriter => "csv.writer",
            AbstractValueKind.CsvDictWriter => "csv.DictWriter",
            AbstractValueKind.CollectionsDefaultDict => "collections.defaultdict",
            AbstractValueKind.CollectionsCounter => "collections.Counter",
            AbstractValueKind.CollectionsDeque => "collections.deque",
            AbstractValueKind.CollectionsChainMap => "collections.ChainMap",
            AbstractValueKind.Decimal => "decimal.Decimal",
            AbstractValueKind.DecimalContext => "decimal.Context",
            AbstractValueKind.DecimalTuple => "decimal.DecimalTuple",
            AbstractValueKind.DateTimeTimedelta => "datetime.timedelta",
            AbstractValueKind.DateTimeDate => "datetime.date",
            AbstractValueKind.DateTimeTime => "datetime.time",
            AbstractValueKind.DateTimeDateTime => "datetime.datetime",
            AbstractValueKind.DateTimeTimezone => "datetime.timezone",
            AbstractValueKind.StatisticsLinearRegression => "statistics.LinearRegression",
            AbstractValueKind.StatisticsNormalDist => "statistics.NormalDist",
            AbstractValueKind.Random => "random.Random",
            AbstractValueKind.DifflibDiffer => "difflib.Differ",
            AbstractValueKind.DifflibHtmlDiff => "difflib.HtmlDiff",
            AbstractValueKind.DifflibMatch => "difflib.Match",
            AbstractValueKind.DifflibSequenceMatcher => "difflib.SequenceMatcher",
            AbstractValueKind.PkgutilModuleInfo => "pkgutil.ModuleInfo",
            AbstractValueKind.PkgutilLoader => "pkgutil.Loader",
            AbstractValueKind.SubprocessCompletedProcess => "subprocess.CompletedProcess",
            AbstractValueKind.SubprocessPopen => "subprocess.Popen",
            AbstractValueKind.DataclassField => "dataclasses.Field",
            AbstractValueKind.OpenPyxlWorkbook => "openpyxl.Workbook",
            AbstractValueKind.OpenPyxlWorksheet => "openpyxl.worksheet.worksheet.Worksheet",
            AbstractValueKind.OpenPyxlCell => "openpyxl.cell.cell.Cell",
            AbstractValueKind.OpenPyxlHyperlink => "openpyxl.worksheet.hyperlink.Hyperlink",
            AbstractValueKind.OpenPyxlComment => "openpyxl.comments.Comment",
            AbstractValueKind.OpenPyxlFont => "openpyxl.styles.Font",
            AbstractValueKind.OpenPyxlPatternFill => "openpyxl.styles.PatternFill",
            AbstractValueKind.OpenPyxlBorder => "openpyxl.styles.Border",
            AbstractValueKind.OpenPyxlSide => "openpyxl.styles.Side",
            AbstractValueKind.OpenPyxlAlignment => "openpyxl.styles.Alignment",
            AbstractValueKind.OpenPyxlProtection => "openpyxl.styles.Protection",
            AbstractValueKind.OpenPyxlNamedStyle => "openpyxl.styles.NamedStyle",
            AbstractValueKind.OpenPyxlColor => "openpyxl.styles.colors.Color",
            AbstractValueKind.OpenPyxlTable => "openpyxl.worksheet.table.Table",
            AbstractValueKind.OpenPyxlTableStyleInfo => "openpyxl.worksheet.table.TableStyleInfo",
            AbstractValueKind.OpenPyxlDataValidation => "openpyxl.worksheet.datavalidation.DataValidation",
            AbstractValueKind.OpenPyxlConditionalFormattingRule => "openpyxl.formatting.rule.Rule",
            AbstractValueKind.OpenPyxlAutoFilter => "openpyxl.worksheet.filters.AutoFilter",
            AbstractValueKind.OpenPyxlSheetProtection => "openpyxl.worksheet.protection.SheetProtection",
            AbstractValueKind.OpenPyxlWorkbookProtection => "openpyxl.workbook.protection.WorkbookProtection",
            AbstractValueKind.OpenPyxlDrawing => "openpyxl.drawing.spreadsheet_drawing.SpreadsheetDrawing",
            AbstractValueKind.OpenPyxlChart => "openpyxl.chart._chart.ChartBase",
            AbstractValueKind.OpenPyxlImage => "openpyxl.drawing.image.Image",
            AbstractValueKind.OpenPyxlSheetView => "openpyxl.worksheet.views.SheetView",
            AbstractValueKind.OpenPyxlSelection => "openpyxl.worksheet.views.Selection",
            AbstractValueKind.OpenPyxlPageMargins => "openpyxl.worksheet.page.PageMargins",
            AbstractValueKind.OpenPyxlPageSetup => "openpyxl.worksheet.page.PrintPageSetup",
            AbstractValueKind.OpenPyxlTableCollection => "openpyxl.worksheet.table.TableList",
            AbstractValueKind.OpenPyxlDataValidationList => "openpyxl.worksheet.datavalidation.DataValidationList",
            AbstractValueKind.OpenPyxlConditionalFormattingCollection => "openpyxl.formatting.formatting.ConditionalFormattingList",
            AbstractValueKind.OpenPyxlColumnDimension => "openpyxl.worksheet.dimensions.ColumnDimension",
            AbstractValueKind.OpenPyxlRowDimension => "openpyxl.worksheet.dimensions.RowDimension",
            AbstractValueKind.OpenPyxlMergedCellSet => "openpyxl.worksheet.cell_range.MultiCellRange",
            AbstractValueKind.Function => "function",
            AbstractValueKind.UserClass => (value.RequireClassSummary()).Name,
            AbstractValueKind.UserInstance => (value.RequireInstanceSummary()).Class.Name,
            _ => "object"
        };
    }
}
