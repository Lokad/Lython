using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests.Harness;

// Model the caller's independently owned, already-admitted working storage.
// Refusal hooks on the execution cannot silently fund this test fixture.
internal sealed class NativeCallWindow : IDisposable
{
    private readonly ILythonMemoryReservation _reservation;
    internal LythonRuntime.ExecutableSlot[] Values { get; }

    internal NativeCallWindow(LythonMemoryBudget budget, ExecutableCodeObject code)
    {
        if (!budget.TryReserve(LythonRuntime.ExecutableSlotsBytes(code.InvocationStorageCapacity), out var reservation))
            throw new InvalidOperationException("The test caller cannot admit its working storage.");
        _reservation = reservation;
        try { Values = new LythonRuntime.ExecutableSlot[code.InvocationStorageCapacity]; }
        catch { reservation.Dispose(); throw; }
    }

    public void Dispose()
    {
        Array.Clear(Values);
        _reservation.Dispose();
    }
}
