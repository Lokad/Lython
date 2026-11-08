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

    // Mutable execution data is distinct from the lexical context's identity.
    // The normal engine owns one inline state; handlers borrow it by reference.
    internal struct ExecutableWorkState
    {
        internal ExecutableCodeObject Code;
        internal object[] Locals;
        internal ExecutableCell?[]? LocalCells;
        internal IReadOnlyList<ExecutableCell>? ClosureCells;
        internal ExecutableValueStorage Stack;
        internal int InstructionIndex;
        // Python None is PyNone; null means no explicit return has completed.
        internal object? ReturnValue;

        public ExecutableWorkState()
        {
            Code = null!;
            Locals = [];
            LocalCells = null;
            ClosureCells = null;
            Stack = default;
            InstructionIndex = 0;
            ReturnValue = null;
        }
    }

    internal sealed partial class ExecutionContext : ExecutionFrame
    {
        internal ExecutableWorkState _execution = new();

        internal void InitializeExecutableBindings(
            ExecutableCodeObject codeObject,
            object[] locals,
            ExecutableCell?[]? localCells,
            IReadOnlyList<ExecutableCell>? closureCells)
        {
            _execution.Code = codeObject;
            _execution.Locals = locals;
            _execution.LocalCells = localCells;
            _execution.ClosureCells = closureCells;
        }

        private void ClearExecutableBindings()
        {
            _execution.Code = null!;
            _execution.Locals = [];
            _execution.LocalCells = null;
            _execution.ClosureCells = null;
        }

        public bool RequiresLocalVariableMirroring => _execution.Code.RequiresLocalVariableMirroring;

        internal int LocalCapacity => _execution.Code.LocalNames.Count;



        internal bool TryReadExecutableLocal(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var slot))
            {
                value = _execution.LocalCells?[slot] is { } cell ? cell.Value : _execution.Locals[slot];
                if (!ReferenceEquals(value, UninitializedLocal)) return true;
            }

            value = null;
            return false;
        }

        public bool TryResolveLocalOrClosure(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var local = _execution.Locals[localSlot];
                if (!ReferenceEquals(local, UninitializedLocal))
                {
                    value = local;
                    return true;
                }
            }

            if (_execution.ClosureCells is not null && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var closure = _execution.ClosureCells[closureSlot].Value;
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
            foreach (var pair in _execution.Code.LocalNameToSlot)
            {
                if (pair.Key.StartsWith('<')) continue;
                var value = _execution.Locals[pair.Value];
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
            foreach (var name in _execution.Code.LocalNames)
                if (!name.StartsWith('<') && TryReadExecutableLocal(name, out var value))
                    yield return new(name, value);
            if (_execution.ClosureCells is null) yield break;
            for (var i = 0; i < _execution.Code.ClosureNames.Count; i++)
            {
                var value = _execution.ClosureCells[i].Value;
                if (!ReferenceEquals(value, UninitializedLocal))
                    yield return new(_execution.Code.ClosureNames[i], value);
            }
        }

        public bool TryGetCell(string name, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_execution.LocalCells is not null && _execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                if (_execution.LocalCells[localSlot] is ExecutableCell localCell)
                {
                    cell = localCell;
                    return true;
                }
            }

            if (_execution.ClosureCells is not null && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                cell = _execution.ClosureCells[closureSlot];
                return true;
            }

            cell = null;
            return false;
        }

        public bool TryGetClosureCell(int slot, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_execution.ClosureCells is not null && slot >= 0 && slot < _execution.ClosureCells.Count)
            {
                cell = _execution.ClosureCells[slot];
                return true;
            }

            cell = null;
            return false;
        }

        public bool TryStoreLocalOrClosure(string name, object value)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                _execution.Locals[localSlot] = value;
                if (_execution.LocalCells?[localSlot] is ExecutableCell localCell)
                {
                    localCell.Value = value;
                }

                return true;
            }

            if (_execution.ClosureCells is not null && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                _execution.ClosureCells[closureSlot].Value = value;
                return true;
            }

            return false;
        }

        public bool TryDeleteLocalOrClosure(string name)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var removed = !ReferenceEquals(_execution.Locals[localSlot], UninitializedLocal);
                _execution.Locals[localSlot] = UninitializedLocal;
                if (_execution.LocalCells?[localSlot] is ExecutableCell localCell)
                {
                    removed |= !ReferenceEquals(localCell.Value, UninitializedLocal);
                    localCell.Value = UninitializedLocal;
                }

                return removed;
            }

            if (_execution.ClosureCells is not null && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var cell = _execution.ClosureCells[closureSlot];
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
    private sealed record PendingJump(int TargetIndex, bool DiscardIterator) : PendingAbruptSignal
    {
        public PendingAbruptSignal? SavedPendingAbrupt { get; set; }
    }

    private sealed record PendingReturn(object Value) : PendingAbruptSignal;

    private sealed record PendingControl(ControlSignal Control) : PendingAbruptSignal;

    // One admitted array holds lookup facts. Entries retain immutable layout/key
    // or member receiver/value pairs; record entries never retain a row's values.
    internal readonly record struct ExecutableLookupCache(object? Target, object? Value, int Position = 0);

    internal static long LookupCacheBytes(int count) => checked(32L + 64L * count);

    internal struct ExecutableValueStorage
    {
        private object[] _items;
        private int[]? _nativeIterationPositions;
        // One reference holds private invocation storage or the existing
        // function definition that owns shared facts. Do not widen every frame
        // with a second owner slot, including frames that never use a cache.
        private object? _lookupStorage;
        private readonly ExecutionState _state;
        private long _ownedStorageBytes;
        private readonly int _firstTemporarySlot;
        private readonly int _lastOwnedTemporarySlot;
        private PyIteration.Cursor? _pendingDisposalHead, _pendingDisposalTail;
        internal bool DeferAsyncDisposal;
        internal bool HasPendingDisposals => _pendingDisposalHead is not null;

        public ExecutableValueStorage(int capacity, int firstTemporarySlot, int lastOwnedTemporarySlot, ExecutionContext context,
            LythonSourceSpan? allocationSpan, long frameStorageBytes, object[]? borrowedValues,
            PyExecutableFunction? lookupOwner)
        {
            _state = context.State;
            _firstTemporarySlot = firstTemporarySlot;
            _lastOwnedTemporarySlot = lastOwnedTemporarySlot;
            _nativeIterationPositions = null;
            _lookupStorage = lookupOwner;
            var bytes = checked(frameStorageBytes + (borrowedValues is null ? EstimateObjectArrayBytes(capacity) : 0));
            var governor = context.MemoryGovernor;
            governor.Reserve(bytes, allocationSpan);
            try { _items = borrowedValues ?? new object[capacity]; governor.Commit(bytes); }
            catch { governor.ReleaseReserved(bytes); throw; }
            _ownedStorageBytes = bytes;
            LiveExtent = firstTemporarySlot;
        }

        internal object[] Values => _items;

        internal readonly void CheckExecution(LythonSourceSpan span) => _state.CheckExecution(span);

        internal void ReleaseStorage()
        {
            _items = [];
            _nativeIterationPositions = null;
            _lookupStorage = null;
            var bytes = _ownedStorageBytes;
            _ownedStorageBytes = 0;
            _state.MemoryGovernor.Release(bytes);
        }

        internal ExecutableLookupCache[] GetLookupCaches(int count, LythonSourceSpan span)
        {
            if (_lookupStorage is PyExecutableFunction owner) return owner.GetLookupCaches(span);
            if (_lookupStorage is ExecutableLookupCache[] existing) return existing;
            var bytes = LookupCacheBytes(count);
            var ownedBytes = checked(_ownedStorageBytes + bytes);
            var governor = _state.MemoryGovernor;
            governor.Reserve(bytes, span);
            try
            {
                _state.CheckExecution(span);
                var caches = new ExecutableLookupCache[count];
                governor.Commit(bytes);
                _ownedStorageBytes = ownedBytes;
                _lookupStorage = caches;
                return caches;
            }
            catch { governor.ReleaseReserved(bytes); throw; }
        }

        internal void ClearLookupCache(int index)
        {
            if (_lookupStorage is PyExecutableFunction owner) owner.ClearLookupCache(index);
            else if (_lookupStorage is ExecutableLookupCache[] caches) caches[index] = default;
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

        public int LiveExtent { get; private set; }

        public object this[int index]
        {
            get => _items[index].RequireNotNull();
            set => _items[index] = value;
        }

        internal void PublishValue(int resultSlot, int liveExtent, object result)
        {
            // Inputs and their owner stay live through callbacks/suspension.
            // Retire only after success, preserving failed-disposal behavior.
            if (liveExtent < LiveExtent) RetireTo(liveExtent);
            _items[resultSlot] = result;
            LiveExtent = liveExtent;
        }

        internal object TakeValue(int inputSlot, int liveExtent)
        {
            var value = _items[inputSlot].RequireNotNull();
            _items[inputSlot] = null!;
            LiveExtent = liveExtent;
            return value;
        }

        // The completed operation consumed this input, as PublishValue would
        // overwrite it. Its result goes directly to a binding instead.
        internal void ForgetResultInput(int resultSlot)
        {
            _items[resultSlot] = null!;
            LiveExtent = resultSlot;
        }

        internal bool TryPrepareNativeIteration(object source, int slot, int liveExtent, LythonSourceSpan span)
        {
            if (source is not (PyList or PyTuple)) return false;
            if (_nativeIterationPositions is null)
            {
                // The existing private operand keeps the source alive. One
                // admitted position array replaces repeated native cursor and
                // enumerator objects; it shares this frame's storage lifetime.
                var bytes = checked(32L + 4L * (_items.Length - _firstTemporarySlot));
                var frameBytes = checked(_ownedStorageBytes + bytes);
                using var storage = _state.MemoryGovernor.ReserveTemporary(bytes, span);
                var positions = new int[_items.Length - _firstTemporarySlot];
                storage.Commit(bytes);
                _nativeIterationPositions = positions;
                _ownedStorageBytes = frameBytes;
            }
            _nativeIterationPositions[slot - _firstTemporarySlot] = 0;
            PublishValue(slot, liveExtent, source);
            return true;
        }

        internal bool TryAdvanceNativeIteration(int slot, out PyIterationResult next)
        {
            switch (_items[slot])
            {
                case PyList list:
                    var listIndex = _nativeIterationPositions![slot - _firstTemporarySlot];
                    next = listIndex < list.Count ? PyIterationResult.Yield(list[listIndex]) : PyIterationResult.End;
                    if (next.HasValue) _nativeIterationPositions[slot - _firstTemporarySlot] = listIndex + 1;
                    return true;
                case PyTuple tuple:
                    var tupleIndex = _nativeIterationPositions![slot - _firstTemporarySlot];
                    next = tupleIndex < tuple.Count ? PyIterationResult.Yield(tuple[tupleIndex]) : PyIterationResult.End;
                    if (next.HasValue) _nativeIterationPositions[slot - _firstTemporarySlot] = tupleIndex + 1;
                    return true;
                default:
                    next = PyIterationResult.End;
                    return false;
            }
        }

        public ReadOnlyMemory<object?> Slice(int start, int count) => _items.AsMemory(start, count);

        public void RetireTo(int liveExtent)
        {
            if (liveExtent == LiveExtent)
            {
                return;
            }

            var newCount = liveExtent;
            // Compiler-owned wrappers cannot occur above this conservative bound.
            // Still clear every retired guest reference after private cleanup.
            for (var i = Math.Min(LiveExtent - 1, _lastOwnedTemporarySlot); i >= newCount; i--)
            {
                if (DeferAsyncDisposal && _items[i] is PyIteration.Cursor cursor)
                {
                    if (_pendingDisposalTail is null) _pendingDisposalHead = cursor;
                    else _pendingDisposalTail.NextPendingDisposal = cursor;
                    _pendingDisposalTail = cursor;
                    // Ownership moved to the pending chain. A later synchronous
                    // disposal can fail; retrying the trim must not enqueue twice.
                    _items[i] = null!;
                }
                else if (_items[i] is ExecutableTemporaryValue temporary) temporary.Dispose();
            }
            Array.Clear(_items, newCount, LiveExtent - newCount);
            LiveExtent = newCount;
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
                context = new ExecutionContext(host, options, _regexProvider, _regexPrograms);
                InitializeModuleAnnotations(script.AnnotationInitializationSpan, context);
                ExecuteExecutableCodeObject(script.EntryPoint, context);

                // Successful execution publishes outstanding writers instead of
                // silently discarding accepted writes; a publication failure
                // converts to a failure result below.
                context.State.CloseOpenFiles(context!);

                return CreateSuccessfulResult(context, null, options);
            }
            catch (ReturnSignal signal)
            {
                // A top-level return still publishes outstanding writers; a
                // publication failure replaces the return with a failure.
                try
                {
                    context?.State.CloseOpenFiles(context!);
                }
                catch (LythonRuntimeException ex)
                {
                    return CreateRuntimeFailureResult(ex, context, options);
                }

                return CreateSuccessfulResult(context, signal.Value, options);
            }
            catch (LythonRuntimeException ex)
            {
                return CreateRuntimeFailureResult(ex, context, options);
            }
            finally
            {
                try { if (context is not null) context.State.TryCloseOpenFiles(context); }
                finally { context?.State.CloseResources(); }
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
        LythonSourceSpan? entrySpan = null,
        PyExecutableFunction? lookupOwner = null)
    {
        context.EnsureExecutableStateAvailable();
        // Bound function invocations already own their call admission. Raw
        // protocol calls have function/managed-stack protection without an
        // extra logical nesting charge; compiling their bodies preserves that.
        var ownsAdmission = boundArguments.Values is null;
        if (ownsAdmission) context.EnterInterpreterFrame(entrySpan);
        else ExecutionState.EnsureInterpreterStack(entrySpan);
        try
        {
            InitializeExecutableFrame(codeObject, context, boundArguments, closureCells, argumentsAreLocals, lookupOwner);
            // Even a zero-local module has live operands during calls. Publish
            // the same existing frame that the async entry publishes; local
            // slot counts do not determine whether an invocation is active.
            context.EnterExecutableSlots();
            context.Execute();
            return context.HasFrameReturn ? context.FrameReturnValue : null;
        }
        finally
        {
            context.LeaveExecutableSlots();
            context.DetachExecutableExecution();
            if (ownsAdmission) context.LeaveInterpreterFrame();
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
        LythonSourceSpan? entrySpan = null,
        PyExecutableFunction? lookupOwner = null)
    {
        context.EnsureExecutableStateAvailable();
        var ownsAdmission = boundArguments.Values is null;
        if (ownsAdmission) context.EnterInterpreterFrame(entrySpan);
        else ExecutionState.EnsureInterpreterStack(entrySpan);
        try
        {
            InitializeExecutableFrame(codeObject, context, boundArguments, closureCells, argumentsAreLocals, lookupOwner);
            context.EnterExecutableSlots();
            await context.ExecuteAsync().ConfigureAwait(false);
            return context.HasFrameReturn ? context.FrameReturnValue : null;
        }
        finally
        {
            context.LeaveExecutableSlots();
            context.DetachExecutableExecution();
            if (ownsAdmission) context.LeaveInterpreterFrame();
        }
    }

    private static void InitializeExecutableFrame(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals,
        PyExecutableFunction? lookupOwner = null)
    {
        var execution = CreateExecutableWorkState(codeObject, context, boundArguments, closureCells, argumentsAreLocals, lookupOwner);
        context.AdoptExecutableExecution(ref execution, context.State.CurrentException);
    }

    private static ExecutableWorkState CreateExecutableWorkState(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals,
        PyExecutableFunction? lookupOwner = null)
    {
        // InvokeBound owns bound storage until this interpreter has detached,
        // including actual asynchronous suspension. No second local array/copy
        // is needed for nonmirrored executable functions.
        var entry = codeObject.Instructions;
        var span = entry.Length == 0 ? null : entry[0].Span;
        long bytes = checked((long)codeObject.PatternStorageCount * RuntimeMemoryEstimates.ScalarStorageBytes);
        if (codeObject.CapturedLocalSlots.Count != 0)
            bytes = checked(bytes + EstimateObjectArrayBytes(codeObject.LocalNames.Count)
                + 48L * codeObject.CapturedLocalSlots.Count);
        // The compiler-sized operand lifetime funds private frame arrays before any
        // are allocated. Escaping cells acquire their separate retained charge.
        var stack = new ExecutableValueStorage(codeObject.ValueStorageCapacity, codeObject.FirstTemporarySlot, codeObject.LastOwnedTemporarySlot,
            context, span, bytes, argumentsAreLocals ? boundArguments.Values : null, lookupOwner);
        try
        {
            var locals = stack.Values;
            Array.Fill(locals, UninitializedLocal, argumentsAreLocals ? boundArguments.Assigned.Length : 0,
                codeObject.LocalNames.Count - (argumentsAreLocals ? boundArguments.Assigned.Length : 0));
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

            return new ExecutableWorkState
            {
                Code = codeObject,
                Locals = locals,
                LocalCells = localCells,
                ClosureCells = closureCells,
                Stack = stack,
                InstructionIndex = codeObject.EntryIndex
            };
        }
        catch
        {
            stack.ReleaseStorage();
            throw;
        }
    }

}
