namespace Lokad.Lython.Tests.Harness;

internal sealed class AdmissionHookMemoryBudget(ILythonMemoryBudget parent) : ILythonMemoryBudget
{
    internal Action<long>? AfterGrowth;
    internal Func<long, bool>? RefuseGrowth;
    public ILythonMemoryScope CreateScope() => parent.CreateScope();
    public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
    {
        if (bytes > 0 && RefuseGrowth?.Invoke(bytes) == true) { reservation = null; return false; }
        if (!parent.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
        reservation = new ProbeReservation(this, admitted);
        AfterGrowth?.Invoke(bytes);
        return true;
    }
    private sealed class ProbeReservation(AdmissionHookMemoryBudget hook, ILythonMemoryReservation inner) : ILythonMemoryReservation
    {
        public long Bytes => inner.Bytes;
        public bool TryResize(long bytes)
        {
            var old = inner.Bytes;
            if (bytes > old && hook.RefuseGrowth?.Invoke(bytes - old) == true) return false;
            if (!inner.TryResize(bytes)) return false;
            if (bytes > old) hook.AfterGrowth?.Invoke(bytes - old);
            return true;
        }
        public void Dispose() => inner.Dispose();
    }
}
