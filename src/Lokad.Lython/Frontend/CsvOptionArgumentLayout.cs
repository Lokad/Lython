namespace Lokad.Lython.Frontend;

/// <summary>Maps the shared CSV format options to their bound-call argument positions.</summary>
internal readonly record struct CsvOptionArgumentLayout(
    int Dialect,
    int Delimiter,
    int QuoteCharacter,
    int Quoting,
    int DoubleQuote,
    int EscapeCharacter,
    int SkipInitialSpace,
    int LineTerminator,
    int Strict)
{
    public static readonly CsvOptionArgumentLayout Standard = new(1, 2, 3, 4, 5, 6, 7, 8, 9);
    public static readonly CsvOptionArgumentLayout Dictionary = new(4, 5, 6, 7, 8, 9, 10, 11, 12);
}
