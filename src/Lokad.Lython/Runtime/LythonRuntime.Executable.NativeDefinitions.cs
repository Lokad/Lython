using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using PreparedFunctionAnnotations = (object? Values, int Capacity, bool Repeated);

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
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
            var call = Code.CallSites[invoke.CallSiteIndex];
            _scope.CheckExecution(invoke.Span);
            _scope.EnterInterpreterFrame(call.CallSpan);
            try
            {
                var previous = _scope.State.CurrentException;
                PyFunctionBase function;
                try
                {
                    _scope.EnterFunctionCall(call.CallSpan);
                    try
                    {
                        var inner = 0;
                        var value = EvaluateClosedIntegerSequence<CaptureIntegerSource>(body, integer, _scope, ref inner);
                        _scope.CheckExecution(body.Instructions[inner++].Span); // The lambda's private capture.
                        ref readonly var create = ref body.Instructions[inner];
                        var lambda = ((ExecutableCreateLambda)body.Constants[create.ConstantIndex]!).Binding;
                        _scope.CheckExecution(create.Span);
                        function = CreateFunctionValue(lambda.Layout, lambda.CodeObject, GetGlobalContext(_scope), default,
                            default, false, _scope, create.Span, uncapturedLocal: new Span<ExecutableSlot>(ref value));
                        _scope.CheckExecution(body.Instructions[^1].Span);
                    }
                    catch (LythonRuntimeException error)
                    {
                        if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                            error.PythonContext ??= previous;
                        throw;
                    }
                    finally
                    {
                        _scope.State.SetCurrentException(previous);
                        _scope.LeaveFunctionCall();
                    }
                }
                catch (LythonRuntimeException error)
                {
                    PyFunctionBinding.AnnotateException(error, _scope, body.Name, call.CallSpan);
                    throw;
                }
                Publish(invoke, function);
                return true;
            }
            finally { _scope.LeaveInterpreterFrame(); }
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
