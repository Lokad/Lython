using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// A closed native body prepares operation structure once. Ordinary code keeps
// its own physical operands and source spans; only eligible helpers own nodes.
// Inputs use the existing operand domains: negative constant indexes, the local
// prefix, then operation indexes offset by that prefix. Consumed local/constant
// leaves need no Read node; real roots and mutable name reads retain metadata.
internal enum NativeReturnOperation
{
    Read,
    Binary,
    Subscript,
    BuiltinUnary,
    StringMethod, BytesMethod, BooleanMethod, ListMethod,
    Split, Replace, StartsWith, EndsWith,
    CasedSplit,
    ConvertFormat, FormatValue,
    Conditional, None, EmptySequence,
}

internal readonly record struct NativeReturnValue(NativeReturnOperation Operation, ExecutableInstruction Instruction, int First, int Second,
    LythonRuntime.ReceiverMethod? Method = null, int Third = -1, LythonSourceSpan? ReturnSpan = null, LythonSourceSpan? MemberSpan = null);

internal sealed record ExecutableNativeBody(NativeReturnValue[] Values, int Root)
{
    internal static readonly ExecutableNativeBody ReturnNone = new([], -1);
}
