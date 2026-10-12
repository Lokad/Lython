using System.Numerics;

namespace Lokad.Lython.Runtime;

// Container-adopted distinct-scalar coupons (N06). Small scalars (boxed integers
// the pool does not already own, doubles and other small CLR numerics) carry no
// per-box pool entry: at 128 B marginal an entry costs several times the box it
// would cover, and creation-side charging would bill short-lived temporaries.
// Instead each governed container adopts the distinct scalar identities it
// retains, committing one uniform coupon per identity and releasing it when the
// last reference leaves. Aliases of one box share a single coupon through
// reference-identity refcounts; cross-container aliases double-charge
// conservatively (bounded, safe direction); scalars that never touch a container
// stay free. Coupons fold into the container's committed storage bytes, so the
// existing snapshot/notify/drop machinery carries them without a second lifetime.
internal sealed class AdoptedScalarCoupons
{
    // Uniform coupon per distinct adopted identity: covers a small box plus its
    // refcount entry with headroom in the safe (overcharging) direction.
    internal const long CouponBytes = 64L;

    private Dictionary<object, int>? _refcounts;
    private long _committedBytes;

    public long CommittedBytes => _committedBytes;

    // Type gate without ownership lookup, so hot scalar-free paths skip both.
    // PyDecimal joins the CLR numerics: a boxed decimal retains as much as a
    // boxed double while carrying no ownership of its own, and int/decimal
    // cost symmetry is pinned by the Counter accounting tests.
    public static bool IsAdoptableScalar(object? value)
        => value is BigInteger or double or int or long or PyDecimal;

    // Adopts one incoming value. Denies before the caller mutates: a denied
    // coupon commits nothing and records nothing.
    public void Adopt(object? value, MemoryGovernor governor, LythonSourceSpan? span)
    {
        if (!IsAdoptableScalar(value) || ChargeReclamationPool.IsTrackedValue(value!))
        {
            return;
        }

        if (_refcounts is not null && _refcounts.TryGetValue(value!, out var held))
        {
            _refcounts[value!] = checked(held + 1);
            return;
        }

        // Commit first, publish last: a denied coupon leaves the map exactly
        // as it was, and a failed publication refunds instead of stranding.
        governor.Reserve(CouponBytes, span);
        governor.Commit(CouponBytes);
        _committedBytes += CouponBytes;
        try
        {
            _refcounts ??= new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            _refcounts.Add(value!, 1);
        }
        catch
        {
            _committedBytes -= CouponBytes;
            governor.Release(CouponBytes);
            throw;
        }
    }

    // Releases one outgoing reference. Map-gated (never a type gate): only an
    // adopted identity can hold a coupon, so unknown values are a no-op and a
    // box that gained pool ownership after adoption still releases exactly once.
    public void Release(object? value, MemoryGovernor governor)
    {
        if (value is null || _refcounts is null)
        {
            return;
        }

        UnadoptOne(value, governor);
    }

    // Releases every coupon at once for wholesale replacement or abandonment.
    public void ReleaseAll(MemoryGovernor governor)
    {
        if (_committedBytes > 0)
        {
            governor.Release(_committedBytes);
            _committedBytes = 0;
        }

        _refcounts?.Clear();
    }

    private void UnadoptOne(object value, MemoryGovernor governor)
    {
        // Unknown or already-released identities carry no coupon.
        if (_refcounts is null || !_refcounts.TryGetValue(value, out var held))
        {
            return;
        }

        if (held <= 1)
        {
            _refcounts.Remove(value);
            _committedBytes -= CouponBytes;
            governor.Release(CouponBytes);
        }
        else
        {
            _refcounts[value] = held - 1;
        }
    }
}
