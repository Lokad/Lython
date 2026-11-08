using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// A closed native body prepares operation structure once. Ordinary code keeps
// its own physical operands and source spans; only eligible helpers own nodes.
internal enum NativeReturnOperation
{
    Read,
    Add,
    Subscript,
    BuiltinUnary,
    StringMethod, BytesMethod, BooleanMethod, ListMethod,
    Split, Replace, StartsWith, EndsWith,
    SplitLowerWhitespace, SplitUpperWhitespace,
    ConvertFormat, FormatValue,
}

internal readonly record struct NativeReturnValue(NativeReturnOperation Operation, int SourceInstruction, int First, int Second,
    LythonRuntime.ReceiverMethod? Method = null);

internal sealed record ExecutableNativeBody(NativeReturnValue[] Values, int Root)
{
    internal static readonly ExecutableNativeBody ReturnNone = new([], -1);
}
