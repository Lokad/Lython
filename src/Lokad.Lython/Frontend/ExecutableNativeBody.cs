using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// A closed native body prepares operation structure once. Ordinary code keeps
// its own physical operands and source spans; only eligible helpers own nodes.
// Inputs use the existing operand domains: negative constant indexes, the local
// prefix, then operation indexes offset by that prefix. Consumed local/constant
// leaves need no Read node; real roots and mutable name reads retain metadata.
internal enum NativeReturnOperation
{
    ReadConstant, ReadLocal, ReadGlobal, ReadClosure,
    Binary, Augmented,
    Subscript,
    BuiltinUnary,
    StringMethod, BytesMethod, BooleanMethod, ListMethod,
    Split, Replace, StartsWith, EndsWith,
    CasedSplit,
    ConvertFormat, FormatValue,
    Conditional, None, EmptyList, EmptyTuple, StoreClosure,
}

internal readonly record struct NativeReturnValue(NativeReturnOperation Operation,
    LythonSourceSpan Span, int Operand, int First, int Second,
    LythonRuntime.ReceiverMethod? Method = null, LythonSourceSpan? ReturnSpan = null, LythonSourceSpan? MemberSpan = null)
{
    internal bool IsRead => Operation is >= NativeReturnOperation.ReadConstant and <= NativeReturnOperation.ReadClosure;

    // Native inputs belong to the operation graph, not ordinary physical slots.
    // Keep only the source and operation facts, validated once during preparation.
    internal NativeReturnValue(NativeReturnOperation operation, in ExecutableInstruction instruction, int first, int second,
        LythonRuntime.ReceiverMethod? method = null,
        LythonSourceSpan? ReturnSpan = null, LythonSourceSpan? MemberSpan = null)
        : this(operation, instruction.Span, operation switch
        {
            NativeReturnOperation.ReadConstant or NativeReturnOperation.FormatValue => instruction.ConstantIndex,
            NativeReturnOperation.ReadLocal => instruction.LocalSlot,
            NativeReturnOperation.ReadGlobal => instruction.NameIndex,
            NativeReturnOperation.ReadClosure or NativeReturnOperation.StoreClosure => instruction.ClosureSlot,
            >= NativeReturnOperation.BuiltinUnary and <= NativeReturnOperation.CasedSplit => instruction.CallSiteIndex,
            NativeReturnOperation.Binary => (int)instruction.BinaryOperator,
            NativeReturnOperation.Augmented => (int)instruction.AugmentedOperator,
            NativeReturnOperation.ConvertFormat => instruction.FormattedConversion,
            NativeReturnOperation.None or NativeReturnOperation.EmptyList or NativeReturnOperation.EmptyTuple
                or NativeReturnOperation.Subscript => 0,
            _ => throw new InvalidOperationException($"Unexpected native operation: {operation}"),
        }, first, second, method, ReturnSpan, MemberSpan) { }
}

internal sealed record ExecutableNativeBody(NativeReturnValue[] Values, int Root, int ClosureInputCount = 0)
{
    internal static readonly ExecutableNativeBody ReturnNone = new([], -1);
}
