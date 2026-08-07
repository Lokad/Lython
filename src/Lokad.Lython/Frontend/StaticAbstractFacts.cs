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
            ? IsDefinitelyNonCallable((AbstractValue)value.Value)
            : (value.Kind is AbstractValueKind.String or
            AbstractValueKind.StringType or
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.Dict or
            AbstractValueKind.Set or
            AbstractValueKind.SetType or
            AbstractValueKind.Path or
            AbstractValueKind.TextFileHandle or
            AbstractValueKind.Module or
            AbstractValueKind.RegexPattern or
            AbstractValueKind.MaybeRegexMatch or
            AbstractValueKind.RegexMatch or
            AbstractValueKind.ArgparseParser or
            AbstractValueKind.ArgparseMutuallyExclusiveGroup or
            AbstractValueKind.ArgparseNamespace or
            AbstractValueKind.CsvReader or
            AbstractValueKind.CsvDictReader or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.CsvDictWriter or
            AbstractValueKind.Decimal or
            AbstractValueKind.DecimalContext or
            AbstractValueKind.DecimalTuple or
            AbstractValueKind.DateTimeTimedelta or
            AbstractValueKind.DateTimeDate or
            AbstractValueKind.DateTimeTime or
            AbstractValueKind.DateTimeDateTime or
            AbstractValueKind.DateTimeTimezone or
            AbstractValueKind.StatisticsLinearRegression or
            AbstractValueKind.StatisticsNormalDist or
            AbstractValueKind.Random or
            AbstractValueKind.DifflibDiffer or
            AbstractValueKind.DifflibHtmlDiff or
            AbstractValueKind.DifflibMatch or
            AbstractValueKind.DifflibSequenceMatcher or
            AbstractValueKind.PkgutilModuleInfo or
            AbstractValueKind.PkgutilLoader or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.SubprocessPopen or
            AbstractValueKind.DataclassField or
            AbstractValueKind.OpenPyxlWorkbook or
            AbstractValueKind.OpenPyxlWorksheet or
            AbstractValueKind.OpenPyxlConditionalFormattingRule or
            AbstractValueKind.OpenPyxlTableCollection or
            AbstractValueKind.OpenPyxlDataValidationList or
            AbstractValueKind.OpenPyxlMergedCellSet) ||
            IsOpenPyxlMemberOnlyValue(value.Kind);

    public static bool IsKnownIntegerLiteral(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolveKnownValue(expression, bindings, out var value) &&
           value.Kind == AbstractValueKind.Integer;

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
            ? IsDefinitelyNonIterable((AbstractValue)value.Value)
            : (value.Kind is AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.Path or
            AbstractValueKind.Module or
            AbstractValueKind.KnownCallable or
            AbstractValueKind.RegexPattern or
            AbstractValueKind.MaybeRegexMatch or
            AbstractValueKind.RegexMatch or
            AbstractValueKind.ArgparseParser or
            AbstractValueKind.ArgparseMutuallyExclusiveGroup or
            AbstractValueKind.ArgparseNamespace or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.CsvDictWriter or
            AbstractValueKind.Decimal or
            AbstractValueKind.DecimalContext or
            AbstractValueKind.DateTimeTimedelta or
            AbstractValueKind.DateTimeDate or
            AbstractValueKind.DateTimeTime or
            AbstractValueKind.DateTimeDateTime or
            AbstractValueKind.DateTimeTimezone or
            AbstractValueKind.StatisticsNormalDist or
            AbstractValueKind.Random or
            AbstractValueKind.DifflibDiffer or
            AbstractValueKind.DifflibHtmlDiff or
            AbstractValueKind.DifflibSequenceMatcher or
            AbstractValueKind.PkgutilLoader or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.SubprocessPopen or
            AbstractValueKind.DataclassField or
            AbstractValueKind.OpenPyxlWorksheet or
            AbstractValueKind.OpenPyxlConditionalFormattingRule or
            AbstractValueKind.Function) ||
            IsOpenPyxlMemberOnlyValue(value.Kind);

    public static bool IsDefinitelyKnownNonSized(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) && IsDefinitelyNonSized(value);

    public static bool IsDefinitelyNonSized(AbstractValue value)
        => value.Kind == AbstractValueKind.MaybeNone
            ? IsDefinitelyNonSized((AbstractValue)value.Value)
            : (value.Kind is AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.Path or
            AbstractValueKind.TextFileHandle or
            AbstractValueKind.Module or
            AbstractValueKind.KnownCallable or
            AbstractValueKind.RegexPattern or
            AbstractValueKind.MaybeRegexMatch or
            AbstractValueKind.RegexMatch or
            AbstractValueKind.ArgparseParser or
            AbstractValueKind.ArgparseMutuallyExclusiveGroup or
            AbstractValueKind.ArgparseNamespace or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.CsvDictWriter or
            AbstractValueKind.Decimal or
            AbstractValueKind.DecimalContext or
            AbstractValueKind.DateTimeTimedelta or
            AbstractValueKind.DateTimeDate or
            AbstractValueKind.DateTimeTime or
            AbstractValueKind.DateTimeDateTime or
            AbstractValueKind.DateTimeTimezone or
            AbstractValueKind.StatisticsNormalDist or
            AbstractValueKind.Random or
            AbstractValueKind.DifflibDiffer or
            AbstractValueKind.DifflibHtmlDiff or
            AbstractValueKind.DifflibSequenceMatcher or
            AbstractValueKind.PkgutilLoader or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.SubprocessPopen or
            AbstractValueKind.DataclassField or
            AbstractValueKind.OpenPyxlWorkbook or
            AbstractValueKind.OpenPyxlWorksheet or
            AbstractValueKind.OpenPyxlTableCollection or
            AbstractValueKind.OpenPyxlDataValidationList or
            AbstractValueKind.OpenPyxlConditionalFormattingCollection or
            AbstractValueKind.OpenPyxlMergedCellSet or
            AbstractValueKind.Function) ||
            IsOpenPyxlMemberOnlyValue(value.Kind);

    public static bool IsDefinitelySized(AbstractValue value)
        => value.Kind is AbstractValueKind.String or
            AbstractValueKind.StringType or
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.StatisticsLinearRegression or
            AbstractValueKind.DifflibMatch or
            AbstractValueKind.PkgutilModuleInfo or
            AbstractValueKind.Dict or
            AbstractValueKind.Set or
            AbstractValueKind.SetType or
            AbstractValueKind.CollectionsDefaultDict or
            AbstractValueKind.CollectionsCounter or
            AbstractValueKind.CollectionsDeque or
            AbstractValueKind.CollectionsChainMap;

    public static bool IsDefinitelyNonSubscriptable(AbstractValue value)
        => value.Kind == AbstractValueKind.MaybeNone
            ? IsDefinitelyNonSubscriptable((AbstractValue)value.Value)
            : (value.Kind is AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.Set or
            AbstractValueKind.SetType or
            AbstractValueKind.Path or
            AbstractValueKind.TextFileHandle or
            AbstractValueKind.Module or
            AbstractValueKind.KnownCallable or
            AbstractValueKind.RegexPattern or
            AbstractValueKind.MaybeRegexMatch or
            AbstractValueKind.RegexMatch or
            AbstractValueKind.ArgparseParser or
            AbstractValueKind.ArgparseMutuallyExclusiveGroup or
            AbstractValueKind.ArgparseNamespace or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.CsvDictWriter or
            AbstractValueKind.Decimal or
            AbstractValueKind.DecimalContext or
            AbstractValueKind.DateTimeTimedelta or
            AbstractValueKind.DateTimeDate or
            AbstractValueKind.DateTimeTime or
            AbstractValueKind.DateTimeDateTime or
            AbstractValueKind.DateTimeTimezone or
            AbstractValueKind.StatisticsNormalDist or
            AbstractValueKind.Random or
            AbstractValueKind.DifflibDiffer or
            AbstractValueKind.DifflibHtmlDiff or
            AbstractValueKind.DifflibSequenceMatcher or
            AbstractValueKind.PkgutilLoader or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.SubprocessPopen or
            AbstractValueKind.DataclassField or
            AbstractValueKind.OpenPyxlConditionalFormattingRule or
            AbstractValueKind.OpenPyxlMergedCellSet or
            AbstractValueKind.Function) ||
            IsOpenPyxlMemberOnlyValue(value.Kind);

    private static bool IsOpenPyxlMemberOnlyValue(AbstractValueKind kind)
        => kind is AbstractValueKind.OpenPyxlCell or
            AbstractValueKind.OpenPyxlHyperlink or
            AbstractValueKind.OpenPyxlComment or
            AbstractValueKind.OpenPyxlFont or
            AbstractValueKind.OpenPyxlPatternFill or
            AbstractValueKind.OpenPyxlBorder or
            AbstractValueKind.OpenPyxlSide or
            AbstractValueKind.OpenPyxlAlignment or
            AbstractValueKind.OpenPyxlProtection or
            AbstractValueKind.OpenPyxlNamedStyle or
            AbstractValueKind.OpenPyxlColor or
            AbstractValueKind.OpenPyxlTable or
            AbstractValueKind.OpenPyxlTableStyleInfo or
            AbstractValueKind.OpenPyxlDataValidation or
            AbstractValueKind.OpenPyxlAutoFilter or
            AbstractValueKind.OpenPyxlSheetProtection or
            AbstractValueKind.OpenPyxlWorkbookProtection or
            AbstractValueKind.OpenPyxlDrawing or
            AbstractValueKind.OpenPyxlChart or
            AbstractValueKind.OpenPyxlImage or
            AbstractValueKind.OpenPyxlSheetView or
            AbstractValueKind.OpenPyxlSelection or
            AbstractValueKind.OpenPyxlPageMargins or
            AbstractValueKind.OpenPyxlPageSetup or
            AbstractValueKind.OpenPyxlColumnDimension or
            AbstractValueKind.OpenPyxlRowDimension;

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

    public static bool IsFloatLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Float or AbstractValueKind.FloatType;

    public static bool IsNumericLike(AbstractValue value)
        => IsIntegerLike(value) || IsFloatLike(value) || value.Kind == AbstractValueKind.Decimal;

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
                truth = (bool)value.Value;
                return true;
            case AbstractValueKind.String:
                truth = ((string)value.Value).Length != 0;
                return true;
            case AbstractValueKind.Bytes:
                truth = ((byte[])value.Value).Length != 0;
                return true;
            case AbstractValueKind.Integer when System.Numerics.BigInteger.TryParse(
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer):
                truth = !integer.IsZero;
                return true;
            case AbstractValueKind.Float when double.TryParse(
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var floating):
                truth = floating != 0.0;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            case AbstractValueKind.Set:
                truth = ((IReadOnlyList<AbstractValue>)value.Value).Count != 0;
                return true;
            case AbstractValueKind.Dict:
                truth = ((IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)value.Value).Count != 0;
                return true;
            case AbstractValueKind.RegexMatch:
                truth = true;
                return true;
            case AbstractValueKind.MaybeNone when TryGetTruthiness((AbstractValue)value.Value, out var innerTruth) && !innerTruth:
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
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
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
            case ListLiteralExpressionSyntax list when !list.UnpackingFlags.Any(flag => flag):
                count = list.Items.Count;
                return true;
            case TupleLiteralExpressionSyntax tuple when !tuple.UnpackingFlags.Any(flag => flag):
                count = tuple.Items.Count;
                return true;
            case SetLiteralExpressionSyntax set when !set.UnpackingFlags.Any(flag => flag):
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
                count = ((string)value.Value).Length;
                return true;
            case AbstractValueKind.Bytes:
                count = ((byte[])value.Value).Length;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            case AbstractValueKind.Set:
                count = ((IReadOnlyList<AbstractValue>)value.Value).Count;
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

    public static string DescribeLiteralType(ExpressionSyntax expression)
    {
        return expression switch
        {
            IntegerLiteralExpressionSyntax => "int",
            FloatLiteralExpressionSyntax => "float",
            BooleanLiteralExpressionSyntax => "bool",
            BytesLiteralExpressionSyntax => "bytes",
            StringLiteralExpressionSyntax => "str",
            NoneLiteralExpressionSyntax => "NoneType",
            ListLiteralExpressionSyntax => "list",
            TupleLiteralExpressionSyntax => "tuple",
            DictLiteralExpressionSyntax => "dict",
            SetLiteralExpressionSyntax => "set",
            _ => "object"
        };
    }

    public static string DescribeLiteralType(AbstractValue value)
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
            AbstractValueKind.Module => $"module '{value.Value}'",
            AbstractValueKind.KnownCallable => $"callable '{value.Value}'",
            AbstractValueKind.RegexPattern => "re.Pattern",
            AbstractValueKind.MaybeNone => DescribeLiteralType((AbstractValue)value.Value) + " | None",
            AbstractValueKind.MaybeRegexMatch => "re.Match | None",
            AbstractValueKind.RegexMatch => "re.Match",
            AbstractValueKind.ArgparseParser => "argparse.ArgumentParser",
            AbstractValueKind.ArgparseMutuallyExclusiveGroup => "argparse._MutuallyExclusiveGroup",
            AbstractValueKind.ArgparseNamespace => "argparse.Namespace",
            AbstractValueKind.CsvReader => "csv.reader",
            AbstractValueKind.CsvDictReader => "csv.DictReader",
            AbstractValueKind.CsvWriter => "csv.writer",
            AbstractValueKind.CsvDictWriter => "csv.DictWriter",
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
            AbstractValueKind.Function => "function",
            AbstractValueKind.UserClass => ((AbstractClassSummary)value.Value).Name,
            AbstractValueKind.UserInstance => ((AbstractInstanceSummary)value.Value).Class.Name,
            _ => "object"
        };
    }
}
