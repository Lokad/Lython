using Lokad.Lython.Runtime.Text;
using System.Runtime.ExceptionServices;

namespace Lokad.Lython.Runtime;

internal sealed partial class ExecutionState
{
    // A hint contains only immutable, independently owned names, never records
    // or values. A suspended/reentrant parser keeps its own immutable snapshot.
    internal PyString[] JsonRecordKeys { get; set; } = [];

    internal void RememberJsonRecordKeys(IEnumerable<object> keys, int count, LythonSourceSpan span)
    {
        CheckExecution(span);
        if (count == 0)
        {
            JsonRecordKeys = [];
            return;
        }

        // Fund replacement beside every still-live snapshot. Replaced metadata
        // conservatively stays batch-owned: callbacks can retain/suspend users.
        // Matching records allocate no snapshot; no name index grows with input.
        var bytes = checked(32L + 8L * count);
        using var storage = MemoryGovernor.ReserveTemporary(bytes, span);
        var snapshot = new PyString[count];
        var index = 0;
        foreach (var key in keys)
        {
            if ((index & 1023) == 0) CheckExecution(span);
            snapshot[index++] = (PyString)key;
        }
        CheckExecution(span);
        try { snapshot.AsSpan().Sort(new JsonKeyComparer(this, span)); }
        catch (InvalidOperationException error) when (error.InnerException is LythonRuntimeException failure)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        CheckExecution(span);
        storage.Commit(bytes);
        JsonRecordKeys = snapshot;
    }

    private readonly struct JsonKeyComparer(ExecutionState state, LythonSourceSpan span) : IComparer<PyString>
    {
        public int Compare(PyString? left, PyString? right)
        {
            state.CheckExecution(span);
            return PyString.CompareOrdinal(left!, right!);
        }
    }
}
