using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class PyTruthiness
{
    public static bool IsTruthy(object value)
    {
        return value switch
        {
            PyNone => false,
            IPyTruthyValue truthy => truthy.IsTruthy(),
            bool boolean => boolean,
            BigInteger integer => integer != BigInteger.Zero,
            double floating => floating != 0.0,
            IPyIterableValue iterable => HasAny(iterable.Iterate()),
            IReadOnlyCollection<object> collection => collection.Count != 0,
            System.Collections.ICollection collection => collection.Count != 0,
            IEnumerable<object> sequence => HasAny(sequence),
            _ => true,
        };
    }

    private static bool HasAny(IEnumerable<object> sequence)
    {
        foreach (var _ in sequence)
        {
            return true;
        }

        return false;
    }
}
