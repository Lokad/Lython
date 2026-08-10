namespace Lokad.Lython.Runtime.Numbers;

internal static class PyRealNumber
{
    public static bool TryAsDouble(object value, out double result)
    {
        if (PyNumberOps.TryAsNumber(value, out var number))
        {
            result = number.ToDouble();
            return true;
        }

        if (value is PyDecimal decimalValue)
        {
            result = (double)decimalValue.Value;
            return true;
        }

        result = default;
        return false;
    }
}
