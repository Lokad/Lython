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

    public static int ParseInt32(object value, string name, string signature, LythonSourceSpan span)
    {
        return value switch
        {
            BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                ? throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span)
                : (int)integer,
            int integer => integer,
            bool flag => flag ? 1 : 0,
            _ => throw new LythonRuntimeException("TypeError", $"{signature} expects {name} to be an integer.", span)
        };
    }

    // Width/count-style arguments coerce through __index__ like CPython;
    // failures name the type instead of the builtin signature.
    public static int ParseIndexInt32(
        object value,
        string name,
        string signature,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        return coerced switch
        {
            BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                ? throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span)
                : (int)integer,
            int integer => integer,
            bool flag => flag ? 1 : 0,
            _ => throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span)
        };
    }

    public static int NormalizeSearchBound(
        object? value,
        int length,
        int defaultValue,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        if (value is null)
        {
            return defaultValue;
        }

        // Search bounds coerce through __index__ like CPython; explicit
        // non-index values (including None) report the slice-indices text.
        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        BigInteger integer;
        if (coerced is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (coerced is int small)
        {
            integer = new BigInteger(small);
        }
        else if (coerced is not BigInteger big)
        {
            throw new LythonRuntimeException("TypeError", "slice indices must be integers or have an __index__ method", span);
        }
        else
        {
            integer = big;
        }

        if (integer < int.MinValue)
        {
            return 0;
        }

        if (integer > int.MaxValue)
        {
            return length;
        }

        var index = (int)integer;
        if (index < 0)
        {
            index += length;
        }

        return Math.Clamp(index, 0, length);
    }
}
