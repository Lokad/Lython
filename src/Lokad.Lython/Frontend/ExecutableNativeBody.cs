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
    CasedSplit,
    ConvertFormat, FormatValue,
    Conditional, None, EmptySequence,
}

internal readonly record struct NativeReturnValue(NativeReturnOperation Operation, int SourceInstruction, int First, int Second,
    LythonRuntime.ReceiverMethod? Method = null, int Third = -1, int ReturnInstruction = -1);

internal sealed record ExecutableNativeBody(NativeReturnValue[] Values, int Root)
{
    internal static readonly ExecutableNativeBody ReturnNone = new([], -1);
}
