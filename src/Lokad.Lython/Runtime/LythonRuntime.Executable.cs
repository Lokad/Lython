using Lokad.Lython.Frontend;
using System.Collections;
using System.Numerics;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Null is internal unbound storage; every guest None is PyNone.
    // Cleared workspaces need no separate sentinel initialization.

    internal sealed class ExecutableCell
    {
        // Cell metadata and one observed current scalar. Older aliases retain
        // their existing container/execution funding; updates refund nothing.
        internal const long StorageBytes = 96;
        private object? _value;
        private long _integer;
        private bool _hasInteger;
        private readonly ExecutionContext? _owner;
        private readonly string? _name;

        public ExecutableCell(object? value, ExecutionContext? owner = null, string? name = null)
        {
            _value = value;
            _owner = owner;
            _name = name;
        }

        // Tree-walk methods and executable siblings share the defining frame's
        // mirrored namespace even after its executable slots have been released.
        public object? Value
        {
            get
            {
                if (UsesNamespace) return _owner!.Variables.GetValueOrDefault(_name!);
                // Publish one stable immutable identity only if it is observed.
                if (_hasInteger && _value is null) _value = (BigInteger)_integer;
                return _value;
            }
            set
            {
                _hasInteger = false;
                _value = value;
                if (UsesNamespace)
                {
                    if (value is null) _owner!.Variables.Remove(_name!);
                    else _owner!.Variables[_name!] = value;
                }
            }
        }

        internal bool CanStoreThroughOwner => _owner is not null && _name is not null && _owner.GlobalNamespace is null;

        private bool UsesNamespace => _owner is { MirrorsExecutableLocals: true } || _owner?.MirrorLocalStores == true;

        internal bool TryApplyIntegerArithmetic(BinaryOperatorSyntax operation, long operand,
            ExecutionContext context, LythonSourceSpan arithmeticSpan, LythonSourceSpan storeSpan)
        {
            if (!CanStoreThroughOwner || UsesNamespace) return false;
            long prior;
            if (_hasInteger) prior = _integer;
            else if (!TryMachineInteger(_value, out prior)) return false;
            context.CheckExecution(arithmeticSpan);
            if (!TryMachineBinary(operation, prior, operand, out var next)) return false;
            context.CheckExecution(storeSpan);
            _integer = next;
            _hasInteger = true;
            _value = null;
            return true;
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
        internal IReadOnlyList<ExecutableCell>? ClosureCells;
        internal ExecutableValueStorage Stack;
        internal int InstructionIndex;
        // Only the incoming ordinary call owns this intent; generators, public
        // entry and protocol results retain observable Python return values.
        internal bool ReturnConsumedByIteration;
        // Python None is PyNone; null means no explicit return has completed.
        internal object? ReturnValue;

        public ExecutableWorkState()
        {
            Code = null!;
            Locals = [];
            ClosureCells = null;
            Stack = default;
            InstructionIndex = 0;
            ReturnValue = null;
        }
    }

    internal sealed partial class ExecutionContext : ExecutionFrame
    {
        internal ExecutableWorkState _execution = new();

        private void ClearExecutableBindings()
        {
            _execution.Code = null!;
            _execution.Locals = [];
            _execution.ClosureCells = null;
        }

        public bool RequiresLocalVariableMirroring => _execution.Code.RequiresLocalVariableMirroring;

        internal int LocalCapacity => _execution.Code.LocalNames.Count;



        internal bool TryReadExecutableLocal(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var slot))
            {
                value = ReadLocalValue(_execution.Locals, slot);
                if (value is not null) return true;
            }

            value = null;
            return false;
        }

        public bool TryResolveLocalOrClosure(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var local = ReadLocalValue(_execution.Locals, localSlot);
                if (local is not null)
                {
                    value = local;
                    return true;
                }
            }

            if (_execution.ClosureCells is not null && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var closure = _execution.ClosureCells[closureSlot].Value;
                if (closure is not null)
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
                var value = ReadLocalValue(_execution.Locals, pair.Value);
                if (value is not null)
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
                if (value is not null)
                    yield return new(_execution.Code.ClosureNames[i], value);
            }
        }

        public bool TryGetCell(string name, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                if (_execution.Locals[localSlot] is ExecutableCell localCell)
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
                WriteLocalValue(_execution.Locals, localSlot, value);

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
                var removed = ReadLocalValue(_execution.Locals, localSlot) is not null;
                WriteLocalValue(_execution.Locals, localSlot, null);

                return removed;
            }

            if (_execution.ClosureCells is not null && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var cell = _execution.ClosureCells[closureSlot];
                var removed = cell.Value is not null;
                cell.Value = null;
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
        internal readonly bool HasPendingDisposals => _pendingDisposalHead is not null;

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

        internal void RestoreNativeExtent(int liveExtent) => LiveExtent = liveExtent;

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
            if (source is PyStringOps.CasedWhitespaceIteration)
            {
                PublishValue(slot, liveExtent, source);
                return true;
            }
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
                case PyStringOps.CasedWhitespaceIteration words:
                    next = words.MoveNext();
                    return true;
                case PyList or PyTuple:
                    next = PyIteration.AdvanceBuiltinSequence(_items[slot], ref _nativeIterationPositions![slot - _firstTemporarySlot]);
                    return true;
                default:
                    next = PyIterationResult.End;
                    return false;
            }
        }

        public ReadOnlyMemory<object?> Slice(int start, int count) => _items.AsMemory(start, count);

        internal readonly bool CanRetireWithoutCleanup(int liveExtent)
        {
            if (HasPendingDisposals) return false;
            for (var i = Math.Min(LiveExtent - 1, _lastOwnedTemporarySlot); i >= liveExtent; i--)
                if (_items[i] is PyIteration.Cursor or ExecutableTemporaryValue) return false;
            return true;
        }

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
                else if (_items[i] is ExecutableTemporaryValue temporary)
                {
                    // Ownership leaves this slot before cleanup can reenter or
                    // fail, just as on the native and asynchronous paths. A retry
                    // must not dispose a returned loan that now has another user.
                    _items[i] = null!;
                    temporary.Dispose();
                }
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
            return ExecuteInitializedFrame(context);
        }
        finally
        {
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
            return await ExecuteInitializedFrameAsync(context).ConfigureAwait(false);
        }
        finally
        {
            if (ownsAdmission) context.LeaveInterpreterFrame();
        }
    }

    internal static object? ExecuteInitializedFrame(ExecutionContext context)
    {
        try
        {
            context.EnterExecutableSlots();
            context.Execute();
            return context.HasFrameReturn ? context.FrameReturnValue : null;
        }
        finally { context.LeaveExecutableSlots(); context.DetachExecutableExecution(); }
    }

    internal static async ValueTask<object?> ExecuteInitializedFrameAsync(ExecutionContext context)
    {
        try
        {
            context.EnterExecutableSlots();
            await context.ExecuteAsync().ConfigureAwait(false);
            return context.HasFrameReturn ? context.FrameReturnValue : null;
        }
        finally { context.LeaveExecutableSlots(); context.DetachExecutableExecution(); }
    }

    private static void InitializeExecutableFrame(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals,
        PyExecutableFunction? lookupOwner = null,
        bool returnConsumedByIteration = false)
    {
        var execution = CreateExecutableWorkState(codeObject, context, boundArguments, closureCells, argumentsAreLocals, lookupOwner, returnConsumedByIteration);
        context.AdoptExecutableExecution(ref execution, context.State.CurrentException);
    }

    private static ExecutableWorkState CreateExecutableWorkState(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments boundArguments,
        IReadOnlyList<ExecutableCell>? closureCells,
        bool argumentsAreLocals,
        PyExecutableFunction? lookupOwner = null,
        bool returnConsumedByIteration = false)
    {
        // InvokeBound owns bound storage until this interpreter has detached,
        // including actual asynchronous suspension. No second local array/copy
        // is needed for executable functions, including mirrored namespaces.
        var entry = codeObject.Instructions;
        var span = entry.Length == 0 ? null : entry[0].Span;
        long bytes = checked((long)codeObject.PatternStorageCount * RuntimeMemoryEstimates.ScalarStorageBytes);
        if (codeObject.CapturedLocalSlots.Count != 0)
            bytes = checked(bytes + ExecutableCell.StorageBytes * codeObject.CapturedLocalSlots.Count);
        // The compiler-sized operand lifetime funds private frame arrays before any
        // are allocated. Escaping cells acquire their separate retained charge.
        var stack = new ExecutableValueStorage(codeObject.ValueStorageCapacity, codeObject.FirstTemporarySlot, codeObject.LastOwnedTemporarySlot,
            context, span, bytes, argumentsAreLocals ? boundArguments.Values : null, lookupOwner);
        try
        {
            var locals = stack.Values;
            var argumentValues = boundArguments.Values ?? Array.Empty<object>();
            for (var i = 0; !argumentsAreLocals && i < boundArguments.Assigned.Length; i++)
                locals[i] = argumentValues[i];
            // A captured slot holds its cell, never a second cached value. The
            // frame array and escaped closures borrow this same identity.
            for (var i = 0; i < codeObject.CapturedLocalSlots.Count; i++)
            {
                var slot = codeObject.CapturedLocalSlots[i];
                locals[slot] = new ExecutableCell(locals[slot], context, codeObject.LocalNames[slot]);
            }

            return new ExecutableWorkState
            {
                Code = codeObject,
                Locals = locals,
                ClosureCells = closureCells,
                Stack = stack,
                ReturnConsumedByIteration = returnConsumedByIteration,
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
