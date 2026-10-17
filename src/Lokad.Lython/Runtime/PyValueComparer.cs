using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyValueComparer : IEqualityComparer<object>
{
    public static readonly PyValueComparer Instance = new();

    public new bool Equals(object? x, object? y)
    {
        // Key probing accepts identity even for non-reflexive values such as
        // NaN. Ordinary value equality retains its separate numeric rules.
        if (ReferenceEquals(x, y)) return x is not null;

        // Sealed UTF-8 strings cannot invoke guest code or recurse structurally.
        if (x is PyString leftText && y is PyString rightText)
            return leftText.Equals(rightText);

        // R13: comparer callbacks stay context-free even when reached under an
        // ambient comparison (for example a dict lookup inside __eq__): nested
        // structural positions observe the suppression and never dispatch
        // guest code through comparer internals.
        using (PyStructuralGuard.SuppressGuestDispatch())
        {
            return x is not null && y is not null && PyEquality.AreEqual(x, y);
        }
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
