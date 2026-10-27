using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static BigInteger IntegerSquareRoot(BigInteger value, ExecutionContext context, LythonSourceSpan span)
    {
        if (value < 2)
        {
            return value;
        }

        // The result holds about half the input bits while Newton scratch
        // (sum, quotient, shift) lives at full input scale: reserve the input
        // plus result/scratch coexistence up front, then stay responsive to
        // cancellation once per iteration (each step divides huge operands).
        var inputBytes = RuntimeMemoryEstimates.EstimateBigIntegerBytes(value);
        var resultBits = (RuntimeMemoryEstimates.GetMagnitudeBitLength(value) + 1) / 2;
        using var integerAdmission = GuardIntegerResultBytes(
            RuntimeMemoryEstimates.SaturatingAdd(inputBytes,
                RuntimeMemoryEstimates.SaturatingMultiply(3, RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits))),
            context.MemoryGovernor,
            span);
        var x = value;
        var y = (x + value / x) >> 1;
        while (y < x)
        {
            context.CheckExecution(span);
            x = y;
            y = (x + value / x) >> 1;
        }

        return x;
    }
}
