using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Tracks committed charges for values whose owner reclaims them once they
// become unreachable: sweeps release charges for entries whose targets were
// collected while keeping every live entry charged. Callers own the sweep
// cadence; an abandoned pool simply stops sweeping and keeps its charges
// (safe direction). Single-threaded like the rest of the runtime.
internal sealed class ChargeReclamationPool
{
    private readonly MemoryGovernor _governor;
    private readonly List<Entry> _entries = new();

    private readonly record struct Entry(WeakReference<object> Target, long Charge);

    public ChargeReclamationPool(MemoryGovernor governor)
    {
        _governor = governor;
    }

    public int Count => _entries.Count;

    public void Track(object value, long charge)
    {
        if (charge > 0)
        {
            _entries.Add(new Entry(new WeakReference<object>(value), charge));
        }
    }

    // Registers a governed string for its exact construction charge; shared
    // empties and unowned values carry no charge and stay untracked.
    public void TrackString(PyString value)
    {
        if (value.OwnerMemoryGovernor is null)
        {
            return;
        }

        Track(value, PyString.EstimateApproximateBytes(value.Utf8Bytes.Length));
    }

    // Releases charges for entries whose targets have been collected and
    // prunes them; returns the released bytes.
    public long Sweep()
    {
        var released = 0L;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (!_entries[i].Target.TryGetTarget(out _))
            {
                released += _entries[i].Charge;
                _entries.RemoveAt(i);
            }
        }

        if (released > 0)
        {
            _governor.Release(released);
        }

        return released;
    }
}