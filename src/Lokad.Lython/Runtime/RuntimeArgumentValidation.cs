using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class RuntimeArgumentValidation
{
    public static BigInteger ExpectInteger(object value, string message, LythonSourceSpan span)
    {
        if (!PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return integer;
    }

    public static double ExpectReal(object value, string owner, LythonSourceSpan span)
    {
        if (!PyRealNumber.TryAsDouble(value, out var real))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
        }

        return real;
    }

    public static string ExpectString(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects a string.", span);
        }

        return text.AsString();
    }

    internal static async ValueTask<int> CoerceIndexInt32Async(object value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var indexed = await LythonRuntime.CoerceTypeIndexAsync(value, context, span, asynchronous).ConfigureAwait(false);
        if (!PyNumberOps.TryAsInteger(indexed ?? PyNone.Instance, out var integer))
            throw RuntimeErrors.Type("'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        if (integer < int.MinValue || integer > int.MaxValue)
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span);
        return (int)integer;
    }

    // Resolve both implicit type slots before consulting a mutable receiver's
    // length. The sync caller uses the same ordering without suspending.
    internal static async ValueTask<(BigInteger? Start, BigInteger? Stop)> CoerceSearchBoundsAsync(
        object[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        const string message = "slice indices must be integers or have an __index__ method";
        BigInteger? start = arguments.Length >= 2 ? ExpectInteger(
            await LythonRuntime.CoerceTypeIndexAsync(arguments[1], context, span, asynchronous).ConfigureAwait(false) ?? PyNone.Instance, message, span) : null;
        BigInteger? stop = arguments.Length >= 3 ? ExpectInteger(
            await LythonRuntime.CoerceTypeIndexAsync(arguments[2], context, span, asynchronous).ConfigureAwait(false) ?? PyNone.Instance, message, span) : null;
        return (start, stop);
    }

    public static int NormalizeSearchBound(BigInteger? value, int length, int defaultValue)
    {
        if (value is not { } integer) return defaultValue;
        if (integer.Sign < 0)
        {
            if (integer < -length) return 0;
            integer += length;
        }
        return (int)BigInteger.Min(integer, int.MaxValue);
    }

    internal static int NormalizeInsertIndex(object? value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        value ??= PyNone.Instance;
        if (!PyNumberOps.TryAsInteger(value, out var integer))
            throw RuntimeErrors.Type("'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        return (int)BigInteger.Clamp(integer, int.MinValue, int.MaxValue);
    }
}
