using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Python None is represented by PyNone; this sentinel is the only non-value
    // stored in executable local slots and keeps the slot arrays non-nullable.
    private static readonly object UninitializedLocal = new();

    internal sealed class ExecutableCell
    {
        public ExecutableCell(object value) => Value = value;

        public object Value { get; set; }

        // MG11: set once a closure owns this cell (first-wins sharing).
        internal bool RetentionCharged;
    }

    internal sealed class ExecutableFrameState
    {
        private readonly ExecutableCodeObject _codeObject;
        private readonly object[] _locals;
        private readonly ExecutableCell?[]? _localCells;
        private readonly IReadOnlyList<ExecutableCell>? _closureCells;

        public ExecutableFrameState(
            ExecutableCodeObject codeObject,
            object[] locals,
            ExecutableCell?[]? localCells,
            IReadOnlyList<ExecutableCell>? closureCells)
        {
            _codeObject = codeObject;
            _locals = locals;
            _localCells = localCells;
            _closureCells = closureCells;
        }

        public bool TryResolveLocalOrClosure(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var local = _locals[localSlot];
                if (!ReferenceEquals(local, UninitializedLocal))
                {
                    value = local;
                    return true;
                }
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var closure = _closureCells[closureSlot].Value;
                if (!ReferenceEquals(closure, UninitializedLocal))
                {
                    value = closure;
                    return true;
                }
            }

            value = PyNone.Instance;
            return false;
        }

        public IEnumerable<KeyValuePair<string, object>> EnumerateLocals()
        {
            foreach (var pair in _codeObject.LocalNameToSlot)
            {
                var value = _locals[pair.Value];
                if (!ReferenceEquals(value, UninitializedLocal))
                {
                    yield return new KeyValuePair<string, object>(pair.Key, value);
                }
            }
        }

        public bool TryGetCell(string name, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_localCells is not null && _codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                if (_localCells[localSlot] is ExecutableCell localCell)
                {
                    cell = localCell;
                    return true;
                }
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                cell = _closureCells[closureSlot];
                return true;
            }

            cell = null;
            return false;
        }

        public bool TryGetClosureCell(int slot, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_closureCells is not null && slot >= 0 && slot < _closureCells.Count)
            {
                cell = _closureCells[slot];
                return true;
            }

            cell = null;
            return false;
        }

        public bool TryStoreLocalOrClosure(string name, object value)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                _locals[localSlot] = value;
                if (_localCells?[localSlot] is ExecutableCell localCell)
                {
                    localCell.Value = value;
                }

                return true;
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                _closureCells[closureSlot].Value = value;
                return true;
            }

            return false;
        }

        public bool TryDeleteLocalOrClosure(string name)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var removed = !ReferenceEquals(_locals[localSlot], UninitializedLocal);
                _locals[localSlot] = UninitializedLocal;
                if (_localCells?[localSlot] is ExecutableCell localCell)
                {
                    removed |= !ReferenceEquals(localCell.Value, UninitializedLocal);
                    localCell.Value = UninitializedLocal;
                }

                return removed;
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var cell = _closureCells[closureSlot];
                var removed = !ReferenceEquals(cell.Value, UninitializedLocal);
                cell.Value = UninitializedLocal;
                return removed;
            }

            return false;
        }
    }

    // A finally block may delay exactly one abrupt outcome. Distinct variants
    // prevent an impossible empty or multiply-populated pending state.
    private abstract record PendingAbruptSignal;

    private sealed record PendingException(LythonRuntimeException Exception) : PendingAbruptSignal;

    // A pending return carries the value, not a signal object: ordinary returns
    // deliver through this record without ever throwing (see DeliverReturn).
    private sealed record PendingReturn(object Value) : PendingAbruptSignal;

    private sealed record PendingControl(ControlSignal Control) : PendingAbruptSignal;

    private sealed record ExecutableMemberCache(object Target, object Value);

    private sealed record ExecutableCallCache(object Target, ICallable Callable);

    private sealed class ExecutableValueStack
    {
        private object?[] _items;

        public ExecutableValueStack(int capacity)
        {
            _items = new object[Math.Max(4, capacity)];
        }

        public int Count { get; private set; }

        public object this[int index] => _items[index].RequireNotNull();

        public void Push(object value)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, checked(_items.Length * 2));
            }

            _items[Count++] = value;
        }

        public object Pop()
        {
            var index = Count - 1;
            var value = _items[index].RequireNotNull();
            _items[index] = null;
            Count = index;
            return value;
        }

        public object Peek() => _items[Count - 1].RequireNotNull();

        public void RemoveTail(int count)
        {
            if (count == 0)
            {
                return;
            }

            var newCount = Count - count;
            Array.Clear(_items, newCount, count);
            Count = newCount;
        }
    }

    public LythonExecutionResult Run(
        ExecutableScript script,
        ILythonHost host,
        LythonRunOptions? options)
    {
        return RunOnDedicatedStack(() =>
        {
            ExecutionContext? context = null;
            try
            {
                context = new ExecutionContext(host, options);
                ExecuteExecutableCodeObject(script.EntryPoint, context);

                return CreateSuccessfulResult(context, null, options);
            }
            catch (ReturnSignal signal)
            {
                return CreateReturnedResult(signal, context, options);
            }
            catch (LythonRuntimeException ex)
            {
                return CreateRuntimeFailureResult(ex, context, options);
            }
        });
    }

    internal static void ExecuteExecutableCodeObject(ExecutableCodeObject codeObject, ExecutionContext context)
        => ExecuteExecutableCodeObject(codeObject, context, default, Array.Empty<int>(), null);

    // Bound arguments arrive in plan layout order with a precomputed
    // layout-to-slot map (or -1 for names that are not frame locals).
    // Indexed stores replace the previous per-local name lookups.
    internal static void ExecuteExecutableCodeObject(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        int[] argumentSlotMap,
        IReadOnlyList<ExecutableCell>? closureCells)
    {
        var previousExecutableFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(null);
        try
        {
            var locals = new object[codeObject.LocalNames.Count];
            Array.Fill(locals, UninitializedLocal);
            var needsLocalCells = codeObject.CapturedLocalSlots.Count != 0;
            ExecutableCell?[]? localCells = null;
            if (needsLocalCells)
            {
                localCells = new ExecutableCell[codeObject.LocalNames.Count];
                for (var i = 0; i < codeObject.CapturedLocalSlots.Count; i++)
                {
                    localCells[codeObject.CapturedLocalSlots[i]] = new ExecutableCell(UninitializedLocal);
                }
            }

            for (var i = 0; i < argumentSlotMap.Length; i++)
            {
                var slot = argumentSlotMap[i];
                if (slot < 0)
                {
                    continue;
                }

                var value = boundArguments.Values[i];
                locals[slot] = value;
                if (localCells?[slot] is ExecutableCell localCell)
                {
                    localCell.Value = value;
                }
            }

            if (closureCells is not null || localCells is not null || codeObject.LocalNames.Count != 0)
            {
                context.EnterExecutableSlots(new ExecutableFrameState(codeObject, locals, localCells, closureCells));
            }

            new ExecutableFrameInterpreter(codeObject, context, locals, localCells).Execute();
        }
        finally
        {
            context.LeaveExecutableSlots(previousExecutableFrame);
            context.LeaveInterpreterFrame();
        }
    }

}
