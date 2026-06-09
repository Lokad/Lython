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
            StaticReturnShape.ListOfOpenPyxlNamedStyle => AbstractValue.ListOf(AbstractValue.OpenPyxlNamedStyle(span), span),
            StaticReturnShape.Boolean => AbstractValue.BooleanType(span),
            StaticReturnShape.None => AbstractValue.None(span),
            StaticReturnShape.Path => AbstractValue.Path(span),
            StaticReturnShape.ListOfPath => AbstractValue.ListOf(AbstractValue.Path(span), span),
            StaticReturnShape.Dict => AbstractValue.Dict([], span),
            StaticReturnShape.Integer => AbstractValue.IntegerType(span),
            StaticReturnShape.Float => AbstractValue.FloatType(span),
            StaticReturnShape.TupleFloatInteger => new(AbstractValueKind.Tuple, new[]
            {
                AbstractValue.FloatType(span),
                AbstractValue.IntegerType(span),
            }, span),
            StaticReturnShape.TupleFloatFloat => new(AbstractValueKind.Tuple, new[]
            {
                AbstractValue.FloatType(span),
                AbstractValue.FloatType(span),
            }, span),
            StaticReturnShape.RegexPattern => AbstractValue.RegexPattern(span),
            StaticReturnShape.MaybeRegexMatch => AbstractValue.MaybeRegexMatch(span),
            StaticReturnShape.RegexMatch => AbstractValue.RegexMatch(span),
            StaticReturnShape.ArgparseParser => AbstractValue.ArgparseParser(span),
            StaticReturnShape.ArgparseMutuallyExclusiveGroup => AbstractValue.ArgparseMutuallyExclusiveGroup(span),
            StaticReturnShape.ArgparseNamespace => AbstractValue.ArgparseNamespace(span),
            StaticReturnShape.CsvReader => AbstractValue.CsvReader(span),
            StaticReturnShape.CsvDictReader => AbstractValue.CsvDictReader(span),
            StaticReturnShape.CsvWriter => AbstractValue.CsvWriter(span),
            StaticReturnShape.CsvDictWriter => AbstractValue.CsvDictWriter(span),
            StaticReturnShape.CollectionsDefaultDict => AbstractValue.CollectionsDefaultDict(span),
            StaticReturnShape.CollectionsCounter => AbstractValue.CollectionsCounter(span),
            StaticReturnShape.CollectionsDeque => AbstractValue.CollectionsDeque(span),
            StaticReturnShape.CollectionsChainMap => AbstractValue.CollectionsChainMap(span),
            StaticReturnShape.Decimal => AbstractValue.Decimal(span),
            StaticReturnShape.DecimalContext => AbstractValue.DecimalContext(span),
            StaticReturnShape.DecimalTuple => AbstractValue.DecimalTuple(span),
            StaticReturnShape.DateTimeTimedelta => AbstractValue.DateTimeTimedelta(span),
            StaticReturnShape.DateTimeDate => AbstractValue.DateTimeDate(span),
            StaticReturnShape.DateTimeTime => AbstractValue.DateTimeTime(span),
            StaticReturnShape.DateTimeDateTime => AbstractValue.DateTimeDateTime(span),
            StaticReturnShape.DateTimeTimezone => AbstractValue.DateTimeTimezone(span),
            StaticReturnShape.DifflibDiffer => AbstractValue.DifflibDiffer(span),
            StaticReturnShape.DifflibHtmlDiff => AbstractValue.DifflibHtmlDiff(span),
            StaticReturnShape.DifflibMatch => AbstractValue.DifflibMatch(span),
            StaticReturnShape.DifflibSequenceMatcher => AbstractValue.DifflibSequenceMatcher(span),
            StaticReturnShape.PkgutilModuleInfo => AbstractValue.PkgutilModuleInfo(span),
            StaticReturnShape.PkgutilLoader => AbstractValue.PkgutilLoader(span),
            StaticReturnShape.ListOfPkgutilModuleInfo => AbstractValue.ListOf(AbstractValue.PkgutilModuleInfo(span), span),
            StaticReturnShape.ListOfBytes => AbstractValue.ListOf(AbstractValue.BytesType(span), span),
            StaticReturnShape.SubprocessCompletedProcess => AbstractValue.SubprocessCompletedProcess(span),
            StaticReturnShape.ListOfListOfString => AbstractValue.ListOf(AbstractValue.ListOf(AbstractValue.StringType(span), span), span),
            StaticReturnShape.OpenPyxlWorkbook => AbstractValue.OpenPyxlWorkbook(span),
            StaticReturnShape.OpenPyxlWorksheet => AbstractValue.OpenPyxlWorksheet(span),
            StaticReturnShape.OpenPyxlCell => AbstractValue.OpenPyxlCell(span),
            StaticReturnShape.OpenPyxlHyperlink => AbstractValue.OpenPyxlHyperlink(span),
            StaticReturnShape.OpenPyxlComment => AbstractValue.OpenPyxlComment(span),
            StaticReturnShape.OpenPyxlFont => AbstractValue.OpenPyxlFont(span),
            StaticReturnShape.OpenPyxlPatternFill => AbstractValue.OpenPyxlPatternFill(span),
            StaticReturnShape.OpenPyxlBorder => AbstractValue.OpenPyxlBorder(span),
            StaticReturnShape.OpenPyxlSide => AbstractValue.OpenPyxlSide(span),
            StaticReturnShape.OpenPyxlAlignment => AbstractValue.OpenPyxlAlignment(span),
            StaticReturnShape.OpenPyxlProtection => AbstractValue.OpenPyxlProtection(span),
            StaticReturnShape.OpenPyxlNamedStyle => AbstractValue.OpenPyxlNamedStyle(span),
            StaticReturnShape.OpenPyxlColor => AbstractValue.OpenPyxlColor(span),
            StaticReturnShape.OpenPyxlTable => AbstractValue.OpenPyxlTable(span),
            StaticReturnShape.OpenPyxlTableStyleInfo => AbstractValue.OpenPyxlTableStyleInfo(span),
            StaticReturnShape.OpenPyxlDataValidation => AbstractValue.OpenPyxlDataValidation(span),
            StaticReturnShape.OpenPyxlConditionalFormattingRule => AbstractValue.OpenPyxlConditionalFormattingRule(span),
            StaticReturnShape.OpenPyxlAutoFilter => AbstractValue.OpenPyxlAutoFilter(span),
            StaticReturnShape.OpenPyxlSheetProtection => AbstractValue.OpenPyxlSheetProtection(span),
            StaticReturnShape.OpenPyxlWorkbookProtection => AbstractValue.OpenPyxlWorkbookProtection(span),
            StaticReturnShape.OpenPyxlDrawing => AbstractValue.OpenPyxlDrawing(span),
            StaticReturnShape.OpenPyxlChart => AbstractValue.OpenPyxlChart(span),
            StaticReturnShape.OpenPyxlImage => AbstractValue.OpenPyxlImage(span),
            StaticReturnShape.OpenPyxlSheetView => AbstractValue.OpenPyxlSheetView(span),
            StaticReturnShape.OpenPyxlSelection => AbstractValue.OpenPyxlSelection(span),
            StaticReturnShape.OpenPyxlPageMargins => AbstractValue.OpenPyxlPageMargins(span),
            StaticReturnShape.OpenPyxlPageSetup => AbstractValue.OpenPyxlPageSetup(span),
            StaticReturnShape.OpenPyxlTableCollection => AbstractValue.OpenPyxlTableCollection(span),
            StaticReturnShape.OpenPyxlDataValidationList => AbstractValue.OpenPyxlDataValidationList(span),
            StaticReturnShape.OpenPyxlConditionalFormattingCollection => AbstractValue.OpenPyxlConditionalFormattingCollection(span),
            StaticReturnShape.OpenPyxlColumnDimension => AbstractValue.OpenPyxlColumnDimension(span),
            StaticReturnShape.OpenPyxlRowDimension => AbstractValue.OpenPyxlRowDimension(span),
            StaticReturnShape.OpenPyxlMergedCellSet => AbstractValue.OpenPyxlMergedCellSet(span),
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
