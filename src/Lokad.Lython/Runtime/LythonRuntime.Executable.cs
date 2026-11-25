using Lokad.Lython.Frontend;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
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

        internal ExecutableCell(ExecutableSlot value) => StoreSlot(value);

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

        internal bool TryReadInteger(out long value)
        {
            if (_hasInteger && !UsesNamespace) { value = _integer; return true; }
            return TryMachineInteger(Value, out value);
        }

        internal bool CanStoreDirectly => _owner is null || (_name is not null && _owner.GlobalNamespace is null);
        internal bool HasNamespaceOwner => _owner is not null;

        private bool UsesNamespace => _owner is { MirrorsExecutableLocals: true } || _owner?.MirrorLocalStores == true;

        // Only a consumed, unpublished integer can move without creating a
        // guest object. A namespace or tuple borrow still observes its value.
        internal void StoreSlot(ExecutableSlot value)
        {
            if (value.IsUnpublishedInteger && !UsesNamespace && value.TryGetInteger(out var integer))
            {
                _integer = integer;
                _hasInteger = true;
                _value = null;
            }
            else Value = value.Object;
        }

        internal bool TryApplyIntegerArithmetic(BinaryOperatorSyntax operation, long operand,
            ExecutionContext context, LythonSourceSpan arithmeticSpan, LythonSourceSpan storeSpan)
        {
            if (!CanStoreDirectly || UsesNamespace) return false;
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

        // Once retained, the execution owner keeps the cell across frame cleanup.
        internal bool RetentionCharged;

    }

    // One word shared by executable calls and suspended frames. A fixed
    // parameter carries its retained value; mutable bindings share cells.
    internal readonly struct ExecutableCaptures
    {
        private readonly object? _storage;

        private ExecutableCaptures(object storage) => _storage = storage;

        internal static ExecutableCaptures FromCell(ExecutableCell cell) => new(cell);
        internal static ExecutableCaptures FromCells(ExecutableCell[] cells) => new(cells);
        internal static ExecutableCaptures FromValue(object value) => new(value);

        internal bool IsEmpty => _storage is null;

        internal int Count => _storage switch
        {
            null => 0,
            ExecutableCell[] cells => cells.Length,
            _ => 1
        };

        internal ExecutableCell? GetCell(int index) => _storage switch
        {
            ExecutableCell cell when index == 0 => cell,
            ExecutableCell[] cells when (uint)index < (uint)cells.Length => cells[index],
            _ => null
        };

        internal bool TryRead(int index, out object? value)
        {
            if (_storage is ExecutableCell[] cells)
            {
                if ((uint)index >= (uint)cells.Length) { value = null; return false; }
                value = cells[index].Value;
                return true;
            }
            if (index == 0 && _storage is not null)
            {
                value = _storage is ExecutableCell cell ? cell.Value : _storage;
                return true;
            }
            value = null;
            return false;
        }

        // Keep the capture representation out of the inline arithmetic operand
        // readers. Locals and constants need neither a cell nor a carrier test.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool TryReadInteger(int index, out long value)
        {
            if (GetCell(index) is { } cell) return cell.TryReadInteger(out value);
            if (index == 0 && _storage is not ExecutableCell[]) return TryMachineInteger(_storage, out value);
            value = 0;
            return false;
        }

        internal object? FixedValue => _storage is ExecutableCell or ExecutableCell[] ? null : _storage;
    }

    // Mutable execution data is distinct from the lexical context's identity.
    // The normal engine owns one inline state; handlers borrow it by reference.
    internal struct ExecutableWorkState
    {
        internal ExecutableCodeObject Code;
        // Retain the binding loan's return handle after operand storage retires.
        internal ExecutableSlot[] Locals;
        internal ExecutableCaptures Captures;
        internal ExecutableValueStorage Stack;
        internal int InstructionIndex;
        // Only the incoming ordinary call owns this intent; generators, public
        // entry and protocol results retain observable Python return values.
        internal bool ReturnConsumedByIteration;
        // Python None is PyNone; null means no explicit return has completed.
        internal ExecutableSlot ReturnValue;

        public ExecutableWorkState()
        {
            Code = null!;
            Locals = [];
            Captures = default;
            Stack = default;
            InstructionIndex = 0;
            ReturnValue = default;
        }
    }

    internal sealed partial class ExecutionContext : ExecutionFrame
    {
        internal ExecutableWorkState _execution = new();

        private void ClearExecutableBindings()
        {
            _execution.Code = null!;
            _execution.Locals = [];
            _execution.Captures = default;
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

            if (!_execution.Captures.IsEmpty && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                if (_execution.Captures.TryRead(closureSlot, out var closure) && closure is not null)
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
            var code = _execution.Code;
            var cells = code.CapturedLocalSlots;
            var nextCell = code.FirstSnapshotCell;
            for (var slot = 0; slot < code.LocalNames.Count; slot++)
            {
                if (nextCell < cells.Count && cells[nextCell] == slot)
                {
                    nextCell++;
                    continue;
                }
                var name = code.LocalNames[slot];
                if (!name.StartsWith('<') && ReadLocalValue(_execution.Locals, slot) is { } value)
                    yield return new(name, value);
            }

            var tail = code.SnapshotTailOrder;
            var count = tail?.Length ?? code.ClosureNames.Count;
            for (var index = 0; index < count; index++)
            {
                var slot = tail?[index] ?? ~index;
                if (slot < 0)
                {
                    if (_execution.Captures.TryRead(~slot, out var value) && value is not null)
                        yield return new(code.ClosureNames[~slot], value);
                }
                else
                {
                    var name = code.LocalNames[slot];
                    if (!name.StartsWith('<') && ReadLocalValue(_execution.Locals, slot) is { } value)
                        yield return new(name, value);
                }
            }
        }

        public bool TryGetCell(string name, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                if (_execution.Locals[localSlot].Reference is ExecutableCell localCell)
                {
                    cell = localCell;
                    return true;
                }
            }

            if (!_execution.Captures.IsEmpty && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                cell = _execution.Captures.GetCell(closureSlot);
                return cell is not null;
            }

            cell = null;
            return false;
        }

        public bool TryGetClosureCell(int slot, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            cell = _execution.Captures.GetCell(slot);
            return cell is not null;
        }

        public bool TryStoreLocalOrClosure(string name, object value)
        {
            if (_execution.Code.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                WriteLocalValue(_execution.Locals, localSlot, value);

                return true;
            }

            if (!_execution.Captures.IsEmpty && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot) &&
                _execution.Captures.GetCell(closureSlot) is { } cell)
            {
                cell.Value = value;
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

            if (!_execution.Captures.IsEmpty && _execution.Code.ClosureNameToSlot.TryGetValue(name, out var closureSlot) &&
                _execution.Captures.GetCell(closureSlot) is { } cell)
            {
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


    // One admitted array holds lookup facts. Entries retain immutable layout/key
    // or member receiver/value pairs; record entries never retain a row's values.
    internal readonly record struct ExecutableLookupCache(object? Target, object? Value, int Position = 0);

    internal static long LookupCacheBytes(int count) => checked(32L + 64L * count);

    internal struct ExecutableValueStorage
    {
        private ExecutableSlot[] _items;
        private int[]? _nativeIterationPositions;
        // One reference holds private invocation storage or the existing
        // function definition that owns shared facts. Do not widen every frame
        // with a second owner slot, including frames that never use a cache.
        private object? _lookupStorage;
        private readonly ExecutionState _state;
        private long _ownedStorageBytes;
        private readonly int _firstTemporarySlot;
        private readonly int _lastOwnedTemporarySlot;

        public ExecutableValueStorage(int capacity, int firstTemporarySlot, int lastOwnedTemporarySlot, ExecutionContext context,
            LythonSourceSpan? allocationSpan, long frameStorageBytes, ExecutableSlot[]? borrowedValues,
            PyExecutableFunction? lookupOwner)
        {
            _state = context.State;
            _firstTemporarySlot = firstTemporarySlot;
            _lastOwnedTemporarySlot = lastOwnedTemporarySlot;
            _nativeIterationPositions = null;
            _lookupStorage = lookupOwner;
            var bytes = checked(frameStorageBytes + (borrowedValues is null ? ExecutableSlotsBytes(capacity) : 0));
            var governor = context.MemoryGovernor;
            governor.Reserve(bytes, allocationSpan);
            try { _items = borrowedValues ?? new ExecutableSlot[capacity]; governor.Commit(bytes); }
            catch { governor.ReleaseReserved(bytes); throw; }
            _ownedStorageBytes = bytes;
            LiveExtent = firstTemporarySlot;
        }

        internal readonly ExecutableSlot[] Values => _items;
        internal readonly ExecutionState State => _state;

        internal readonly void CheckExecution(LythonSourceSpan span) => _state.CheckExecution(span);

        internal readonly bool CanContinue => _state.CanContinue;

        internal void TransferCapturedCellCharges(int count)
        {
            // These cells are already committed in the shared governor. They
            // outlive frame cleanup under the execution owner.
            var bytes = (long)ExecutableCell.StorageBytes * count;
            System.Diagnostics.Debug.Assert(_ownedStorageBytes >= bytes);
            _ownedStorageBytes -= bytes;
        }

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

        public int LiveExtent { get; private set; }

        internal void RestoreNativeExtent(int liveExtent) => LiveExtent = liveExtent;

        public object this[int index]
        {
            get => _items[index].Object.RequireNotNull();
            set => _items[index].Object = value;
        }

        internal void PublishValue(int resultSlot, int liveExtent, object result)
            => PublishSlot(resultSlot, liveExtent, ExecutableSlot.FromObject(result));

        internal void PublishSlot(int resultSlot, int liveExtent, ExecutableSlot result)
        {
            // Inputs and their owner stay live through callbacks/suspension.
            // Retire only after success, preserving failed-disposal behavior.
            if (liveExtent < LiveExtent) RetireTo(liveExtent);
            _items[resultSlot] = result;
            LiveExtent = liveExtent;
        }

        internal object TakeValue(int inputSlot, int liveExtent)
        {
            var value = _items[inputSlot].Object.RequireNotNull();
            _items[inputSlot].Object = null!;
            LiveExtent = liveExtent;
            return value;
        }

        internal ExecutableSlot TakeSlotValue(int inputSlot, int liveExtent)
        {
            var value = _items[inputSlot];
            if (value.IsUnassigned) _ = value.Object.RequireNotNull();
            _items[inputSlot] = default;
            LiveExtent = liveExtent;
            return value;
        }

        // The completed operation consumed this input, as PublishValue would
        // overwrite it. Its result goes directly to a binding instead.
        internal void ForgetResultInput(int resultSlot)
        {
            _items[resultSlot].Object = null!;
            LiveExtent = resultSlot;
        }

        internal bool TryPrepareNativeIteration(object source, int slot, int liveExtent, LythonSourceSpan span)
        {
            if (source is PyStringOps.CasedWhitespaceIteration)
            {
                PublishValue(slot, liveExtent, source);
                return true;
            }
            if (source is PyRange range)
            {
                // Exact ranges have no user iteration protocol or async cleanup.
                // ForNext already checks execution before advancing this operand.
                PublishValue(slot, liveExtent, range.GetEnumerator());
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

        internal bool TryAdvanceNativeIteration(int slot, out bool hasValue, out ExecutableSlot next)
        {
            var source = _items[slot].Object.RequireNotNull();
            PyIterationResult result;
            switch (source)
            {
                case PyStringOps.CasedWhitespaceIteration words:
                    result = words.MoveNext();
                    break;
                case PyList or PyTuple:
                    result = PyIteration.AdvanceBuiltinSequence(source, ref _nativeIterationPositions![slot - _firstTemporarySlot]);
                    break;
                case PyRange.Enumerator range:
                    result = range.TryMoveNext();
                    break;
                default:
                    hasValue = false;
                    next = default;
                    return false;
            }
            hasValue = result.HasValue;
            next = hasValue ? result.TakeSlotValue() : default;
            return true;
        }

        public Memory<ExecutableSlot> Slice(int start, int count) => _items.AsMemory(start, count);

        internal readonly bool CanRetireWithoutCleanup(int liveExtent)
        {
            for (var i = Math.Min(LiveExtent - 1, _lastOwnedTemporarySlot); i >= liveExtent; i--)
                if (_items[i].RequiresRetirement) return false;
            return true;
        }

        public void RetireTo(int liveExtent)
        {
            if (liveExtent == LiveExtent)
            {
                return;
            }

            var newCount = liveExtent;
            // Compiler-owned values cannot occur above this conservative bound.
            // Still clear every retired guest reference after private cleanup.
            var i = Math.Min(LiveExtent - 1, _lastOwnedTemporarySlot);
            try
            {
                for (; i >= newCount; i--)
                    _items[i].RetireTemporary(_state.MemoryGovernor);
            }
            catch
            {
                // A failing cursor still relinquishes its slot. Retire the rest
                // in order before surfacing the first cleanup failure.
                while (--i >= newCount)
                {
                    try { _items[i].RetireTemporary(_state.MemoryGovernor); }
                    catch { }
                }
                throw;
            }
            finally
            {
                Array.Clear(_items, newCount, LiveExtent - newCount);
                LiveExtent = newCount;
            }
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
                context = new ExecutionContext(host, options, _regexProvider, _regexPrograms, ownsModuleNamespace: true);
                InitializeModuleAnnotations(script.AnnotationInitializationSpan, context);
                ExecuteExecutableCodeObject(script.EntryPoint, context);
                context.ReleaseOwnedModuleExecution();

                // Successful execution publishes outstanding writers instead of
                // silently discarding accepted writes; a publication failure
                // converts to a failure result below.
                context.State.CloseOpenFiles(context!);

                return CreateSuccessfulResult(context, null, options);
            }
            catch (ReturnSignal signal)
            {
                context?.ReleaseOwnedModuleExecution();
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
                try
                {
                    try { context?.ReleaseOwnedModuleExecution(); }
                    finally { if (context is not null) context.State.TryCloseOpenFiles(context); }
                }
                finally { context?.State.CloseResources(); }
            }
        });
    }

    internal static void ExecuteExecutableCodeObject(ExecutableCodeObject codeObject, ExecutionContext context)
    {
        // Module top-level keeps the historical contract: an explicit frame
        // return surfaces once as a signal for Run to project (sync function
        // bodies consume the value directly instead).
        if (ExecuteExecutableCodeObject(codeObject, context, default, default) is { } returnValue)
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
        BoundFunctionArguments boundArguments,
        ExecutableCaptures? closureCells,
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
            InitializeExecutableFrame(codeObject, context, boundArguments, closureCells ?? default, lookupOwner);
            return ExecuteInitializedFrame(context);
        }
        finally
        {
            if (ownsAdmission) context.LeaveInterpreterFrame();
        }
    }

    internal static ValueTask<object?> ExecuteExecutableCodeObjectAsync(ExecutableCodeObject codeObject, ExecutionContext context)
        => ExecuteExecutableCodeObjectAsync(codeObject, context, default, default);

    internal static async ValueTask<object?> ExecuteExecutableCodeObjectAsync(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundFunctionArguments boundArguments,
        ExecutableCaptures closureCells,
        LythonSourceSpan? entrySpan = null,
        PyExecutableFunction? lookupOwner = null)
    {
        context.EnsureExecutableStateAvailable();
        var ownsAdmission = boundArguments.Values is null;
        if (ownsAdmission) context.EnterInterpreterFrame(entrySpan);
        else ExecutionState.EnsureInterpreterStack(entrySpan);
        try
        {
            InitializeExecutableFrame(codeObject, context, boundArguments, closureCells, lookupOwner);
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
        finally
        {
            if (!context.OwnsModuleNamespace)
            {
                context.LeaveExecutableSlots();
                context.DetachExecutableExecution();
            }
        }
    }

    internal static async ValueTask<object?> ExecuteInitializedFrameAsync(ExecutionContext context)
    {
        try
        {
            context.EnterExecutableSlots();
            await context.ExecuteAsync().ConfigureAwait(false);
            return context.HasFrameReturn ? context.FrameReturnValue : null;
        }
        finally
        {
            if (!context.OwnsModuleNamespace)
            {
                context.LeaveExecutableSlots();
                context.DetachExecutableExecution();
            }
        }
    }

    private static void InitializeExecutableFrame(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundFunctionArguments boundArguments,
        ExecutableCaptures closureCells,
        PyExecutableFunction? lookupOwner = null,
        bool returnConsumedByIteration = false, bool continuingNative = false)
    {
        var execution = CreateExecutableWorkState(codeObject, context, boundArguments, closureCells, lookupOwner,
            returnConsumedByIteration, continuingNative: continuingNative);
        context.AdoptExecutableExecution(ref execution, context.State.CurrentException);
    }

    private static ExecutableWorkState CreateExecutableWorkState(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundFunctionArguments boundArguments,
        ExecutableCaptures closureCells,
        PyExecutableFunction? lookupOwner = null,
        bool returnConsumedByIteration = false,
        bool borrowedInvocation = false, bool continuingNative = false)
    {
        // Binding already fills the final local slots. Borrow their storage
        // under the invocation owner, including while a generator is suspended.
        var entry = codeObject.Instructions;
        var span = entry.Length == 0 ? null : entry[0].Span;
        long bytes = checked((long)codeObject.PatternStorageCount * RuntimeMemoryEstimates.ScalarStorageBytes);
        var newCells = codeObject.CapturedLocalSlots.Count;
        if (continuingNative && newCells != 0)
        {
            foreach (var slot in codeObject.CapturedLocalSlots)
                if (boundArguments.Values[slot].Reference is ExecutableCell) newCells--;
        }
        bytes = checked(bytes + ExecutableCell.StorageBytes * newCells);
        // Admit private frame arrays and cells before allocating them. An
        // escaping cell keeps its original charge beyond this frame.
        var stack = new ExecutableValueStorage(codeObject.ValueStorageCapacity, codeObject.FirstTemporarySlot, codeObject.LastOwnedTemporarySlot,
            context, span, bytes, boundArguments.Values, lookupOwner);
        try
        {
            var locals = stack.Values;
            // A captured slot holds its cell, never a second cached value. The
            // frame array and escaped closures borrow this same identity.
            // A borrowed invocation uses the defining scope as its execution
            // authority. That scope's namespace flags do not describe its locals.
            var standalone = codeObject.UsesOnlyLexicalSlots && (borrowedInvocation || !context.MirrorLocalStores &&
                !context.IsClassBody && !context.IsAnnotationScope && context.InlinedComprehensionLookup is null);
            for (var i = 0; i < codeObject.CapturedLocalSlots.Count; i++)
            {
                var slot = codeObject.CapturedLocalSlots[i];
                if (continuingNative && locals[slot].Reference is ExecutableCell) continue;
                locals[slot].Object = standalone
                    ? new ExecutableCell(locals[slot])
                    : new ExecutableCell(locals[slot].Object, context, codeObject.LocalNames[slot]);
            }

            return new ExecutableWorkState
            {
                Code = codeObject,
                Locals = locals,
                Captures = closureCells,
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
