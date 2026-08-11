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
            _ => throw new LythonRuntimeException("TypeError", $"{signature} expects {name} to be an integer.", span)
        };
    }
}
