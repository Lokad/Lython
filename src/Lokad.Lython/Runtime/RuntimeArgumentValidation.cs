using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class RuntimeArgumentValidation
{
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
