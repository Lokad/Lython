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
