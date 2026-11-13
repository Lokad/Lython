using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Shared by exact guest operations, ingress/bool fallback and accumulators.
    // Admission covers the retained magnitude and working overlap in one grant.
    internal static object EvaluateIntegerBinary(BinaryOperatorSyntax op, BigInteger left,
        BigInteger right, ExecutionContext context, LythonSourceSpan span, MemoryGovernor? owner = null)
    {
        if (op is BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo && right.IsZero)
            throw new LythonRuntimeException("ZeroDivisionError", op == BinaryOperatorSyntax.Modulo
                ? "integer modulo by zero" : "integer division or modulo by zero", span);

        var governor = owner ?? context.MemoryGovernor;
        try
        {
            using var storage = GuardIntegerBinary(op, left, right, context, governor, span);
            // Admission can synchronously call the host, including cancellation.
            if (storage is not null) context.CheckExecution(span);
            var result = op switch
            {
                BinaryOperatorSyntax.Subtract => left - right,
                BinaryOperatorSyntax.Multiply => left * right,
                BinaryOperatorSyntax.FloorDivide => PyNumberOps.FloorDivideIntegers(left, right),
                BinaryOperatorSyntax.Modulo => PyNumberOps.ModuloIntegers(left, right),
                _ => throw new System.Diagnostics.UnreachableException(),
            };
            if (storage is not null) context.CheckExecution(span);
            return OwnFreshInteger(result, governor, span, storage);
        }
        catch (OverflowException error)
        {
            throw new LythonRuntimeException("OverflowError", error.Message, span);
        }
    }

    private static MemoryGovernor.TemporaryMemoryReservation? GuardIntegerBinary(
        BinaryOperatorSyntax op, BigInteger left, BigInteger right, ExecutionContext context,
        MemoryGovernor governor, LythonSourceSpan span)
    {
        if (op == BinaryOperatorSyntax.Subtract)
            return GuardIntegerAddition(left, right, governor, span);

        // Fixed-size scalars use existing minor-storage headroom. No allowance
        // transaction per counter; major operands still fund division workspace
        // even when their remainder is small.
        if (left.GetByteCount() < 4 && right.GetByteCount() < 4) return null;
        var leftBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(left);
        var rightBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(right);
        var bits = op == BinaryOperatorSyntax.Multiply
            ? RuntimeMemoryEstimates.SaturatingAdd(leftBits, rightBits)
            : Math.Max(leftBits, rightBits);
        if (bits <= 64) return null;
        context.CheckExecution(span);
        var bytes = RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(
            RuntimeMemoryEstimates.SaturatingAdd(bits, 1));
        // Multiplication shares math.prod's conservative result/scratch factor.
        // Division also covers quotient, remainder, normalization and correction.
        return governor.ReserveTemporary(RuntimeMemoryEstimates.SaturatingMultiply(
            op == BinaryOperatorSyntax.Multiply ? 3 : 8, bytes), span);
    }
}
