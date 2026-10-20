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
        private object _value;
        private readonly ExecutionContext? _owner;
        private readonly string? _name;

        public ExecutableCell(object value, ExecutionContext? owner = null, string? name = null)
        {
            _value = value;
            _owner = owner;
            _name = name;
        }

        // Tree-walk methods and executable siblings share the defining frame's
        // mirrored namespace even after its executable slots have been released.
        public object Value
        {
            get => _owner is { MirrorsExecutableLocals: true } || _owner?.MirrorLocalStores == true
                ? _owner!.Variables.GetValueOrDefault(_name!, UninitializedLocal) : _value;
            set
            {
                _value = value;
                if (_owner is { MirrorsExecutableLocals: true } || _owner?.MirrorLocalStores == true)
                {
                    if (ReferenceEquals(value, UninitializedLocal)) _owner!.Variables.Remove(_name!);
                    else _owner!.Variables[_name!] = value;
                }
            }
        }

        // MG11: set once a closure owns this cell (first-wins sharing).
        internal bool RetentionCharged;
    }

    internal class ExecutableFrameState
    {
        protected readonly ExecutableCodeObject _codeObject;
        protected readonly object[] _locals;
        protected readonly ExecutableCell?[]? _localCells;
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

        public bool RequiresLocalVariableMirroring => _codeObject.RequiresLocalVariableMirroring;

        internal int LocalCapacity => _locals.Length;



        public bool TryResolveOwnLocal(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var slot))
            {
                value = _localCells?[slot] is { } cell ? cell.Value : _locals[slot];
                if (!ReferenceEquals(value, UninitializedLocal)) return true;
            }

            value = null;
            return false;
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
                if (pair.Key.StartsWith('<')) continue;
                var value = _locals[pair.Value];
                if (!ReferenceEquals(value, UninitializedLocal))
                {
                    yield return new KeyValuePair<string, object>(pair.Key, value);
                }
            }
        }

        // Introspection sees current local cells and the free variables actually
        // captured by this frame. Keep raw local enumeration for namespace seeding:
        // it must not copy closure snapshots into a live parent lookup chain.
        public IEnumerable<KeyValuePair<string, object>> EnumerateVisibleLocals()
        {
            foreach (var name in _codeObject.LocalNames)
                if (!name.StartsWith('<') && TryResolveOwnLocal(name, out var value))
                    yield return new(name, value);
            if (_closureCells is null) yield break;
            for (var i = 0; i < _codeObject.ClosureNames.Count; i++)
            {
                var value = _closureCells[i].Value;
                if (!ReferenceEquals(value, UninitializedLocal))
                    yield return new(_codeObject.ClosureNames[i], value);
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
    private abstract record PendingAbruptSignal
    {
        public int? CleanupId { get; set; }

    }

    private sealed record PendingException(LythonRuntimeException Exception) : PendingAbruptSignal;

    // A pending return carries the value, not a signal object: ordinary returns
    // deliver through this record without ever throwing (see DeliverReturn).
    private sealed record PendingJump(int TargetBlock, bool DiscardIterator) : PendingAbruptSignal
    {
        public PendingAbruptSignal? SavedPendingAbrupt { get; set; }
    }

    private sealed record PendingReturn(object Value) : PendingAbruptSignal;

    private sealed record PendingControl(ControlSignal Control) : PendingAbruptSignal;

    private sealed record ExecutableMemberCache(object Target, object Value)
    {
    }

    private struct ExecutableValueStack
    {
        private object?[] _items;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan? _allocationSpan;
        private PyIteration.Cursor? _pendingDisposalHead, _pendingDisposalTail;
        internal bool DeferAsyncDisposal;
        internal bool HasPendingDisposals => _pendingDisposalHead is not null;

        public ExecutableValueStack(int capacity, ExecutionContext context, LythonSourceSpan? allocationSpan)
        {
            _context = context;
            _allocationSpan = allocationSpan;
            var governor = context.MemoryGovernor;
            capacity = Math.Max(4, capacity);
            var bytes = EstimateObjectArrayBytes(capacity);
            governor.Reserve(bytes, allocationSpan);
            try { _items = new object[capacity]; governor.Commit(bytes); }
            catch { governor.ReleaseReserved(bytes); throw; }
            Count = 0;
        }

        internal long RetainedBytes => _items.Length == 0 ? 0 : EstimateObjectArrayBytes(_items.Length);

        internal void ReleaseStorage()
        {
            var bytes = RetainedBytes;
            _items = Array.Empty<object?>();
            _context.MemoryGovernor.Release(bytes);
        }

        internal PyIteration.Cursor? TakePendingDisposal()
        {
            var cursor = _pendingDisposalHead;
            if (cursor is null) return null;
            _pendingDisposalHead = cursor.NextPendingDisposal;
            cursor.NextPendingDisposal = null;
            if (_pendingDisposalHead is null) _pendingDisposalTail = null;
            return cursor;
        }

        public int Count { get; private set; }

        public object this[int index]
        {
            get => _items[index].RequireNotNull();
            set => _items[index] = value;
        }

        public void Push(object value)
        {
            if (Count == _items.Length)
            {
                var capacity = checked(_items.Length * 2);
                var bytes = EstimateObjectArrayBytes(capacity);
                var previous = RetainedBytes;
                var governor = _context.MemoryGovernor;
                var span = _context.CurrentExecutableFrame is ExecutableFrameInterpreter frame
                    ? frame.CurrentInstructionSpan ?? _allocationSpan : _allocationSpan;
                // Full new capacity overlaps the still-owned old buffer.
                governor.Reserve(bytes, span);
                try { Array.Resize(ref _items, capacity); governor.Commit(bytes); }
                catch { governor.ReleaseReserved(bytes); throw; }
                governor.Release(previous);
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

        public ReadOnlyMemory<object?> Slice(int start, int count) => _items.AsMemory(start, count);

        public void RemoveTail(int count)
        {
            if (count == 0)
            {
                return;
            }

            var newCount = Count - count;
            for (var i = Count - 1; i >= newCount; i--)
            {
                if (DeferAsyncDisposal && _items[i] is PyIteration.Cursor cursor)
                {
                    if (_pendingDisposalTail is null) _pendingDisposalHead = cursor;
                    else _pendingDisposalTail.NextPendingDisposal = cursor;
                    _pendingDisposalTail = cursor;
                    // Ownership moved to the pending chain. A later synchronous
                    // disposal can fail; retrying the trim must not enqueue twice.
                    _items[i] = null;
                }
                else if (_items[i] is IExecutableTemporaryValue temporary) temporary.Dispose();
            }
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
                context = new ExecutionContext(host, options, _regexProvider);
                InitializeModuleAnnotations(script.AnnotationInitializationSpan, context);
                ExecuteExecutableCodeObject(script.EntryPoint, context);

                // Successful execution publishes outstanding writers instead of
                // silently discarding accepted writes; a publication failure
                // converts to a failure result below.
                context.Services.State.CloseOpenFileWriters(context!);

                return CreateSuccessfulResult(context, null, options);
            }
            catch (ReturnSignal signal)
            {
                // A top-level return still publishes outstanding writers; a
                // publication failure replaces the return with a failure.
                try
                {
                    context?.Services.State.CloseOpenFileWriters(context!);
                }
                catch (LythonRuntimeException ex)
                {
                    return CreateRuntimeFailureResult(ex, context, options);
                }

                return CreateReturnedResult(signal, context, options);
            }
            catch (LythonRuntimeException ex)
            {
                return CreateRuntimeFailureResult(ex, context, options);
            }
        });
    }

    internal static void ExecuteExecutableCodeObject(ExecutableCodeObject codeObject, ExecutionContext context)
    {
        // Module top-level keeps the historical contract: an explicit frame
        // return surfaces once as a signal for Run to project (sync function
        // bodies consume the value directly instead).
        if (ExecuteExecutableCodeObject(codeObject, context, default, null) is { } returnValue)
        {
            throw new ReturnSignal(returnValue);
        }
    }

    // Bound arguments occupy the compiler's parameter prefix in plan layout order.
    // Returns the frame's explicit return value, or null when the body fell
    // through (an explicit guest return is never raw null).
    internal static object? ExecuteExecutableCodeObject(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals = false,
        LythonSourceSpan? entrySpan = null)
    {
        var previousExecutableFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            var interpreter = CreateExecutableInterpreter(codeObject, context, boundArguments, closureCells, argumentsAreLocals);
            // Even a zero-local module has live operands during calls. Publish
            // the same existing frame that the async entry publishes; local
            // slot counts do not determine whether an invocation is active.
            context.EnterExecutableSlots(interpreter);
            interpreter.Execute();
            return interpreter.HasFrameReturn ? interpreter.FrameReturnValue : null;
        }
        finally
        {
            context.LeaveExecutableSlots(previousExecutableFrame);
            context.LeaveInterpreterFrame();
        }
    }

    internal static ValueTask<object?> ExecuteExecutableCodeObjectAsync(ExecutableCodeObject codeObject, ExecutionContext context)
        => ExecuteExecutableCodeObjectAsync(codeObject, context, default, null);

    internal static async ValueTask<object?> ExecuteExecutableCodeObjectAsync(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals = false,
        LythonSourceSpan? entrySpan = null)
    {
        var previousExecutableFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            var interpreter = CreateExecutableInterpreter(codeObject, context, boundArguments, closureCells, argumentsAreLocals);
            context.EnterExecutableSlots(interpreter);
            await interpreter.ExecuteAsync().ConfigureAwait(false);
            return interpreter.HasFrameReturn ? interpreter.FrameReturnValue : null;
        }
        finally
        {
            context.LeaveExecutableSlots(previousExecutableFrame);
            context.LeaveInterpreterFrame();
        }
    }

    private static ExecutableFrameInterpreter CreateExecutableInterpreter(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals)
    {
        // InvokeBound owns bound storage until this interpreter has detached,
        // including actual asynchronous suspension. No second local array/copy
        // is needed for nonmirrored executable functions.
        var locals = argumentsAreLocals ? boundArguments.Values : new object[codeObject.LocalNames.Count];
        Array.Fill(locals, UninitializedLocal, argumentsAreLocals ? boundArguments.Assigned.Length : 0,
            locals.Length - (argumentsAreLocals ? boundArguments.Assigned.Length : 0));
        var needsLocalCells = codeObject.CapturedLocalSlots.Count != 0;
        ExecutableCell?[]? localCells = null;
        if (needsLocalCells)
        {
            localCells = new ExecutableCell[codeObject.LocalNames.Count];
            for (var i = 0; i < codeObject.CapturedLocalSlots.Count; i++)
            {
                var slot = codeObject.CapturedLocalSlots[i];
                localCells[slot] = new ExecutableCell(argumentsAreLocals ? locals[slot] : UninitializedLocal, context, codeObject.LocalNames[slot]);
            }
        }

        var argumentValues = boundArguments.Values ?? Array.Empty<object>();
        for (var i = 0; !argumentsAreLocals && i < boundArguments.Assigned.Length; i++)
        {
            var value = argumentValues[i];
            locals[i] = value;
            if (localCells?[i] is ExecutableCell localCell)
            {
                localCell.Value = value;
            }
        }

        return new ExecutableFrameInterpreter(codeObject, context, locals, localCells, closureCells);
    }

}
