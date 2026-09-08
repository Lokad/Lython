using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyValueComparer : IEqualityComparer<object>
{
    public static readonly PyValueComparer Instance = new();

    public new bool Equals(object? x, object? y)
    {
        return x is not null && y is not null && PyEquality.AreEqual(x, y);
    }

    public int GetHashCode(object obj)
    {
        if (obj is IPyHashableValue hashable)
        {
            return hashable.GetPyHashCode();
        }

        return obj switch
        {
            _ when PyNumberOps.TryAsNumber(obj, out var numeric) => PyNumberOps.GetHashCode(numeric),
            _ => throw new PyUnhashableException()
        };
    }
}
