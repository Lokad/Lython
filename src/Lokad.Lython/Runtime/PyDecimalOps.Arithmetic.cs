namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    internal static LythonRuntimeException DivisionByZero(string message, LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "DivisionByZero"), message, span);

    internal static LythonRuntimeException InvalidOperation(string message, LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "InvalidOperation"), message, span);

    internal static LythonRuntimeException DecimalOverflow(LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "Overflow"), "Decimal arithmetic overflowed Lython's fixed-precision range.", span);

    public static object Add(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs + rhs, static (lhs, rhs) => Math.Min(lhs, rhs));

    public static object Subtract(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs - rhs, static (lhs, rhs) => Math.Min(lhs, rhs));

    public static object Multiply(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs * rhs, static (lhs, rhs) => checked(lhs + rhs));

    public static object Divide(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        if (rhs == 0m)
        {
            throw DivisionByZero("decimal division by zero", span);
        }

        try
        {
            return new PyDecimal(lhs / rhs);
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    public static object Modulo(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        if (rhs == 0m)
        {
            throw DivisionByZero("decimal modulo by zero", span);
        }

        try
        {
            return new PyDecimal(lhs % rhs, Math.Min(GetOperandExponent(left, lhs), GetOperandExponent(right, rhs)));
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    public static object Power(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !Numbers.PyNumberOps.TryAsInteger(right, out var exponent))
        {
            throw new LythonRuntimeException("TypeError", "Decimal power requires a Decimal base and an integer exponent.", span);
        }

        if (exponent < int.MinValue || exponent > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal exponent is too large.", span);
        }

        var exponentInt = (int)exponent;
        if (exponentInt == 0)
        {
            return new PyDecimal(1m);
        }

        if (exponentInt < 0)
        {
            if (lhs == 0m)
            {
                throw DivisionByZero("decimal division by zero", span);
            }

            try
            {
                return new PyDecimal(1m / Pow(lhs, -exponentInt));
            }
            catch (Exception ex) when (ex is OverflowException or DivideByZeroException)
            {
                // An underflowed divisor means a reciprocal past the range.
                throw DecimalOverflow(span);
            }
        }

        try
        {
            var result = Pow(lhs, exponentInt);
            return new PyDecimal(result, ConsistentExponent(result, checked(GetOperandExponent(left, lhs) * exponentInt)));
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    private static LythonRuntimeException CompareFailed(string? operation, object left, object right, LythonSourceSpan span)
        => operation is null
            ? new LythonRuntimeException("TypeError", "Values are not comparable.", span)
            : RuntimeErrors.UnsupportedComparison(operation, left, right, span);

    public static int Compare(object left, object right, LythonSourceSpan span, string? operation = null)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw CompareFailed(operation, left, right, span);
        }

        return lhs.CompareTo(rhs);
    }

    public static bool AreEqual(object left, object right)
        => TryAsDecimal(left, out var lhs) && TryAsDecimal(right, out var rhs) && lhs == rhs;

    // BCL multiplication rounds to fit instead of throwing, so a declared
    // exponent can overstate the stored scale and poison later rendering;
    // clamp it to the stored scale (a no-op for exact results and zeros).
    private static int ConsistentExponent(decimal value, int declared)
        => value == 0m || declared >= 0 ? declared : Math.Max(declared, -GetScale(value));

    private static object Binary(
        object left,
        object right,
        LythonSourceSpan span,
        Func<decimal, decimal, decimal> operation,
        Func<int, int, int> combineExponent)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        try
        {
            var result = operation(lhs, rhs);
            return new PyDecimal(
                result,
                ConsistentExponent(result, combineExponent(GetOperandExponent(left, lhs), GetOperandExponent(right, rhs))));
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    private static int GetOperandExponent(object value, decimal numericValue)
        => value is PyDecimal pyDecimal ? pyDecimal.Exponent : -GetScale(numericValue);

    private static decimal Pow(decimal value, int exponent)
    {
        decimal result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= value;
        }

        return result;
    }
}
