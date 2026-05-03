using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static class StaticAbstractFacts
{
    public static bool IsDefinitelyNonCallable(AbstractValue value)
        => value.Kind is AbstractValueKind.String or
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
            AbstractValueKind.Path or
            AbstractValueKind.TextFileHandle or
            AbstractValueKind.Module or
            AbstractValueKind.RegexPattern or
            AbstractValueKind.MaybeRegexMatch or
            AbstractValueKind.RegexMatch or
            AbstractValueKind.ArgparseParser or
            AbstractValueKind.ArgparseMutuallyExclusiveGroup or
            AbstractValueKind.ArgparseNamespace or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.DataclassField;

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
        => value.Kind is AbstractValueKind.Integer or
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
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.DataclassField or
            AbstractValueKind.Function;

    public static bool IsDefinitelyKnownNonSized(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) && IsDefinitelyNonSized(value);

    public static bool IsDefinitelyNonSized(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or
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
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.DataclassField or
            AbstractValueKind.Function;

    public static bool IsDefinitelySized(AbstractValue value)
        => value.Kind is AbstractValueKind.String or
            AbstractValueKind.StringType or
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.Dict or
            AbstractValueKind.Set;

    public static bool IsDefinitelyNonSubscriptable(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.Set or
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
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.DataclassField or
            AbstractValueKind.Function;

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
            AbstractValueKind.Tuple;

    public static bool IsDefinitelyNonIntegerLike(AbstractValue value)
        => value.Kind is not AbstractValueKind.Unknown and
            not AbstractValueKind.Never and
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
        => IsIntegerLike(value) || IsFloatLike(value);

    public static bool IsDefinitelyNonNone(AbstractValue value)
        => value.Kind is not AbstractValueKind.Unknown and
            not AbstractValueKind.Never and
            not AbstractValueKind.MaybeRegexMatch and
            not AbstractValueKind.None;

    public static bool TryGetNonNegativeInt32(AbstractValue value, out int integer)
    {
        if (value.Kind == AbstractValueKind.Integer &&
            int.TryParse(
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out integer) &&
            integer >= 0)
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
            case ListLiteralExpressionSyntax list:
                count = list.Items.Count;
                return true;
            case TupleLiteralExpressionSyntax tuple:
                count = tuple.Items.Count;
                return true;
            case SetLiteralExpressionSyntax set:
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
            AbstractValueKind.Path => "pathlib.Path",
            AbstractValueKind.TextFileHandle => "file",
            AbstractValueKind.Module => $"module '{value.Value}'",
            AbstractValueKind.KnownCallable => $"callable '{value.Value}'",
            AbstractValueKind.RegexPattern => "re.Pattern",
            AbstractValueKind.MaybeRegexMatch => "re.Match | None",
            AbstractValueKind.RegexMatch => "re.Match",
            AbstractValueKind.ArgparseParser => "argparse.ArgumentParser",
            AbstractValueKind.ArgparseMutuallyExclusiveGroup => "argparse._MutuallyExclusiveGroup",
            AbstractValueKind.ArgparseNamespace => "argparse.Namespace",
            AbstractValueKind.CsvWriter => "csv.writer",
            AbstractValueKind.SubprocessCompletedProcess => "subprocess.CompletedProcess",
            AbstractValueKind.DataclassField => "dataclasses.Field",
            AbstractValueKind.Function => "function",
            AbstractValueKind.UserClass => ((AbstractClassSummary)value.Value).Name,
            AbstractValueKind.UserInstance => ((AbstractInstanceSummary)value.Value).Class.Name,
            _ => "object"
        };
    }
}
