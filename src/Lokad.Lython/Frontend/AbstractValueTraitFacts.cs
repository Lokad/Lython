namespace Lokad.Lython.Frontend;

[Flags]
internal enum AbstractValueTraits
{
    None = 0,
    DefinitelyNonCallable = 1 << 0,
    DefinitelyNonIterable = 1 << 1,
    DefinitelySized = 1 << 2,
    DefinitelyNonSized = 1 << 3,
    DefinitelyNonSubscriptable = 1 << 4,
}

internal static class AbstractValueTraitFacts
{
    private const AbstractValueTraits AtomicValue =
        AbstractValueTraits.DefinitelyNonCallable |
        AbstractValueTraits.DefinitelyNonIterable |
        AbstractValueTraits.DefinitelyNonSized |
        AbstractValueTraits.DefinitelyNonSubscriptable;

    private const AbstractValueTraits CallableAtomicValue =
        AbstractValueTraits.DefinitelyNonIterable |
        AbstractValueTraits.DefinitelyNonSized |
        AbstractValueTraits.DefinitelyNonSubscriptable;

    private const AbstractValueTraits SequenceValue =
        AbstractValueTraits.DefinitelyNonCallable |
        AbstractValueTraits.DefinitelySized;

    public static bool Has(AbstractValueKind kind, AbstractValueTraits trait)
        => (Get(kind) & trait) != 0;

    // Each kind deliberately appears once. This is the shared source of truth for
    // negative protocol facts; keeping it exhaustive prevents related diagnostics
    // from acquiring subtly different inventories as new abstract kinds are added.
    public static AbstractValueTraits Get(AbstractValueKind kind)
        => kind switch
        {
            AbstractValueKind.String or
            AbstractValueKind.StringType or
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.Dict or
            AbstractValueKind.StatisticsLinearRegression or
            AbstractValueKind.DifflibMatch or
            AbstractValueKind.PkgutilModuleInfo => SequenceValue,

            AbstractValueKind.Set or
            AbstractValueKind.SetType => SequenceValue | AbstractValueTraits.DefinitelyNonSubscriptable,

            AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.Ellipsis or
            AbstractValueKind.Path or
            AbstractValueKind.Module or
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
            AbstractValueKind.OpenPyxlMergedCellSet or
            AbstractValueKind.OpenPyxlCell or
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
            AbstractValueKind.OpenPyxlRowDimension => AtomicValue,

            AbstractValueKind.TextFileHandle =>
                AbstractValueTraits.DefinitelyNonCallable |
                AbstractValueTraits.DefinitelyNonSized |
                AbstractValueTraits.DefinitelyNonSubscriptable,

            AbstractValueKind.KnownCallable or
            AbstractValueKind.Function => CallableAtomicValue,

            AbstractValueKind.CsvReader or
            AbstractValueKind.CsvDictReader or
            AbstractValueKind.DecimalTuple => AbstractValueTraits.DefinitelyNonCallable,

            AbstractValueKind.CollectionsDefaultDict or
            AbstractValueKind.CollectionsCounter or
            AbstractValueKind.CollectionsDeque or
            AbstractValueKind.CollectionsChainMap => AbstractValueTraits.DefinitelySized,

            AbstractValueKind.OpenPyxlWorkbook or
            AbstractValueKind.OpenPyxlTableCollection or
            AbstractValueKind.OpenPyxlDataValidationList =>
                AbstractValueTraits.DefinitelyNonCallable |
                AbstractValueTraits.DefinitelyNonSized,

            AbstractValueKind.OpenPyxlConditionalFormattingRule =>
                AbstractValueTraits.DefinitelyNonCallable |
                AbstractValueTraits.DefinitelyNonIterable |
                AbstractValueTraits.DefinitelyNonSubscriptable,

            AbstractValueKind.OpenPyxlWorksheet =>
                AbstractValueTraits.DefinitelyNonCallable |
                AbstractValueTraits.DefinitelyNonIterable |
                AbstractValueTraits.DefinitelyNonSized,

            AbstractValueKind.OpenPyxlConditionalFormattingCollection => AbstractValueTraits.DefinitelyNonSized,

            AbstractValueKind.Unknown or
            AbstractValueKind.Never or
            AbstractValueKind.MaybeNone or
            AbstractValueKind.UserClass or
            AbstractValueKind.UserInstance => AbstractValueTraits.None,

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown abstract value kind."),
        };
}
