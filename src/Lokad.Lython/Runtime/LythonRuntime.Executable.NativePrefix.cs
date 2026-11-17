using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal const int NativeValueCapacity = 16;
    // Eligibility only: deeper calls continue in the existing linear runner.
    // Use its logical depth, without a second counter or a guest recursion limit.
    private const int NativeCallDepth = 8;

    // This view cannot survive a suspension. The final executable slots and PC
    // are also the ordinary frame's representation; transfer never replays work.
    internal ref partial struct NativeExecution(PyExecutableFunction function, Span<ExecutableSlot> values, bool consumedByIteration, int unpackedReturnCount)
    {
        private readonly PyExecutableFunction _function = function;
        internal readonly ExecutableCodeObject Code = function.CodeObject;
        internal readonly Span<ExecutableSlot> Values = values;
        private readonly IReadOnlyList<ExecutableCell> _cells = function.ClosureCells;
        private readonly ExecutionContext _scope = function.DefinitionContext;
        private readonly bool _consumedByIteration = consumedByIteration;
        private readonly int _unpackedReturnCount = unpackedReturnCount;
        private int _unpackedTupleStart = -1;
        internal bool ReturnedUnpacked => _unpackedTupleStart >= 0;
        internal int Index = function.CodeObject.EntryIndex;
        internal int LiveExtent = function.CodeObject.FirstTemporarySlot;
        private bool _transferred;
        private ExecutionContext? _preparedChild;
        private MemoryGovernor? _preparedChildOwner;
        internal LythonRuntimeException? Failure { get; private set; }

        internal bool Run(LythonSourceSpan entrySpan, out ExecutableSlot result, Span<ExecutableSlot> unpackedReturn = default)
        {
            // The call boundary owns logical interpreter admission, and
            // StartInline owns function admission. Like an ordinary bound body,
            // validate this managed stack entry without charging nesting twice.
            ExecutionState.EnsureInterpreterStack(entrySpan);
            var previous = _scope.State.CurrentException;
            try
            {
                var completed = RunCore(out result);
                if (completed && ReturnedUnpacked)
                {
                    Values.Slice(_unpackedTupleStart, _unpackedReturnCount).CopyTo(unpackedReturn);
                    Trim(Code.FirstTemporarySlot);
                    _scope.CheckExecution(Code.Instructions[Index + 1].Span);
                }
                return completed;
            }
            catch (LythonRuntimeException error)
            {
                if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                    error.PythonContext ??= previous;
                if (Code.ExceptionRegions.Count == 0) throw;
                // First refusal and cancellation still fail the command. A
                // catchable guest fault transfers once, without replaying its
                // operation or building a second exception dispatcher here.
                _scope.CheckExecution(entrySpan);
                Failure = error;
                result = ExecutableSlot.FromObject(PyNone.Instance);
                return false;
            }
            finally
            {
                _scope.State.SetCurrentException(previous);
            }
        }

        private bool RunCore(out ExecutableSlot result)
        {
            result = ExecutableSlot.FromObject(PyNone.Instance);
            while (Index < Code.NativePrefixEnd)
            {
                ref readonly var instruction = ref Code.Instructions[Index];
                _scope.State.CheckExecution(instruction.Span);
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Complete:
                    case ExecutableOpCode.ReturnNone:
                        if (Code.NativePrefixEnd != Code.Instructions.Length) return false;
                        Trim(Code.FirstTemporarySlot);
                        return true;
                    case ExecutableOpCode.Return:
                        if (Code.NativePrefixEnd != Code.Instructions.Length) return false;
                        result = ReadReturnSlot(instruction.InputStart);
                        if (instruction.InputStart >= Code.FirstTemporarySlot) Values[instruction.InputStart].Object = null!;
                        Trim(Code.FirstTemporarySlot);
                        return true;
                    case ExecutableOpCode.LoadConst:
                        Publish(instruction, RuntimeValue(Code.Constants[instruction.ConstantIndex]));
                        break;
                    case ExecutableOpCode.LoadLocal:
                        Publish(instruction, Read(instruction.LocalSlot, 0));
                        break;
                    case ExecutableOpCode.LoadGlobal:
                        Publish(instruction, ResolveExecutableGlobal(Code.Names[instruction.NameIndex], instruction.Span, _scope));
                        break;
                    case ExecutableOpCode.LoadClosure:
                        if (TryExecuteClosureArithmetic(Code, _cells, Index, LiveExtent, _scope))
                        { Index += 2; break; }
                        if (!TryCell(instruction.ClosureSlot, out var readCell) || readCell.Value is not { } captured) return false;
                        Publish(instruction, captured);
                        break;
                    case ExecutableOpCode.StoreClosure:
                        if (!TryCell(instruction.ClosureSlot, out var writeCell) || !writeCell.CanStoreThroughOwner) return false;
                        var stored = Take(instruction);
                        writeCell.Value = stored;
                        break;
                    case ExecutableOpCode.StoreLocal:
                        Values[instruction.LocalSlot].Object = Take(instruction);
                        break;
                    case ExecutableOpCode.LoadCallMember:
                    {
                        var receiver = Read(instruction.InputStart, 0);
                        var name = Code.Names[instruction.NameIndex];
                        var method = ResolveBuiltinReceiverMethod(receiver, name);
                        if (method is null) return false;
                        Put(instruction.ResultSlot, instruction.ResultSlot + 1, receiver);
                        Put(instruction.ResultSlot + 1, instruction.LiveExtent, method);
                        break;
                    }
                    case ExecutableOpCode.Call:
                        if (!Call(instruction)) return false;
                        break;
                    case ExecutableOpCode.Subscript:
                    {
                        var target = Read(instruction.InputStart, 0);
                        if (TryReadMachineOperand(Code, Values, instruction.SecondInput, out var index)
                            && PyIndexing.TryReadSequenceIndex(target, index, instruction.Span, _scope, out var item))
                        {
                            Publish(instruction, item);
                            break;
                        }
                        var key = Read(instruction.SecondInput, 1);
                        if (target is not PyDict { HasProtocolKeys: false } dictionary || key is not PyString field) return false;
                        if (instruction.LookupCacheIndex >= 0 && dictionary.HasSharedRecordKeys)
                        {
                            // Reuse the ordinary definition-owned cache. Admit its
                            // backing before proving the current live row layout.
                            var caches = _function.GetLookupCaches(instruction.Span);
                            if (dictionary.TryReadRecordValue(field, ref caches[instruction.LookupCacheIndex], _scope,
                                instruction.Span, out var cached))
                            { Publish(instruction, cached); break; }
                        }
                        if (dictionary.HasProtocolKeys) return false;
                        Publish(instruction, ReadSubscriptValue(dictionary, field, instruction.Span, _scope));
                        break;
                    }
                    case ExecutableOpCode.Binary:
                    case ExecutableOpCode.Augmented:
                        if (TryIntegerOperation()) break;
                        goto case ExecutableOpCode.Unary;
                    case ExecutableOpCode.Unary:
                        if (!TryEvaluateBuiltinArithmetic(Code, Values, Index, _scope, out var arithmetic)) return false;
                        Publish(instruction, arithmetic);
                        break;
                    case ExecutableOpCode.CompareJumpIfFalse:
                    {
                        bool truth;
                        if (!TryIntegerComparison(instruction, out truth))
                        {
                            var first = Read(instruction.InputStart, 0);
                            var second = Read(instruction.SecondInput, 1);
                            if (!TryEvaluateScalarComparison(instruction.BinaryOperator, first, second, _scope, instruction.Span, out truth)) return false;
                        }
                        Trim(instruction.LiveExtent);
                        _scope.CheckExecution(instruction.Span);
                        if (!truth) { Index = instruction.TargetIndex; continue; }
                        break;
                    }
                    case ExecutableOpCode.JumpIfFalse:
                    {
                        var value = Read(instruction.InputStart, 0);
                        if (!CanUseNativeTruth(value)) return false;
                        _ = Take(instruction);
                        if (!IsTruthy(value, _scope, instruction.Span)) { Index = instruction.TargetIndex; continue; }
                        break;
                    }
                    case ExecutableOpCode.Jump:
                        Index = instruction.TargetIndex;
                        continue;
                    case ExecutableOpCode.AbruptJump:
                        // The ordinary frame owns pending cleanup for an abrupt
                        // exit. Transfer before retiring or changing its inputs.
                        if (Code.NativePrefixEnd != Code.Instructions.Length) return false;
                        if (instruction.DiscardIterator) Trim(LiveExtent - 1);
                        Index = instruction.TargetIndex;
                        continue;
                    case ExecutableOpCode.GetIter:
                    {
                        var source = Read(instruction.InputStart, 0);
                        if (source is PyStringOps.CasedWhitespaceIteration)
                            Publish(instruction, source);
                        else if (source is PyList or PyTuple or PyRange or PyBytes or PyString or PyDict or PySet)
                            Publish(instruction, PyIteration.Cursor.Create(source, instruction.Span, _scope));
                        else return false; // Promote before any user iteration protocol.
                        break;
                    }
                    case ExecutableOpCode.ForNext:
                    {
                        var iterator = Values[instruction.InputStart].Object.RequireNotNull();
                        var next = iterator is PyStringOps.CasedWhitespaceIteration words ? words.MoveNext()
                            : ((PyIteration.Cursor)iterator).TryMoveNext();
                        if (!next.HasValue)
                        {
                            Trim(instruction.InputStart);
                            Index = instruction.TargetIndex;
                            continue;
                        }
                        Publish(instruction, next.TakeSlotValue());
                        break;
                    }
                    case ExecutableOpCode.MakeTuple:
                    {
                        if (TryReturnUnpackedTuple(instruction)) return true;
                        var tuple = instruction.ItemCount == 0 ? PyTuple.Empty
                            : CreateSlotTuple(Values.Slice(instruction.InputStart, instruction.ItemCount), _scope, instruction.Span);
                        Trim(instruction.InputStart);
                        Publish(instruction, tuple);
                        break;
                    }
                    case ExecutableOpCode.MakeList:
                    {
                        var list = CreateSlotList(Values.Slice(instruction.InputStart, instruction.ItemCount), _scope, instruction.Span);
                        Trim(instruction.InputStart);
                        Publish(instruction, list);
                        break;
                    }
                    case ExecutableOpCode.StartFormattedString or ExecutableOpCode.AppendFormattedText
                        or ExecutableOpCode.FinishFormattedString or ExecutableOpCode.ConvertFormattedValue or ExecutableOpCode.FormatField:
                        if (!Format(instruction)) return false;
                        break;
                    case ExecutableOpCode.Dup:
                        Put(instruction.ResultSlot + 1, instruction.LiveExtent, Read(instruction.InputStart, 0));
                        break;
                    case ExecutableOpCode.PopTop:
                        _ = Read(instruction.InputStart, 0);
                        Trim(instruction.LiveExtent);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected inline opcode: {instruction.OpCode}");
                }
                Index++;
            }
            return Index >= Code.Instructions.Length;
        }

        private bool Format(in ExecutableInstruction instruction)
        {
            ExecutableFormatField? field = null;
            object? value = null;
            string? specifier = null;
            if (instruction.OpCode is ExecutableOpCode.ConvertFormattedValue or ExecutableOpCode.FormatField)
            {
                var source = instruction.OpCode == ExecutableOpCode.ConvertFormattedValue
                    ? instruction.InputStart : instruction.ResultSlot;
                if (!IsImmediateFormattedValue(Read(source, 0))) return false;
                if (instruction.OpCode == ExecutableOpCode.FormatField)
                {
                    field = (ExecutableFormatField)Code.Constants[instruction.ConstantIndex]!;
                    specifier = GetExecutableFormatSpecifier(field, field.DynamicSpecifier
                        ? TakeFormattedInput(instruction.ResultSlot + 1, instruction.ResultSlot + 1) : null);
                }
                value = TakeFormattedInput(source, instruction.ResultSlot);
            }
            else if (instruction.OpCode == ExecutableOpCode.AppendFormattedText)
                value = Code.Constants[instruction.ConstantIndex];
            var output = instruction.OpCode is ExecutableOpCode.AppendFormattedText or ExecutableOpCode.FinishFormattedString
                || field?.Append == true ? (GovernedByteBuilder)Values[instruction.InputStart].Object! : null;
            var result = ExecuteBuiltinFormattedInstruction(instruction, output, value, specifier, field, _scope);
            if (instruction.OpCode == ExecutableOpCode.FinishFormattedString) Trim(instruction.ResultSlot);
            if (result is not null)
            {
                try { Publish(instruction, result); }
                catch { if (result is ExecutableTemporaryValue owned) owned.Dispose(); throw; }
            }
            return true;
        }

        private object TakeFormattedInput(int source, int liveExtent)
        {
            var value = Read(source, 0);
            Values[source].Object = null!;
            Trim(liveExtent);
            return value;
        }

        private bool Call(in ExecutableInstruction instruction)
        {
            var call = Code.CallSites[instruction.CallSiteIndex];
            var start = instruction.InputStart;
            var target = Values[start + call.TargetOffset].Object.RequireNotNull();
            var result = default(ExecutableSlot);
            try
            {
                if (!call.IsPositionalOnly)
                {
                    if (!CallFunction(call, start, target, out result)) return false;
                }
                else if (!call.HasReceiver && ExecutionContext.IsRangeBuiltin(target))
                {
                    if (!TryInvokeNativeRange(Values.Slice(start + call.ArgumentOffset, call.ArgumentCount),
                        call.CallSpan, _scope, out var rangeResult)) return false;
                    result.Object = rangeResult;
                }
                else if (call.HasReceiver && target is ReceiverMethod method)
                {
                    var receiver = Values[start].Object!;
                    if (call.ArgumentCount == 0 && receiver is PyString text
                        && call.CasedSplit is { } recipe && ReferenceEquals(target, recipe.CasingMethod))
                    {
                        var final = Code.Instructions[Index + 2];
                        var consumer = Code.CallSites[final.CallSiteIndex];
                        _scope.EnterInterpreterFrame(recipe.CasingCallSpan);
                        try { result.Object = ExecuteCasedSplit(recipe, text, _scope,
                            consumer.ConsumedByIteration || _consumedByIteration && consumer.ReturnedDirectly); }
                        finally { _scope.LeaveInterpreterFrame(); }
                        Index += 2;
                        Publish(final, result);
                        return true;
                    }
                    if (!TryInvokeClosedReceiver(method, receiver,
                        Values.Slice(start + call.ArgumentOffset, call.ArgumentCount), call.CallSpan, _scope,
                        out var called)) return false;
                    result.Object = called;
                }
                else
                {
                    var argument = call.ArgumentCount == 1 && !call.HasReceiver
                        ? Values[start + call.ArgumentOffset].Object : null;
                    var closed = false;
                    var operation = argument is not null ? SelectNativeUnaryOperation(target, argument, out closed) : null;
                    if (operation is not null && closed)
                        result = InvokeSingleInput(argument!, operation, call.CallSpan, _scope);
                    else if (!CallFunction(call, start, target, out result)) return false;
                }
            }
            catch (Exception error) when (TryTranslateCallableException(error, call.CallSpan, out var translated))
            { throw translated; }
            // Publication and its store checkpoint remain outside callable
            // exception translation. The marker never leaves this activation.
            Publish(instruction, result);
            return true;
        }

        private bool CallFunction(ExecutableCallSite call, int start, object target, out ExecutableSlot result)
        {
            result = ExecutableSlot.FromObject(PyNone.Instance);
            if (_scope.State.CurrentRecursionDepth >= NativeCallDepth) return false;
            var function = target as PyExecutableFunction;
            object? receiver = call.HasReceiver && Values[start].Object is not PyNone ? Values[start].Object : null;
            if (target is PyBoundMethod { Function: PyExecutableFunction boundFunction } bound)
            {
                function = boundFunction;
                receiver = bound.Receiver;
            }
            if (function is null || !function.CanStartInline) return false;
            var borrow = call.IsPositionalOnly && function.SupportsDirectPositional
                && function.CanBorrowPositionalArguments(call.ArgumentCount, receiver);
            if (!borrow && !function.CanBindNativeArguments) return false;
            _scope.EnterInterpreterFrame(call.CallSpan);
            var entered = true;
            try
            {
                var arguments = Values.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                function.StartNativeCall(call, arguments, receiver, _scope, call.CallSpan,
                    call.ConsumedByIteration || _consumedByIteration && call.ReturnedDirectly,
                    borrow, default, out _, out var childValue, out var child);
                result = childValue;
                if (child is not null)
                {
                    // No operation is running in the child. Keep its exact
                    // PC and ownership while this caller acquires a frame.
                    _preparedChild = child;
                    _preparedChildOwner = function.DefinitionContext.MemoryGovernor;
                    entered = false;
                    return false;
                }
            }
            finally { if (entered) _scope.LeaveInterpreterFrame(); }
            return true;
        }

        private bool TryReturnUnpackedTuple(in ExecutableInstruction instruction)
        {
            if (Code.NativePrefixEnd != Code.Instructions.Length || _unpackedReturnCount == 0 || instruction.ItemCount != _unpackedReturnCount
                || instruction.HasLocalResult || Index + 1 >= Code.Instructions.Length) return false;
            ref readonly var next = ref Code.Instructions[Index + 1];
            if (next.OpCode != ExecutableOpCode.Return || next.InputStart != instruction.ResultSlot) return false;
            var items = Values.Slice(instruction.InputStart, instruction.ItemCount);
            // Keep eager tuple admission before any caller target can change.
            // Its conservative funding survives until the execution owner ends;
            // only the unobservable tuple shell/backing allocation disappears.
            var bytes = SlotTupleBytes(items, _scope, instruction.Span);
            _scope.MemoryGovernor.Reserve(bytes, instruction.Span);
            try
            {
                _scope.CheckExecution(instruction.Span);
                _scope.MemoryGovernor.Commit(bytes);
            }
            catch { _scope.MemoryGovernor.ReleaseReserved(bytes); throw; }
            // The return wrapper copies these already-admitted operands before
            // native cleanup. The hot execution state retains no caller borrow.
            _unpackedTupleStart = instruction.InputStart;
            return true;
        }

        private bool TryCell(int slot, out ExecutableCell cell)
        {
            cell = (uint)slot < (uint)_cells.Count ? _cells[slot] : null!;
            return cell is not null;
        }

        private ExecutableSlot ReadReturnSlot(int source)
        {
            if (source < 0) return ExecutableSlot.FromObject(RuntimeValue(Code.Constants[~source]));
            var value = Values[source];
            if (value.IsUnassigned) _ = Read(source, 0);
            return value;
        }

        private object Read(int source, int input)
            => ReadExecutableOperand(Code, Values, source, Index, input, _scope);

        private object Take(in ExecutableInstruction instruction)
        {
            var value = Read(instruction.InputStart, 0);
            if (instruction.InputStart >= Code.FirstTemporarySlot) Values[instruction.InputStart].Object = null!;
            Trim(instruction.LiveExtent);
            return value;
        }

        private void Publish(in ExecutableInstruction instruction, object value)
            => Publish(instruction, ExecutableSlot.FromObject(value));

        private void Publish(in ExecutableInstruction instruction, ExecutableSlot value)
        {
            if (!instruction.HasLocalResult) { Put(instruction.ResultSlot, instruction.LiveExtent, value); return; }
            if (instruction.LiveExtent < LiveExtent) Trim(instruction.LiveExtent);
            Values[instruction.ResultSlot] = default;
            LiveExtent = instruction.ResultSlot;
            ref readonly var store = ref Code.Instructions[Index + 1];
            _scope.CheckExecution(store.Span);
            Values[store.LocalSlot] = value;
            Index++;
        }

        private void Put(int slot, int liveExtent, object value)
            => Put(slot, liveExtent, ExecutableSlot.FromObject(value));

        private void Put(int slot, int liveExtent, ExecutableSlot value)
        {
            if (liveExtent < LiveExtent) Trim(liveExtent);
            Values[slot] = value;
            LiveExtent = liveExtent;
        }

        private void Trim(int liveExtent)
        {
            for (var i = Math.Min(LiveExtent - 1, Code.LastOwnedTemporarySlot); i >= liveExtent; i--)
                if (Values[i].Reference is ExecutableTemporaryValue owned)
                { Values[i].Object = null!; owned.Dispose(); }
            // Dead references stay in the private span or already-admitted owned
            // backing until this synchronous prefix ends. Their owners remain
            // funded. Clear the dead tail once before transfer.
            LiveExtent = liveExtent;
        }

        internal void PrepareTransfer()
        {
            // Final managed slots transfer directly; no borrowed side span remains.
            Values[LiveExtent..].Clear();
        }
        internal void TransferOwnership(ExecutionContext frame)
        {
            if (_preparedChild is { } child)
            {
                var call = Code.CallSites[Code.Instructions[Index].CallSiteIndex];
                frame.AdoptPreparedNativeChild(child, call, Code.Instructions[Index].InputStart, _preparedChildOwner!);
                _preparedChild = null;
                _preparedChildOwner = null;
            }
            _transferred = true;
        }
        internal void Dispose()
        {
            if (_transferred) return;
            try
            {
                if (_preparedChild is { } child)
                {
                    _preparedChild = null;
                    try { ExecutionContext.AbortPreparedNativeCall(child, _preparedChildOwner!); }
                    finally { _scope.LeaveInterpreterFrame(); }
                }
            }
            finally { DisposeValues(); }
        }

        private void DisposeValues()
        {
            // A directly returned fused call can produce a private cursor even
            // without a GetIter slot in this function. Cancellation before Return
            // must retire that loan too, before the caller can own the result.
            for (var i = LiveExtent - 1; i >= Code.FirstTemporarySlot; i--)
                if (Values[i].Reference is ExecutableTemporaryValue owned)
                { Values[i].Object = null!; owned.Dispose(); }
            LiveExtent = Code.FirstTemporarySlot;
        }
    }

    internal static ExecutionContext CreateNativeContinuation(PyExecutableFunction function,
        in BoundFunctionArguments bound, LythonSourceSpan span, int index, int liveExtent, bool consumedByIteration,
        LythonRuntimeException? failure)
    {
        var frame = function.EnterInvocationFrame(bound, span, enterFunctionCall: false);
        InitializeExecutableFrame(function.CodeObject, frame, bound, function.ClosureCells,
            argumentsAreLocals: true, lookupOwner: function, returnConsumedByIteration: consumedByIteration);
        frame.RestoreNativePosition(index, liveExtent, failure);
        return frame;
    }

    internal sealed partial class ExecutionContext
    {
        internal void RestoreNativePosition(int index, int liveExtent, LythonRuntimeException? failure)
        {
            _execution.InstructionIndex = index;
            _execution.Stack.RestoreNativeExtent(liveExtent);
            _injectedException = failure;
        }
    }
}
