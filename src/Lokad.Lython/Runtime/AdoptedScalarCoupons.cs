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

    // All-or-nothing batch adoption: the rollback record precedes each coupon,
    // so a denied coupon (or a failed record allocation) rolls back to pre-held
    // counts and the caller either owns the whole batch or nothing beyond them.
    // The values enumerable may be consumed twice on the rollback path only;
    // callers pass materialized inputs.
    public void AdoptAll(IEnumerable<object> values, MemoryGovernor governor, LythonSourceSpan? span)
    {
        List<object>? staged = null;
        try
        {
            foreach (var value in values)
            {
                if (!IsAdoptableScalar(value) || ChargeReclamationPool.IsTrackedValue(value!))
                {
                    continue;
                }

                // Record before adopting: a record-allocation failure then
                // commits nothing, and a denied adoption still unwinds through
                // UnadoptOne, which ignores recorded-but-unadopted identities.
                staged ??= new List<object>();
                staged.Add(value!);
                AdoptStaged(value!, governor, span);
            }
        }
        catch
        {
            if (staged is not null)
            {
                for (var i = staged.Count - 1; i >= 0; i--)
                {
                    UnadoptOne(staged[i], governor);
                }
            }

            throw;
        }
    }

    // All-or-nothing batch adoption over stored pairs: both halves adopt per
    // entry with the same rollback as AdoptAll. Callers pass materialized pairs.
    public void AdoptAllPairs(IEnumerable<KeyValuePair<object, object>> pairs, MemoryGovernor governor, LythonSourceSpan? span)
    {
        List<object>? staged = null;
        try
        {
            foreach (var pair in pairs)
            {
                if (IsAdoptableScalar(pair.Key) && !ChargeReclamationPool.IsTrackedValue(pair.Key!))
                {
                    // Record before adopting (see AdoptAll): the rollback
                    // tolerates recorded-but-unadopted entries.
                    staged ??= new List<object>();
                    staged.Add(pair.Key);
                    AdoptStaged(pair.Key, governor, span);
                }

                if (IsAdoptableScalar(pair.Value) && !ChargeReclamationPool.IsTrackedValue(pair.Value!))
                {
                    staged ??= new List<object>();
                    staged.Add(pair.Value);
                    AdoptStaged(pair.Value, governor, span);
                }
            }
        }
        catch
        {
            if (staged is not null)
            {
                for (var i = staged.Count - 1; i >= 0; i--)
                {
                    UnadoptOne(staged[i], governor);
                }
            }

            throw;
        }
    }

    // Replicates already-held references without adopting: every identity flows
    // through this container already, so only refcounts move and no coupon can
    // commit. Identities the container never adopted stay unadopted.
    public void AddRef(object? value, int extra)
    {
        if (value is null || extra <= 0 || _refcounts is null)
        {
            return;
        }

        if (_refcounts.TryGetValue(value, out var held))
        {
            _refcounts[value] = checked(held + extra);
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

    private void AdoptStaged(object value, MemoryGovernor governor, LythonSourceSpan? span)
    {
        if (_refcounts is not null && _refcounts.TryGetValue(value, out var held))
        {
            _refcounts[value] = checked(held + 1);
            return;
        }

        governor.Reserve(CouponBytes, span);
        governor.Commit(CouponBytes);
        _committedBytes += CouponBytes;
        try
        {
            _refcounts ??= new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            _refcounts.Add(value, 1);
        }
        catch
        {
            _committedBytes -= CouponBytes;
            governor.Release(CouponBytes);
            throw;
        }
    }

    private void UnadoptOne(object value, MemoryGovernor governor)
    {
        // A batch rollback may name an identity whose adoption denied before
        // the refcount map existed (the record precedes the coupon): nothing
        // to release, matching the Release/ReleaseAll guards above.
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