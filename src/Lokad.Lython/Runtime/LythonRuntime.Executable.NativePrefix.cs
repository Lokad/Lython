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
        private readonly ExecutableCaptures _cells = function.Captures;
        private readonly ExecutionContext _scope = function.DefinitionContext;
        private readonly bool _consumedByIteration = consumedByIteration;
        private readonly int _unpackedReturnCount = unpackedReturnCount;
        private int _unpackedTupleStart = -1;
        internal bool ReturnedUnpacked => _unpackedTupleStart >= 0;
        internal int Index = function.CodeObject.EntryIndex;
        internal int LiveExtent = function.CodeObject.FirstTemporarySlot;
        private bool _entered;
        private bool _hasDeferredDefinitions;
        internal bool OwnershipTransferred { get; private set; }
        private ExecutionContext? _preparedChild;
        private MemoryGovernor? _preparedChildOwner;
        internal LythonRuntimeException? Failure { get; private set; }

        internal bool Run(LythonSourceSpan entrySpan, out ExecutableSlot result, Span<ExecutableSlot> unpackedReturn = default)
        {
            // EnterFunctionCall also validates the physical stack. Enter at
            // this managed body boundary and keep the admission with the work
            // until it exits or transfers to the continuation.
            _scope.EnterFunctionCall(entrySpan);
            _entered = true;
            var previous = _scope.State.CurrentException;
            try
            {
                var completed = RunCore(out result);
                if (!completed && Failure is { } returned)
                    RecordFailure(returned, previous, entrySpan);
                if (completed && ReturnedUnpacked)
                {
                    WriteUnpackOperands(Values.Slice(_unpackedTupleStart, _unpackedReturnCount), unpackedReturn);
                    Trim(Code.FirstTemporarySlot);
                    _scope.CheckExecution(Code.Instructions[Index + 1].Span);
                }
                return completed;
            }
            catch (LythonRuntimeException error)
            {
                if (!RecordFailure(error, previous, entrySpan)) throw;
                result = ExecutableSlot.FromObject(PyNone.Instance);
                return false;
            }
            finally
            {
                _scope.State.SetCurrentException(previous);
            }
        }

        private bool RecordFailure(LythonRuntimeException error, PyException? previous, LythonSourceSpan entrySpan)
        {
            if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                error.PythonContext ??= previous;
            if (Code.ExceptionRegions.Count == 0) return false;
            // Reuse the existing transferred PC and pending exception. Refusal
            // and cancellation still stop the command before another guest step.
            _scope.CheckExecution(entrySpan);
            Failure = error;
            return true;
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
                        result = ReadSlotForTransfer(instruction.InputStart);
                        if (instruction.InputStart >= Code.FirstTemporarySlot) Values[instruction.InputStart].Object = null!;
                        Trim(Code.FirstTemporarySlot);
                        return true;
                    case ExecutableOpCode.LoadConst:
                        Publish(instruction, RuntimeValue(Code.Constants[instruction.ConstantIndex]));
                        break;
                    case ExecutableOpCode.IntegerCallUpdate:
                        if (instruction.IntegerUpdateCallIndex + 2 < Code.NativePrefixEnd &&
                            _scope.State.CurrentRecursionDepth < NativeCallDepth &&
                            TryEvaluateIntegerCallUpdate(Code, Values, ref Index, LiveExtent, _scope, out var updated))
                        {
                            Publish(Code.Instructions[Index], updated);
                            break;
                        }
                        if (instruction.IntegerUpdateChecksLocal) goto case ExecutableOpCode.CheckLocal;
                        goto case ExecutableOpCode.LoadLocal;
                    case ExecutableOpCode.PreparedTupleReturn:
                        if (Code.NativePrefixEnd == Code.Instructions.Length &&
                            TryPrepareFixedTupleReturn(Code, Values, ref Index, LiveExtent, _unpackedReturnCount, _scope))
                        {
                            _unpackedTupleStart = LiveExtent;
                            LiveExtent += _unpackedReturnCount;
                            return true;
                        }
                        goto case ExecutableOpCode.LoadLocal;
                    case ExecutableOpCode.LoadDictionaryGetKey:
                    case ExecutableOpCode.LoadLocal:
                        Publish(instruction, Read(instruction.LocalSlot, 0));
                        break;
                    case ExecutableOpCode.CheckLocal:
                        if (Values[instruction.LocalSlot].IsUnassigned)
                            throw RuntimeErrors.UnboundLocalVariable(Code.LocalNames[instruction.LocalSlot], instruction.Span);
                        break;
                    case ExecutableOpCode.LoadGlobalForArithmeticCall:
                        if (Index + 2 < Code.NativePrefixEnd && _scope.State.CurrentRecursionDepth < NativeCallDepth &&
                            TryCallWithArithmeticArgument(Code, Values, ref Index, LiveExtent, _scope, out var arithmeticCall))
                        {
                            Publish(Code.Instructions[Index], arithmeticCall);
                            break;
                        }
                        goto case ExecutableOpCode.LoadGlobal;
                    case ExecutableOpCode.LoadGlobal:
                        Publish(instruction, ResolveExecutableGlobal(Code, instruction.NameIndex, instruction.Span, _scope));
                        break;
                    case ExecutableOpCode.LoadClosure:
                        if (TryExecuteClosureArithmetic(Code, _cells, Index, LiveExtent, _scope))
                        { Index += 2; break; }
                        if (!_cells.TryRead(instruction.ClosureSlot, out var captured) || captured is null) return false;
                        Publish(instruction, captured);
                        break;
                    case ExecutableOpCode.StoreClosure:
                        if (_cells.GetCell(instruction.ClosureSlot) is not { CanStoreDirectly: true } writeCell) return false;
                        var stored = ReadSlotForTransfer(instruction.InputStart);
                        Values[instruction.InputStart] = default;
                        Trim(instruction.LiveExtent);
                        writeCell.StoreSlot(stored);
                        break;
                    case ExecutableOpCode.StoreLocal:
                        StoreLocal(instruction);
                        break;
                    case ExecutableOpCode.LoadCasedSplit:
                        if (TryExecuteCasedSplitMember(instruction)) break;
                        goto case ExecutableOpCode.LoadCallMember;
                    case ExecutableOpCode.LoadCallMember:
                    {
                        var receiver = Read(instruction.InputStart, 0);
                        var name = Code.Names[instruction.NameIndex];
                        object? target = receiver is PyInstance instance &&
                            PyAttributeLookup.TryResolveInstanceFunctionForCall(instance, name, out var function)
                            ? function : ResolveBuiltinReceiverMethod(receiver, name);
                        if (target is null) return false;
                        Put(instruction.ResultSlot, instruction.ResultSlot + 1, receiver);
                        Put(instruction.ResultSlot + 1, instruction.LiveExtent, target);
                        break;
                    }
                    case ExecutableOpCode.Call:
                        if (_hasDeferredDefinitions) MaterializeDeferredDefinitions();
                        if (!Call(instruction)) return false;
                        break;
                    case ExecutableOpCode.StartFunctionDefaults:
                        System.Diagnostics.Debug.Assert(LiveExtent == instruction.ResultSlot);
                        StartExecutableDefaults((ExecutableCreateFunction)Code.Constants[instruction.ConstantIndex]!,
                            Values.Slice(instruction.ResultSlot, instruction.LiveExtent - instruction.ResultSlot),
                            instruction.StartsKeywordDefaultsOnly, _scope, instruction.Span);
                        LiveExtent = instruction.LiveExtent;
                        break;
                    case ExecutableOpCode.StoreFunctionDefault:
                        var defaultValue = Values[instruction.ResultSlot];
                        if (defaultValue.IsUnassigned) _ = defaultValue.Object.RequireNotNull();
                        Values[instruction.ResultSlot] = default;
                        Trim(instruction.LiveExtent);
                        StoreDefaultResult(Values, instruction, defaultValue, _scope.State);
                        break;
                    case ExecutableOpCode.ApplyDeferredDefinition:
                        if (TryDeferDefinition(instruction)) break;
                        goto case ExecutableOpCode.ApplyOperation;
                    case ExecutableOpCode.ApplyOperation:
                        if (!ApplyDefinition(instruction)) return false;
                        break;
                    case ExecutableOpCode.Subscript:
                    {
                        var target = Read(instruction.InputStart, 0);
                        if (TryReadMachineOperand(Code, Values, instruction.SecondInput, _scope, out var index)
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
                    case ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast:
                        if (!StoreSubscript(instruction)) return false;
                        break;
                    case ExecutableOpCode.Binary:
                    case ExecutableOpCode.Augmented:
                        if (TryIntegerOperation()) break;
                        goto case ExecutableOpCode.Unary;
                    case ExecutableOpCode.Unary:
                        if (!TryEvaluateBuiltinArithmetic(Code, Values, Index, _scope, out var arithmetic, _cells)) return false;
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
                        else if (source is PyRange range)
                            Publish(instruction, range.GetEnumerator());
                        else if (source is PyList or PyTuple or PyBytes or PyString or PyDict or PySet)
                            Publish(instruction, PyIteration.Cursor.Create(source, instruction.Span, _scope));
                        else return false; // Promote before any user iteration protocol.
                        break;
                    }
                    case ExecutableOpCode.ForNext or ExecutableOpCode.ForNextUnpack:
                    {
                        var iterator = Values[instruction.InputStart].Object.RequireNotNull();
                        var next = iterator switch
                        {
                            PyStringOps.CasedWhitespaceIteration words => words.MoveNext(),
                            PyRange.Enumerator range => range.TryMoveNext(),
                            _ => ((PyIteration.Cursor)iterator).TryMoveNext(),
                        };
                        if (!next.HasValue)
                        {
                            Trim(instruction.InputStart);
                            Index = instruction.TargetIndex;
                            continue;
                        }
                        Publish(instruction, next.TakeSlotValue());
                        break;
                    }
                    case ExecutableOpCode.ReverseAssignmentValues:
                        ReverseAssignmentValues(Values.Slice(instruction.InputStart, instruction.AssignmentValueCount), _scope.State, instruction.Span);
                        break;
                    case ExecutableOpCode.MakeTuple:
                    {
                        if (TryReturnUnpackedTuple(instruction)) return true;
                        var tuple = instruction.ItemCount == 0 ? PyTuple.Empty
                            : PyTuple.FromSlots(Values.Slice(instruction.InputStart, instruction.ItemCount), _scope, instruction.Span);
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
                    case ExecutableOpCode.MakeDict or ExecutableOpCode.MakeSet or ExecutableOpCode.MakeRecord:
                        if (!ConstructContainer(instruction)) return false;
                        break;
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
                        // Preserve the current PC and values. The ordinary frame
                        // owns protocols, handlers and actual suspension.
                        return false;
                }
                Index++;
            }
            return Index >= Code.Instructions.Length;
        }

        // Keep new container construction and unpublished-result cleanup out
        // of the ordinary arithmetic/call loop. Unknown keys transfer before
        // traversing composites or invoking any Python callback.
        private bool ConstructContainer(in ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.MakeRecord)
            {
                var keys = (PyDict.RecordKeys)Code.Constants[instruction.ConstantIndex]!;
                var record = keys.CreateDisplay(Values.Slice(instruction.InputStart, keys.Count), keyAndValue: false,
                    _scope, instruction.Span);
                try { Trim(instruction.InputStart); Publish(instruction, record); return true; }
                catch { record.DiscardUnpublishedStorage(); throw; }
            }
            var dictionary = instruction.OpCode == ExecutableOpCode.MakeDict;
            var inputs = Values.Slice(instruction.InputStart,
                dictionary ? checked(2 * instruction.PairCount) : instruction.ItemCount);
            if (!CanConstructClosedSlotKeys(inputs, dictionary ? 2 : 1, _scope, instruction.Span)) return false;
            object value;
            if (!dictionary) value = CreateSlotSet(inputs, _scope, instruction.Span);
            else if (instruction.LookupCacheIndex >= 0 && inputs[0].Reference is PyString)
                value = CreateSlotDictionary(inputs, ref _function.GetLookupCaches(instruction.Span)[instruction.LookupCacheIndex],
                    _scope, instruction.Span);
            else value = CreateSlotDictionary(inputs, _scope, instruction.Span);
            try
            {
                Trim(instruction.InputStart);
                Publish(instruction, value);
                return true;
            }
            catch
            {
                if (value is PyDict map) map.DiscardUnpublishedStorage();
                else ((PySet)value).ReleaseUnpublishedStorage();
                throw;
            }
        }

        private bool Format(in ExecutableInstruction instruction)
        {
            ExecutableFormatField? field = null;
            object? value = null;
            PyString? specifier = null;
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

        private bool StoreSubscript(in ExecutableInstruction instruction)
        {
            // Resolve the physical inputs in their existing evaluation order.
            // Growth and protocol keys transfer before the dictionary is changed.
            var first = Read(instruction.InputStart, 0);
            var second = Read(instruction.SecondInput, 1);
            var third = Read(instruction.ThirdInput, 2);
            var receiver = instruction.ValueLast ? first : second;
            if (receiver is not PyDict dictionary) return false;
            var key = instruction.ValueLast ? second : third;
            var value = instruction.ValueLast ? third : first;
            var span = (LythonSourceSpan)Code.Constants[instruction.TargetSpanIndex]!;
            if (!dictionary.TryWriteKeyWithoutAdmission(key, value, _scope, span)) return false;
            try { _scope.CheckExecution(span); }
            finally { Trim(instruction.LiveExtent); }
            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool TryExecuteCasedSplitMember(in ExecutableInstruction instruction)
        {
            if (Read(instruction.InputStart, 0) is not PyString text) return false;
            var split = ExecuteCasedSplit(Code, Index, text, _scope, _consumedByIteration);
            Index += 3;
            Publish(Code.Instructions[Index], ExecutableSlot.FromObject(split));
            return true;
        }

        private bool Call(in ExecutableInstruction instruction)
        {
            var call = Code.CallSites[instruction.CallSiteIndex];
            var start = instruction.ResultSlot;
            object target;
            object? forwardedArgument = null;
            var borrowedArgument = -1;
            if (instruction.HasForwardedCallOperands && LiveExtent == start)
            {
                target = Read(instruction.InputStart, 0);
                var source = instruction.SecondInput;
                if (source >= 0 && Values[source].TryGetShortUtf8(out var packed)
                    && TryInvokeShortUtf8Float(packed, target, call.CallSpan, _scope, out var converted))
                {
                    Publish(instruction, converted);
                    return true;
                }
                if (CanBorrowForwardedInteger(target, Values, source)) borrowedArgument = source;
                else forwardedArgument = Read(source, 1);
            }
            else target = Values[start + call.TargetOffset].Object.RequireNotNull();
            var argument = ExecutableSlot.FromObject(forwardedArgument);
            var arguments = borrowedArgument >= 0 ? Values.Slice(borrowedArgument, 1)
                : forwardedArgument is not null ? new Span<ExecutableSlot>(ref argument)
                : Values.Slice(start + call.ArgumentOffset, call.ArgumentCount);
            var result = default(ExecutableSlot);
            var suppliedReturn = false;
            try
            {
                if (!call.IsPositionalOnly)
                {
                    if (!CallFunction(call, start, target, arguments, false, out result, out suppliedReturn)) goto Declined;
                }
                else if (!call.HasReceiver && ExecutionContext.IsRangeBuiltin(target))
                {
                    if (!TryInvokeNativeRange(arguments, call.CallSpan, _scope, out var rangeResult)) goto Declined;
                    result.Object = rangeResult;
                }
                else if (call.HasReceiver && target is ReceiverMethod method)
                {
                    var receiver = Values[start].Object!;
                    if (call.SplitPrefix is ExecutableTrimmedSplit trimmed &&
                        Code.Instructions[Index + 3].InputStart == start &&
                        TryReadAsciiTrim(call, target, Values, start, out var input, out var trim))
                    {
                        var split = Code.CallSites[Code.Instructions[Index + 3].CallSiteIndex];
                        result.Object = ExecuteTrimmedSplit(trimmed, input, trim, call.CallSpan, split.CallSpan, default, _scope)!;
                        Index += 3;
                    }
                    else if (!TryConsumePrivateStringResult(method, Code, Values, ref Index, call, start, _scope, out result))
                    {
                        if (!TryInvokeClosedReceiver(method, receiver, arguments, call.CallSpan, _scope, out var called)) return false;
                        result.Object = called;
                    }
                }
                else
                {
                    var invoked = borrowedArgument < 0 && (forwardedArgument is null
                        ? TryInvokeBuiltinFromSlots(call, target, Values, start, _scope, allowProtocol: false, out result)
                        : target is BuiltinCallable builtin && TryInvokeUnaryBuiltin(builtin, forwardedArgument,
                            call.CallSpan, _scope, allowProtocol: false, out result));
                    if (invoked)
                    {
                        if (result.Reference is LythonRuntimeException failure)
                        {
                            if (Code.ExceptionRegions.Count == 0) throw failure;
                            Failure = failure;
                            return false;
                        }
                    }
                    else if (!CallFunction(call, start, target, arguments, borrowedArgument >= 0, out result, out suppliedReturn)) goto Declined;
                }
            }
            catch (Exception error) when (TryTranslateCallableException(error, call.CallSpan, out var translated))
            { throw translated; }
            // Publication and its store checkpoint remain outside callable
            // exception translation. The marker never leaves this activation.
            if (call.UnpackedReturnCount != 0 && Code.Instructions[Index].OpCode == ExecutableOpCode.UnpackValues)
            {
                _scope.CheckExecution(Code.Instructions[Index].Span);
                Trim(start + call.UnpackedReturnCount);
            }
            else
            {
                ref readonly var producer = ref Code.Instructions[Index];
                var next = Index + 1;
                if (suppliedReturn && !producer.HasResultConsumer && next < Code.NativePrefixEnd &&
                    TryCombineCallAugmentation(Code, Values, producer, next, result, _scope.State, out var updated))
                {
                    ref readonly var update = ref Code.Instructions[next];
                    if (Code.LastOwnedTemporarySlot < update.ResultSlot && (!update.HasResultConsumer ||
                        next + 1 < Code.NativePrefixEnd &&
                        Code.Instructions[next + 1] is { OpCode: ExecutableOpCode.StoreLocal, StoresCapturedLocal: false }))
                    {
                        Index = next;
                        Publish(update, updated);
                        return true;
                    }
                }
                Publish(producer, result);
            }
            return true;

        Declined:
            // A prepared child already owns the evaluated argument. Other
            // continuations resume from ordinary operands at this same call.
            if (_preparedChild is null)
            {
                if (borrowedArgument >= 0) forwardedArgument = Read(borrowedArgument, 1);
                if (forwardedArgument is not null)
                {
                    Put(start, start + 1, target);
                    Put(start + 1, start + 2, forwardedArgument);
                }
            }
            return false;
        }

        private bool CallFunction(ExecutableCallSite call, int start, object target,
            scoped Span<ExecutableSlot> arguments, bool borrowedArgument, out ExecutableSlot result, out bool suppliedReturn)
        {
            result = ExecutableSlot.FromObject(PyNone.Instance);
            suppliedReturn = false;
            if (_scope.State.CurrentRecursionDepth >= NativeCallDepth) return false;
            var function = target as PyExecutableFunction;
            object? receiver = call.HasReceiver && Values[start].Object is not PyNone ? Values[start].Object : null;
            if (target is PyBoundMethod { Function: PyExecutableFunction boundFunction } bound)
            {
                function = boundFunction;
                receiver = bound.Receiver;
            }
            if (function is null || !function.TrySelectNativeCall(call, receiver, out var completePositional)) return false;
            _scope.EnterInterpreterFrame(call.CallSpan);
            var entered = true;
            try
            {
                if (borrowedArgument) _scope.CheckExecution(call.CallSpan);
                if (function.TryCompleteNativeCall(call, arguments, receiver, _scope, completePositional, borrowedArgument,
                    out result, out suppliedReturn))
                    return true;
                var consumedByIteration = call.ConsumedByIteration || _consumedByIteration && call.ReturnedDirectly;
                ExecutionContext? child;
                if (call.UnpackedReturnCount != 0 && Code.LastOwnedTemporarySlot < start)
                    StartNativeFixedReturn(function, call, arguments, receiver, start, consumedByIteration,
                        completePositional, borrowedArgument, out result, out child);
                else function.StartNativeCall(call, arguments, receiver, _scope, call.CallSpan,
                    consumedByIteration, completePositional, borrowedArgument, default, out _, out result, out child);
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

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void StartNativeFixedReturn(PyExecutableFunction function, ExecutableCallSite call,
            scoped Span<ExecutableSlot> arguments, object? receiver, int start, bool consumedByIteration,
            bool completePositional, bool borrowedArgument, out ExecutableSlot result, out ExecutionContext? child)
        {
            // This destination cannot overlap an owned temporary. Binding
            // consumes arguments before the callee writes. Track wider returns
            // before invoking it so a terminal exit can retire all references.
            var originalExtent = LiveExtent;
            var returned = Values.Slice(start, call.UnpackedReturnCount);
            LiveExtent = Math.Max(originalExtent, start + returned.Length);
            function.StartNativeCall(call, arguments, receiver, _scope, call.CallSpan,
                consumedByIteration, completePositional, borrowedArgument, returned, out var unpacked, out result, out child);
            if (unpacked) Index++; // The caller completes this existing unpack after leaving the call.
            else LiveExtent = originalExtent;
        }

        private bool TryReturnUnpackedTuple(in ExecutableInstruction instruction)
        {
            if (Code.NativePrefixEnd != Code.Instructions.Length ||
                !IsUnpackedTupleReturn(Code, Index, instruction, _unpackedReturnCount)) return false;
            AdmitUnpackedReturnScalars(Values.Slice(instruction.InputStart, instruction.ItemCount), _scope, instruction.Span);
            // The return wrapper copies these already-admitted operands before
            // native cleanup. The hot execution state retains no caller borrow.
            _unpackedTupleStart = instruction.InputStart;
            return true;
        }

        private void StoreLocal(in ExecutableInstruction instruction)
        {
            var source = instruction.InputStart;
            if (source < Code.FirstTemporarySlot)
            {
                // The input can still be read through its original binding.
                var input = Take(instruction);
                if (!instruction.StoresCapturedLocal) Values[instruction.LocalSlot].Object = input;
                else StoreCapturedLocal(instruction.LocalSlot, ExecutableSlot.FromObject(input));
                return;
            }

            var value = ReadSlotForTransfer(source);
            Values[source] = default;
            Trim(instruction.LiveExtent);
            if (!instruction.StoresCapturedLocal) Values[instruction.LocalSlot] = value;
            else StoreCapturedLocal(instruction.LocalSlot, value);
        }

        private void StoreCapturedLocal(int slot, ExecutableSlot value)
        {
            if (Values[slot].Reference is ExecutableCell cell) cell.StoreSlot(value);
            else Values[slot] = value;
        }

        private ExecutableSlot ReadSlotForTransfer(int source)
        {
            if (source < 0) return ExecutableSlot.FromObject(Read(source, 0));
            // The operand pass never forwards a captured local into a
            // consumer. Its LoadLocal reads the cell into a temporary.
            var value = Values[source];
            if (value.IsUnassigned) _ = Read(source, 0);
            return value;
        }

        private object Read(int source, int input)
            => ReadExecutableOperand(Code, Values, source, Index, input, _scope, _cells);

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
            if (!instruction.HasResultConsumer) { Put(instruction.ResultSlot, instruction.LiveExtent, value); return; }
            if (instruction.LiveExtent < LiveExtent) Trim(instruction.LiveExtent);
            if (!_scope.State.CanContinue)
            {
                Put(instruction.ResultSlot, instruction.LiveExtent, value);
                return;
            }
            Values[instruction.ResultSlot] = default;
            LiveExtent = instruction.ResultSlot;
            ref readonly var consumer = ref Code.Instructions[Index + 1];
            if (consumer.OpCode == ExecutableOpCode.StoreLocal)
            {
                if (!consumer.StoresCapturedLocal) Values[consumer.LocalSlot] = value;
                else StoreCapturedLocal(consumer.LocalSlot, value);
            }
            else if (consumer.OpCode == ExecutableOpCode.StoreFunctionDefault)
                StoreDefaultResult(Values, consumer, value, _scope.State);
            else if (consumer.OpCode == ExecutableOpCode.StoreClosure)
            {
                if (_cells.GetCell(consumer.ClosureSlot) is not { CanStoreDirectly: true } cell)
                {
                    Put(instruction.ResultSlot, instruction.LiveExtent, value);
                    return;
                }
                cell.StoreSlot(value);
            }
            else if (consumer.OpCode != ExecutableOpCode.PopTop)
                throw new InvalidOperationException("Invalid prepared result consumer.");
            else if (value.IsUnassigned) _ = value.Object.RequireNotNull();
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
                Values[i].RetireTemporary(_scope.MemoryGovernor);
            // Dead references stay in the private span or already-admitted owned
            // backing until this synchronous prefix ends. Their owners remain
            // funded. Clear the dead tail once before transfer.
            LiveExtent = liveExtent;
        }

        internal void PrepareTransfer()
        {
            if (_hasDeferredDefinitions) MaterializeDeferredDefinitions();
            // Final managed slots transfer directly; no borrowed side span remains.
            Values[LiveExtent..].Clear();
        }
        internal void TransferOwnership(ExecutionContext frame)
        {
            if (_preparedChild is { } child)
            {
                var call = Code.CallSites[Code.Instructions[Index].CallSiteIndex];
                frame.AdoptPreparedChild(child, call, Code.Instructions[Index].ResultSlot, _preparedChildOwner!);
                _preparedChild = null;
                _preparedChildOwner = null;
            }
            OwnershipTransferred = true;
        }
        internal void Dispose()
        {
            if (OwnershipTransferred) return;
            try
            {
                if (_preparedChild is { } child)
                {
                    _preparedChild = null;
                    try { ExecutionContext.AbortPreparedCall(child, _preparedChildOwner!); }
                    finally { _scope.LeaveInterpreterFrame(); }
                }
            }
            finally
            {
                try
                {
                    try { Trim(Code.FirstTemporarySlot); }
                    finally { if (_hasDeferredDefinitions) RefundDeferredDefinitions(); }
                }
                finally
                {
                    if (_entered) _scope.LeaveFunctionCall();
                    _entered = false;
                }
            }
        }
    }

    internal static ExecutionContext CreateNativeContinuation(PyExecutableFunction function,
        in BoundFunctionArguments bound, LythonSourceSpan span, int index, int liveExtent, bool consumedByIteration,
        LythonRuntimeException? failure)
    {
        var frame = function.EnterInvocationFrame(bound, span, enterFunctionCall: false);
        InitializeExecutableFrame(function.CodeObject, frame, bound, function.Captures,
            lookupOwner: function, returnConsumedByIteration: consumedByIteration, continuingNative: true);
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
