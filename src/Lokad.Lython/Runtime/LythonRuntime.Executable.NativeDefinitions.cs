using Lokad.Lython.Frontend;

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
                case ExecutableStartFunctionDefaults start:
                    var owner = new PendingFunctionDefaults(start.Layout, _scope, span);
                    try { Publish(instruction, owner); }
                    catch { owner.Dispose(); throw; }
                    return true;
                case ExecutableAppendFunctionDefault:
                    var result = Values[instruction.ResultSlot];
                    if (result.IsUnassigned) _ = result.Object.RequireNotNull();
                    Values[instruction.ResultSlot] = default;
                    Trim(instruction.LiveExtent);
                    StoreDefaultResult(Code, Values, instruction, result);
                    return true;
                case ExecutableCreateFunction function
                    when CanCreateWithoutCapture(function.CodeObject) && !NeedsDefinitionContext(function, _scope):
                    _scope.EnterInterpreterFrame(span);
                    PyFunctionBase value;
                    try
                    {
                        value = CreateInlineDefinition(instruction, function.CodeObject, function.ParameterLayout,
                            function.IsGenerator, PythonCallableKind.Function, function.InputCount);
                    }
                    finally { _scope.LeaveInterpreterFrame(); }
                    Publish(instruction, value);
                    return true;
                case ExecutableCreateLambda { Binding.NamespaceClosure: false } lambda
                    when CanCreateWithoutCapture(lambda.CodeObject):
                    var binding = lambda.Binding;
                    Publish(instruction, CreateInlineDefinition(instruction, binding.CodeObject, binding.Layout,
                        binding.IsGenerator, PythonCallableKind.Lambda, lambda.InputCount));
                    return true;
                default:
                    return false;
            }
        }

        private static bool CanCreateWithoutCapture(ExecutableCodeObject code)
            => code.ClosureNames.Count == 0 && CanDefineLexically(code);

        private PyFunctionBase CreateInlineDefinition(in ExecutableInstruction instruction,
            ExecutableCodeObject code, FunctionParameterLayout layout, bool generator, PythonCallableKind kind,
            int inputCount)
        {
            var first = instruction.InputStart;
            try
            {
                return CreateLexicalFunctionValue(layout, code, [], generator, _scope, instruction.Span, kind,
                    Values.Slice(first, inputCount));
            }
            finally { Trim(first); }
        }
    }
}
