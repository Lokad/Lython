using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal const int NativeValueCapacity = 16;

    // This view cannot survive a suspension. The final executable slots and PC
    // are also the ordinary frame's representation; transfer never replays work.
    internal ref struct NativeExecution(PyExecutableFunction function, Span<object> values, bool consumedByIteration)
    {
        private readonly PyExecutableFunction _function = function;
        internal readonly ExecutableCodeObject Code = function.CodeObject;
        internal readonly Span<object> Values = values;
        private readonly IReadOnlyList<ExecutableCell> _cells = function.ClosureCells;
        private readonly ExecutionContext _scope = function.DefinitionContext;
        private readonly bool _consumedByIteration = consumedByIteration;
        internal int Index = function.CodeObject.EntryIndex;
        internal int LiveExtent = function.CodeObject.FirstTemporarySlot;
        private bool _transferred;
        private ExecutionContext? _preparedChild;
        private MemoryGovernor? _preparedChildOwner;

        internal bool Run(LythonSourceSpan entrySpan, out object result)
        {
            _scope.EnterInterpreterFrame(entrySpan);
            var previous = _scope.State.CurrentException;
            try { return RunCore(out result); }
            catch (LythonRuntimeException error)
            {
                if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                    error.PythonContext ??= previous;
                throw;
            }
            finally
            {
                _scope.State.SetCurrentException(previous);
                _scope.LeaveInterpreterFrame();
            }
        }

        private bool RunCore(out object result)
        {
            result = PyNone.Instance;
            while (Index < Code.Instructions.Length)
            {
                ref readonly var instruction = ref Code.Instructions[Index];
                _scope.State.CheckExecution(instruction.Span);
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Complete:
                    case ExecutableOpCode.ReturnNone:
                        Trim(Code.FirstTemporarySlot);
                        return true;
                    case ExecutableOpCode.Return:
                        result = Read(instruction.InputStart, 0);
                        if (instruction.InputStart >= Code.FirstTemporarySlot) Values[instruction.InputStart] = null!;
                        Trim(Code.FirstTemporarySlot);
                        return true;
                    case ExecutableOpCode.LoadConst:
                        Publish(instruction, RuntimeValue(Code.Constants[instruction.ConstantIndex]));
                        break;
                    case ExecutableOpCode.LoadLocal:
                        Publish(instruction, ReadLocal(instruction.LocalSlot, instruction.Span));
                        break;
                    case ExecutableOpCode.LoadGlobal:
                        Publish(instruction, ResolveExecutableGlobal(Code.Names[instruction.NameIndex], instruction.Span, _scope));
                        break;
                    case ExecutableOpCode.LoadClosure:
                        if (!TryCell(instruction.ClosureSlot, out var readCell) || readCell.Value is not { } captured) return false;
                        Publish(instruction, captured);
                        break;
                    case ExecutableOpCode.StoreClosure:
                        if (!TryCell(instruction.ClosureSlot, out var writeCell) || !writeCell.CanStoreThroughOwner) return false;
                        var stored = Take(instruction);
                        writeCell.Value = stored;
                        break;
                    case ExecutableOpCode.StoreLocal:
                        Values[instruction.LocalSlot] = Take(instruction);
                        break;
                    case ExecutableOpCode.LoadCallMember:
                    {
                        var receiver = Read(instruction.InputStart, 0);
                        var name = Code.Names[instruction.NameIndex];
                        var method = receiver is PyString ? StringMembers.GetMethod(name)
                            : receiver is PyDict ? DictMembers.GetMethod(name) : null;
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
                    case ExecutableOpCode.Unary:
                    {
                        var value = Read(instruction.InputStart, 0);
                        if (instruction.UnaryOperator == ExecutableUnaryOperator.Not ? !CanUseNativeTruth(value)
                            : value is not (System.Numerics.BigInteger or double or bool)) return false;
                        Publish(instruction, EvaluateExecutableUnary(instruction.UnaryOperator, value, _scope, instruction.Span));
                        break;
                    }
                    case ExecutableOpCode.Binary:
                    case ExecutableOpCode.Augmented:
                    {
                        var first = Read(instruction.InputStart, 0);
                        var second = Read(instruction.SecondInput, 1);
                        var augmented = instruction.OpCode == ExecutableOpCode.Augmented;
                        if (!augmented && TryEvaluateScalarComparison(instruction.BinaryOperator, first, second,
                            _scope, instruction.Span, out var scalarTruth))
                        {
                            Publish(instruction, RuntimeValue(scalarTruth));
                            break;
                        }
                        if (!CanUseNativeBinary(augmented ? AugmentedBinaryOperator(instruction.AugmentedOperator)
                            : instruction.BinaryOperator, first, second)) return false;
                        if (augmented && (first is not (System.Numerics.BigInteger or double or bool)
                            || second is not (System.Numerics.BigInteger or double or bool))) return false;
                        Publish(instruction, augmented
                            ? EvaluateAugmentedBuiltin(first, second, instruction.AugmentedOperator, _scope, instruction.Span)
                            : EvaluateBinaryOperator(instruction.BinaryOperator, first, second, _scope, instruction.Span));
                        break;
                    }
                    case ExecutableOpCode.CompareJumpIfFalse:
                    {
                        var first = Read(instruction.InputStart, 0);
                        var second = Read(instruction.SecondInput, 1);
                        if (!TryEvaluateScalarComparison(instruction.BinaryOperator, first, second, _scope, instruction.Span, out var truth)) return false;
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
                    case ExecutableOpCode.MakeTuple:
                    {
                        var tuple = instruction.ItemCount == 0 ? PyTuple.Empty
                            : new PyTuple(Values.Slice(instruction.InputStart, instruction.ItemCount),
                                _scope.MemoryGovernor, instruction.Span, _scope);
                        Trim(instruction.InputStart);
                        Publish(instruction, tuple);
                        break;
                    }
                    case ExecutableOpCode.MakeList:
                    {
                        var bytes = PyList.EstimateCapacityBytes(instruction.ItemCount);
                        using var storage = _scope.MemoryGovernor.ReserveTemporary(bytes, instruction.Span);
                        _scope.CheckExecution(instruction.Span);
                        var items = Values.Slice(instruction.InputStart, instruction.ItemCount).ToArray();
                        Trim(instruction.InputStart);
                        storage.Commit(bytes);
                        Publish(instruction, PyList.FromTransferredArray(items, _scope.MemoryGovernor, instruction.Span));
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
            return true;
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
                || field?.Append == true ? (GovernedByteBuilder)Values[instruction.InputStart] : null;
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
            var value = Values[source];
            Values[source] = null!;
            Trim(liveExtent);
            return value;
        }

        private bool Call(in ExecutableInstruction instruction)
        {
            var call = Code.CallSites[instruction.CallSiteIndex];
            var start = instruction.InputStart;
            var target = Values[start + call.TargetOffset];
            object result;
            try
            {
                if (!call.IsPositionalOnly)
                {
                    if (!CallFunction(call, start, target, out result)) return false;
                }
                else if (call.HasReceiver && Values[start] is PyDict dictionary)
                {
                    if (call.ArgumentCount == 0 && target is ReceiverMethod method && method.AcceptsPositional(0))
                        result = InvokeReceiverMethod(method, dictionary, call.CallSpan, _scope);
                    else if (call.ArgumentCount is 1 or 2 && ReferenceEquals(target, ReceiverMethods.DictionaryGet)
                        && dictionary.CanReadKeySynchronously(Values[start + call.ArgumentOffset]))
                        result = InvokeDictionaryGet(dictionary, Values[start + call.ArgumentOffset],
                            call.ArgumentCount == 2 ? Values[start + call.ArgumentOffset + 1] : PyNone.Instance, call.CallSpan, _scope);
                    else return false;
                }
                else if (call.HasReceiver && Values[start] is PyString text)
                {
                    if (call.ArgumentCount == 0 && target is ReceiverMethod method && method.AcceptsPositional(0))
                    {
                        if (call.CasedSplit is { } recipe && ReferenceEquals(target, recipe.CasingMethod))
                        {
                            var final = Code.Instructions[Index + 2];
                            var consumer = Code.CallSites[final.CallSiteIndex];
                            _scope.EnterInterpreterFrame(recipe.CasingCallSpan);
                            try { result = ExecuteCasedSplit(recipe, text, _scope,
                                consumer.ConsumedByIteration || _consumedByIteration && consumer.ReturnedDirectly); }
                            finally { _scope.LeaveInterpreterFrame(); }
                            Index += 2;
                            Publish(final, result);
                            return true;
                        }
                        result = InvokeReceiverMethod(method, text, call.CallSpan, _scope);
                    }
                    else
                    {
                        var args = Values.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                        var first = args.Length > 0 ? args[0] : null;
                        var second = args.Length > 1 ? args[1] : null;
                        var third = args.Length > 2 ? args[2] : null;
                        if (SelectNativeStringOperation(target, args.Length) is not { } operation
                            || !CanUseNativeStringCall(operation, first, second, third)) return false;
                        result = InvokeNativeStringOperation(operation, text, first, second, third, call.CallSpan, _scope);
                    }
                }
                else
                {
                    var argument = call.ArgumentCount == 1 && !call.HasReceiver
                        ? Values[start + call.ArgumentOffset] : null;
                    var closed = false;
                    var operation = argument is not null ? SelectNativeUnaryOperation(target, argument, out closed) : null;
                    if (operation is not null && closed)
                        result = InvokeSingleInput(argument!, operation, call.CallSpan, _scope);
                    else if (!CallFunction(call, start, target, out result)) return false;
                }
            }
            catch (Exception error) when (TryTranslateCallableException(error, call.CallSpan, out var translated))
            { throw translated; }
            Publish(instruction, result);
            return true;
        }

        private bool CallFunction(ExecutableCallSite call, int start, object target, out object result)
        {
            result = PyNone.Instance;
            var function = target as PyExecutableFunction;
            object? receiver = call.HasReceiver && Values[start] is not PyNone ? Values[start] : null;
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
                if (!function.TryStartNativeCall(call, arguments, receiver, _scope, call.CallSpan,
                    call.ConsumedByIteration || _consumedByIteration && call.ReturnedDirectly,
                    borrow, out result, out var child)) return false;
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

        private bool TryCell(int slot, out ExecutableCell cell)
        {
            cell = (uint)slot < (uint)_cells.Count ? _cells[slot] : null!;
            return cell is not null;
        }

        private object Read(int source, int input)
        {
            if (source < 0) return RuntimeValue(Code.Constants[~source]);
            var value = Values[source];
            if (value is null && source < Code.FirstTemporarySlot)
                throw RuntimeErrors.UnboundLocalVariable(Code.LocalNames[source], Code.OperandReadSpan(Index, input));
            return value.RequireNotNull();
        }

        private object ReadLocal(int slot, LythonSourceSpan span) => Values[slot]
            ?? throw RuntimeErrors.UnboundLocalVariable(Code.LocalNames[slot], span);

        private object Take(in ExecutableInstruction instruction)
        {
            var value = Read(instruction.InputStart, 0);
            if (instruction.InputStart >= Code.FirstTemporarySlot) Values[instruction.InputStart] = null!;
            Trim(instruction.LiveExtent);
            return value;
        }

        private void Publish(in ExecutableInstruction instruction, object value)
        {
            if (!instruction.HasLocalResult) { Put(instruction.ResultSlot, instruction.LiveExtent, value); return; }
            if (instruction.LiveExtent < LiveExtent) Trim(instruction.LiveExtent);
            Values[instruction.ResultSlot] = null!;
            LiveExtent = instruction.ResultSlot;
            ref readonly var store = ref Code.Instructions[Index + 1];
            _scope.CheckExecution(store.Span);
            Values[store.LocalSlot] = value;
            Index++;
        }

        private void Put(int slot, int liveExtent, object value)
        {
            if (liveExtent < LiveExtent) Trim(liveExtent);
            Values[slot] = value;
            LiveExtent = liveExtent;
        }

        private void Trim(int liveExtent)
        {
            for (var i = Math.Min(LiveExtent - 1, Code.LastOwnedTemporarySlot); i >= liveExtent; i--)
                if (Values[i] is ExecutableTemporaryValue owned)
                { Values[i] = null!; owned.Dispose(); }
            // At most sixteen private references survive until this synchronous
            // prefix ends. Their execution owners remain funded. Clear the dead
            // tail once before transfer, rather than after every operation.
            LiveExtent = liveExtent;
        }

        internal void PrepareTransfer() => Values[LiveExtent..].Clear();
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
                if (Values[i] is ExecutableTemporaryValue owned)
                { Values[i] = null!; owned.Dispose(); }
            LiveExtent = Code.FirstTemporarySlot;
        }
    }

    internal static ExecutionContext CreateNativeContinuation(PyExecutableFunction function,
        in BoundCallArguments bound, LythonSourceSpan span, int index, int liveExtent, bool consumedByIteration)
    {
        var frame = function.EnterInvocationFrame(bound, span, enterFunctionCall: false);
        InitializeExecutableFrame(function.CodeObject, frame, bound, function.ClosureCells,
            argumentsAreLocals: true, lookupOwner: function, returnConsumedByIteration: consumedByIteration);
        frame.RestoreNativePosition(index, liveExtent);
        return frame;
    }

    internal sealed partial class ExecutionContext
    {
        internal void RestoreNativePosition(int index, int liveExtent)
        {
            _execution.InstructionIndex = index;
            _execution.Stack.RestoreNativeExtent(liveExtent);
        }
    }
}
