namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static AbstractValue CreateReturnValue(StaticReturnShape shape, LythonSourceSpan span)
        => CreateReturnValue(shape, AbstractValue.Unknown(span), span);

    private static AbstractValue CreateReturnValue(StaticReturnShape shape, AbstractValue receiver, LythonSourceSpan span)
    {
        return shape switch
        {
            StaticReturnShape.String => AbstractValue.StringType(span),
            StaticReturnShape.ListOfUnknown => AbstractValue.ListOf(AbstractValue.Unknown(span), span),
            StaticReturnShape.ListOfString => AbstractValue.ListOf(AbstractValue.StringType(span), span),
            StaticReturnShape.Boolean => AbstractValue.BooleanType(span),
            StaticReturnShape.None => AbstractValue.None(span),
            StaticReturnShape.Path => AbstractValue.Path(span),
            StaticReturnShape.ListOfPath => AbstractValue.ListOf(AbstractValue.Path(span), span),
            StaticReturnShape.Integer => AbstractValue.IntegerType(span),
            StaticReturnShape.Float => AbstractValue.FloatType(span),
            StaticReturnShape.RegexPattern => AbstractValue.RegexPattern(span),
            StaticReturnShape.MaybeRegexMatch => AbstractValue.MaybeRegexMatch(span),
            StaticReturnShape.RegexMatch => AbstractValue.RegexMatch(span),
            StaticReturnShape.ArgparseParser => AbstractValue.ArgparseParser(span),
            StaticReturnShape.ArgparseMutuallyExclusiveGroup => AbstractValue.ArgparseMutuallyExclusiveGroup(span),
            StaticReturnShape.ArgparseNamespace => AbstractValue.ArgparseNamespace(span),
            StaticReturnShape.CsvWriter => AbstractValue.CsvWriter(span),
            StaticReturnShape.SubprocessCompletedProcess => AbstractValue.SubprocessCompletedProcess(span),
            StaticReturnShape.ListOfListOfString => AbstractValue.ListOf(AbstractValue.ListOf(AbstractValue.StringType(span), span), span),
            StaticReturnShape.ListSame => CopyContainerValue(receiver, span, AbstractValueKind.List),
            StaticReturnShape.ListElement => GetListElementValue(receiver, span),
            StaticReturnShape.DictSame => CopyContainerValue(receiver, span, AbstractValueKind.Dict),
            StaticReturnShape.SetSame => CopyContainerValue(receiver, span, AbstractValueKind.Set),
            _ => AbstractValue.Unknown(span)
        };
    }

    private static AbstractValue CopyContainerValue(AbstractValue receiver, LythonSourceSpan span, AbstractValueKind expectedKind)
        => receiver.Kind == expectedKind || (expectedKind == AbstractValueKind.List && receiver.Kind == AbstractValueKind.ListType)
            ? receiver.WithSpan(span)
            : AbstractValue.Unknown(span);

    private static AbstractValue GetListElementValue(AbstractValue receiver, LythonSourceSpan span)
    {
        return receiver.Kind switch
        {
            AbstractValueKind.ListType => ((AbstractValue)receiver.Value).WithSpan(span),
            AbstractValueKind.List => JoinSequenceItems((IReadOnlyList<AbstractValue>)receiver.Value, span),
            _ => AbstractValue.Unknown(span)
        };
    }

    private static AbstractValue JoinSequenceItems(IReadOnlyList<AbstractValue> items, LythonSourceSpan span)
    {
        var result = AbstractValue.Never(span);
        foreach (var item in items)
        {
            result = AbstractValue.Join(result, item, span);
        }

        return result.Kind == AbstractValueKind.Never ? AbstractValue.Unknown(span) : result;
    }
}
