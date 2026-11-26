using Lokad.Lython.Frontend;
using PreparedFunctionAnnotations = (object? Values, int Capacity, bool Repeated);

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    [System.Runtime.CompilerServices.InlineArray(ExecutableCodeObject.ClosedCaptureCapacity)]
    private struct ClosedCaptureBindings { private ExecutableSlot _first; }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void CompleteClosedCapturedPair(ExecutableCodeObject factory, ExecutionContext scope,
        long argument, Span<ExecutableSlot> destination)
    {
        ClosedCaptureBindings buffer = default;
        Span<ExecutableSlot> bindings = buffer;
        var code = factory.Instructions;
        var index = 0;
        while (code[index].OpCode == ExecutableOpCode.LoadLocal)
        {
            scope.CheckExecution(code[index++].Span);
            var value = EvaluateClosedIntegerSequence<FactoryBindingIntegerSource>(factory, argument, scope, ref index);
            ref readonly var store = ref code[index++];
            scope.CheckExecution(store.Span);
            bindings[store.LocalSlot] = value;
        }

        ref readonly var create = ref code[index++];
        scope.CheckExecution(create.Span);
        var left = (ExecutableCreateFunction)factory.Constants[create.ConstantIndex]!;
        var captures = left.CodeObject.CaptureSources!;
        var slots = captures.Slots;
        var single = slots is [var local] ? bindings.Slice(local, 1) : default;
        long leftBytes = 0, rightBytes = 0;
        try
        {
            ExecutableCaptures cells = default;
            if (single.IsEmpty)
            {
                // These captures come entirely from private bindings in this
                // operation. Each cell and the array have this one owner.
                cells = CaptureExecutableCells(left.CodeObject, scope, create.Span, bindings, default, slots, out _, inline: true);
                leftBytes = ExecutableCaptureBytes(slots.Length, slots.Length);
            }
            var first = CreateFunctionValue(left.ParameterLayout, left.CodeObject, GetGlobalContext(scope),
                cells, default, false, scope, create.Span, uncapturedLocal: single);
            leftBytes += FunctionValueBytes;
            if (!single.IsEmpty) leftBytes += InlineCaptureBytes(captures.CanCaptureByValue);
            scope.CheckExecution(code[index++].Span); // Store the left binding.

            ref readonly var nested = ref code[index++];
            var right = (ExecutableCreateFunction)factory.Constants[nested.ConstantIndex]!;
            scope.CheckExecution(nested.Span);
            scope.CheckExecution(code[index++].Span); // Private store.
            scope.CheckExecution(code[index++].Span); // Read the left binding.
            scope.CheckExecution(code[index++].Span); // Read the private callable.
            ref readonly var invoke = ref code[index++];
            scope.CheckExecution(invoke.Span);
            var second = CreateImmediatelyCalledLambda(right.CodeObject, argument, scope,
                factory.CallSites[invoke.CallSiteIndex].CallSpan, out rightBytes);
            for (; index < code.Length; index++) scope.CheckExecution(code[index].Span);

            destination[1].Object = first;
            destination[0].Object = second;
        }
        catch
        {
            var governor = scope.MemoryGovernor;
            try { governor.Release(rightBytes); }
            finally { governor.Release(leftBytes); }
            throw;
        }
    }

    private static PyFunctionBase CreateImmediatelyCalledLambda(ExecutableCodeObject body, long argument,
        ExecutionContext scope, LythonSourceSpan callSpan, out long privateBytes)
    {
        scope.EnterInterpreterFrame(callSpan);
        try
        {
            var previous = scope.State.CurrentException;
            try
            {
                scope.EnterFunctionCall(callSpan);
                try
                {
                    var inner = 0;
                    var value = EvaluateClosedIntegerSequence<CaptureIntegerSource>(body, argument, scope, ref inner);
                    scope.CheckExecution(body.Instructions[inner++].Span); // The lambda's private capture.
                    ref readonly var create = ref body.Instructions[inner];
                    var lambda = (ExecutableCreateFunction)body.Constants[create.ConstantIndex]!;
                    scope.CheckExecution(create.Span);
                    var function = CreateFunctionValue(lambda.ParameterLayout, lambda.CodeObject, GetGlobalContext(scope), default,
                        default, false, scope, create.Span, uncapturedLocal: new Span<ExecutableSlot>(ref value));
                    var bytes = FunctionValueBytes + InlineCaptureBytes(lambda.CodeObject.CaptureSources!.CanCaptureByValue);
                    try { scope.CheckExecution(body.Instructions[^1].Span); }
                    catch
                    {
                        scope.MemoryGovernor.Release(bytes);
                        throw;
                    }
                    privateBytes = bytes;
                    return function;
                }
                catch (LythonRuntimeException error)
                {
                    if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                        error.PythonContext ??= previous;
                    throw;
                }
                finally
                {
                    scope.State.SetCurrentException(previous);
                    scope.LeaveFunctionCall();
                }
            }
            catch (LythonRuntimeException error)
            {
                PyFunctionBinding.AnnotateException(error, scope, body.Name, callSpan);
                throw;
            }
        }
        finally { scope.LeaveInterpreterFrame(); }
    }

    internal ref partial struct NativeExecution
    {
        private bool TryDeferDefinition(in ExecutableInstruction instruction)
        {
            var store = Code.Instructions[Index + 1];
            var keywordSlot = instruction.DeferredKeywordSlot;
            var sharedPositional = keywordSlot == store.LocalSlot;
            var defaults = Values.Slice(instruction.InputStart, sharedPositional ? 1 : 2);
            var keywords = defaults[^1];
            if (!keywords.HasPrepaidCallable || !keywords.HasPrivateKeywordDefault ||
                (!sharedPositional && !defaults[0].HasPrivatePositionalDefault)) return false;
            _scope.CheckExecution(store.Span);
            if (!sharedPositional) Values[store.LocalSlot] = defaults[0];
            Values[keywordSlot] = keywords;
            defaults.Clear();
            Trim(instruction.InputStart);
            _hasDeferredDefinitions = true;
            Index++;
            return true;
        }

        private void MaterializeDeferredDefinitions()
        {
            foreach (var index in Code.DeferredDefinitionSites!)
            {
                ref readonly var create = ref Code.Instructions[index];
                ref var keyword = ref Values[create.DeferredKeywordSlot];
                if (!keyword.HasPrepaidCallable) continue;
                ref var positional = ref Values[Code.Instructions[index + 1].LocalSlot];
                var definition = (ExecutableCreateFunction)Code.Constants[create.ConstantIndex]!;
                var positionalDefaults = definition.LiteralPositionalDefaults is { } literals ? ExecutableSlot.FromObject(literals) : positional;
                var callable = CreateFunctionValue(definition.ParameterLayout, definition.CodeObject,
                    GetGlobalContext(_scope), default, new(positionalDefaults, keyword), false, _scope, create.Span);
                keyword = default;
                positional.Object = callable;
            }
            _hasDeferredDefinitions = false;
        }

        private void RefundDeferredDefinitions()
        {
            var bytes = 0L;
            foreach (var index in Code.DeferredDefinitionSites!)
            {
                ref var keyword = ref Values[Code.Instructions[index].DeferredKeywordSlot];
                if (!keyword.HasPrepaidCallable) continue;
                ref var positional = ref Values[Code.Instructions[index + 1].LocalSlot];
                // Compiled positional tuples need no local slot or invocation charge.
                var positionalBytes = positional.HasPrivatePositionalDefault
                    ? FunctionDefaults.InlinePositionalBytes(positional.ReadDefaultOperand()) : 0L;
                bytes = checked(bytes + positionalBytes + FunctionValueBytes + PyDict.RecordKeys.SinglePreparedValueBytes);
                keyword = default;
                positional = default;
            }
            _hasDeferredDefinitions = false;
            if (bytes != 0) _scope.MemoryGovernor.Release(bytes);
        }

        private bool ApplyDefinition(in ExecutableInstruction instruction)
        {
            var span = instruction.Span;
            if (Code.Constants[instruction.ConstantIndex] is not ExecutableCreateFunction definition ||
                NeedsDefinitionContext(definition, _scope) || !CanDefineWithoutInvocation(definition.CodeObject,
                    Code, _cells, _scope, span, out var captures)) return false;
            if (instruction.OpCode == ExecutableOpCode.ApplyOperation &&
                instruction.ImmediatelyCalledDefinitionCapture != int.MaxValue &&
                TryCompleteImmediatelyCalledDefinition(definition.CodeObject, instruction.ImmediatelyCalledDefinitionCapture))
                return true;

            Publish(instruction, CreateInlineDefinition(instruction, definition, captures));
            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool TryCompleteImmediatelyCalledDefinition(ExecutableCodeObject body, int capture)
        {
            if (_scope.State.CurrentRecursionDepth >= NativeCallDepth) return false;
            var argument = Values[capture];
            long integer;
            if (argument.Reference is ExecutableCell cell ? !cell.TryReadInteger(out integer) : !argument.TryGetInteger(out integer))
                return false;

            var instructions = Code.Instructions;
            _scope.CheckExecution(instructions[Index + 1].Span);
            ref readonly var first = ref instructions[Index + 2];
            _scope.CheckExecution(first.Span);
            Publish(first, Values[first.LocalSlot]);
            _scope.CheckExecution(instructions[Index + 3].Span);
            Index += 4;
            ref readonly var invoke = ref instructions[Index];
            _scope.CheckExecution(invoke.Span);
            var result = CreateImmediatelyCalledLambda(body, integer, _scope, Code.CallSites[invoke.CallSiteIndex].CallSpan, out _);
            Publish(invoke, result);
            return true;
        }

        private PyFunctionBase CreateInlineDefinition(in ExecutableInstruction instruction,
            ExecutableCreateFunction definition, int[]? captures)
        {
            var first = instruction.InputStart;
            var code = definition.CodeObject;
            var eagerReads = definition.EagerAnnotationReads;
            PreparedFunctionAnnotations preparedAnnotations = default;
            try
            {
                if (eagerReads is not null)
                    preparedAnnotations = ReadEagerFunctionAnnotations(eagerReads, Code, code, _scope, instruction.Span);
                Span<ExecutableSlot> uncapturedLocal = default;
                if (eagerReads is null && captures is [>= 0] && Values[captures[0]].Reference is not ExecutableCell)
                    uncapturedLocal = Values.Slice(captures[0], 1);
                ExecutableCaptures cells = captures is null || !uncapturedLocal.IsEmpty ? default :
                    CaptureExecutableCells(code, _scope, instruction.Span, Values, _cells, captures, out _, inline: true);
                var function = CreateLexicalFunctionValue(definition, cells, _scope, instruction.Span,
                    Values.Slice(first, definition.InputCount), preparedAnnotations, uncapturedLocal);
                preparedAnnotations = default;
                return function;
            }
            finally
            {
                try { RefundEagerFunctionAnnotations(preparedAnnotations, _scope); }
                finally { Trim(first); }
            }
        }
    }
}
