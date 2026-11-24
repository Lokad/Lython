using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
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
        var slots = left.CodeObject.CaptureSources!.Slots;
        var single = slots is [var local] ? bindings.Slice(local, 1) : default;
        var cells = single.IsEmpty
            ? CaptureExecutableCells(left.CodeObject, scope, create.Span, bindings, default, slots, out _, inline: true)
            : default;
        var first = CreateFunctionValue(left.ParameterLayout, left.CodeObject, GetGlobalContext(scope),
            cells, default, false, scope, create.Span, uncapturedLocal: single);
        scope.CheckExecution(code[index++].Span); // Store the left binding.

        ref readonly var nested = ref code[index++];
        var right = (ExecutableCreateFunction)factory.Constants[nested.ConstantIndex]!;
        scope.CheckExecution(nested.Span);
        scope.CheckExecution(code[index++].Span); // Private store.
        scope.CheckExecution(code[index++].Span); // Read the left binding.
        scope.CheckExecution(code[index++].Span); // Read the private callable.
        ref readonly var invoke = ref code[index++];
        scope.CheckExecution(invoke.Span);
        var second = CreateImmediatelyCalledLambda(right.CodeObject, argument, scope, factory.CallSites[invoke.CallSiteIndex].CallSpan);
        for (; index < code.Length; index++) scope.CheckExecution(code[index].Span);

        destination[1].Object = first;
        destination[0].Object = second;
    }

    private static PyFunctionBase CreateImmediatelyCalledLambda(ExecutableCodeObject body, long argument,
        ExecutionContext scope, LythonSourceSpan callSpan)
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
                    var lambda = ((ExecutableCreateLambda)body.Constants[create.ConstantIndex]!).Binding;
                    scope.CheckExecution(create.Span);
                    var function = CreateFunctionValue(lambda.Layout, lambda.CodeObject, GetGlobalContext(scope), default,
                        default, false, scope, create.Span, uncapturedLocal: new Span<ExecutableSlot>(ref value));
                    scope.CheckExecution(body.Instructions[^1].Span);
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
        private bool ApplyDefinition(in ExecutableInstruction instruction)
        {
            var span = instruction.Span;
            switch (Code.Constants[instruction.ConstantIndex])
            {
                case ExecutableCreateFunction function
                    when !NeedsDefinitionContext(function, _scope) && CanDefineWithoutInvocation(function.CodeObject,
                        Code, _cells, _scope, span, out var functionCaptures):
                    if (instruction.ImmediatelyCalledDefinitionCapture != int.MaxValue &&
                        TryCompleteImmediatelyCalledDefinition(function.CodeObject, instruction.ImmediatelyCalledDefinitionCapture))
                        return true;
                    // Defaults have executed and these direct annotation reads
                    // need no child evaluator. Like a lambda, construction
                    // finishes in the current admitted body.
                    Publish(instruction, CreateInlineDefinition(instruction, function.CodeObject, function.ParameterLayout,
                        function.IsGenerator, function.InputCount, functionCaptures,
                        function.PostponedAnnotations, function.EagerAnnotationReads, function.DirectKeywordDefault));
                    return true;
                case ExecutableCreateLambda { Binding.NamespaceClosure: false } lambda
                    when CanDefineWithoutInvocation(lambda.CodeObject, Code, _cells, _scope, span, out var lambdaCaptures):
                    var binding = lambda.Binding;
                    Publish(instruction, CreateInlineDefinition(instruction, binding.CodeObject, binding.Layout,
                        binding.IsGenerator, lambda.InputCount, lambdaCaptures));
                    return true;
                default:
                    return false;
            }
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
            var result = CreateImmediatelyCalledLambda(body, integer, _scope, Code.CallSites[invoke.CallSiteIndex].CallSpan);
            Publish(invoke, result);
            return true;
        }

        private PyFunctionBase CreateInlineDefinition(in ExecutableInstruction instruction,
            ExecutableCodeObject code, FunctionParameterLayout layout, bool generator,
            int inputCount, int[]? captures, PyString[]? postponedAnnotations = null,
            ExecutableInstruction[]? eagerReads = null, bool directKeywordDefault = false)
        {
            var first = instruction.InputStart;
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
                var function = CreateLexicalFunctionValue(layout, code, cells, generator, _scope, instruction.Span,
                    Values.Slice(first, inputCount), postponedAnnotations, preparedAnnotations, directKeywordDefault, uncapturedLocal);
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
