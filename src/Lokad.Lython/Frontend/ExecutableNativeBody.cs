namespace Lokad.Lython.Frontend;

// A closed native body prepares operation structure once. Ordinary code keeps
// its own physical operands and source spans; only eligible helpers own nodes.
internal enum NativeReturnOperation
{
    Read,
    Add,
    Subscript,
    BuiltinUnary,
    Strip, Lower, Upper, IsLower, IsUpper, IsTitle, IsAlpha, IsDigit, IsAlnum,
    IsAscii, IsDecimal, IsIdentifier, IsNumeric, IsPrintable, IsSpace,
    Split, Replace, StartsWith, EndsWith,
    SplitLowerWhitespace, SplitUpperWhitespace,
    StartFormat, AppendFormatText, AppendFormatField, FinishFormat, ConvertFormat, FormatValue,
}

internal readonly record struct NativeReturnValue(NativeReturnOperation Operation, int SourceInstruction, int First, int Second);

internal sealed record ExecutableNativeBody(NativeReturnValue[] Values, int Root)
{
    internal static readonly ExecutableNativeBody ReturnNone = new([], -1);
}
