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

        if (TryMachineIntegerBinary(op, left, right, out var machineResult)) return machineResult;

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

    // Exact machine-sized arithmetic shares the instruction-chain/cell kernel.
    // No value escapes and no admission occurs until the result fits. Larger
    // results continue through the existing funded BigInteger path.
    private static bool TryMachineIntegerBinary(BinaryOperatorSyntax op, BigInteger left,
        BigInteger right, out BigInteger result)
    {
        if (TryMachineInteger(left, out var a) && TryMachineInteger(right, out var b)
            && TryMachineBinary(op, a, b, out var value))
        {
            result = value;
            return true;
        }
        result = default;
        return false;
    }

    private static bool TryMachineInteger(object? value, out long result)
    {
        if (value is BigInteger integer) return TryMachineInteger(integer, out result);
        result = 0;
        return false;
    }

    private static bool TryMachineInteger(BigInteger value, out long result)
    {
        if (value.GetByteCount() <= sizeof(long))
        {
            result = (long)value;
            return true;
        }
        result = 0;
        return false;
    }

    private static bool TryMachineBinary(BinaryOperatorSyntax op, long left, long right, out long result)
    {
        result = 0;
        // Two int64 operands fit these intermediate operations in Int128.
        // Out-of-range results decline without an exception or publication.
        // The same rule serves ordinary arithmetic, chains and captured cells.
        switch (op)
        {
            case BinaryOperatorSyntax.Add:
            case BinaryOperatorSyntax.Subtract:
            case BinaryOperatorSyntax.Multiply:
                var wide = op == BinaryOperatorSyntax.Add ? (Int128)left + right
                    : op == BinaryOperatorSyntax.Subtract ? (Int128)left - right : (Int128)left * right;
                if (wide < long.MinValue || wide > long.MaxValue) return false;
                result = (long)wide;
                return true;
            case BinaryOperatorSyntax.FloorDivide when right != 0:
                if (left == long.MinValue && right == -1) return false;
                result = Math.DivRem(left, right, out var remainder);
                if (remainder != 0 && (left < 0) != (right < 0)) result--;
                return true;
            case BinaryOperatorSyntax.Modulo when right != 0:
                if (left == long.MinValue && right == -1) return true; // Exact remainder is zero.
                result = left % right;
                if (result != 0 && (left < 0) != (right < 0)) result += right;
                return true;
            default: return false;
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
