using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool TryFinishDirectCasedReturn(PyExecutableFunction function, ExecutableSlot argument,
        LythonSourceSpan callSpan, out ExecutableSlot result)
    {
        result = default;
        if (!argument.HasMaterializedValue || argument.Reference is not PyString text) return false;
        var scope = function.DefinitionContext;
        var code = function.CodeObject;
        var split = code.DirectCasedReturnIndex;
        var instructions = code.Instructions;
        ExecutableCell? width = null;
        if (split != 0)
        {
            var name = ~instructions[0].InputStart - code.Constants.Length;
            width = function.Captures.GetCell(instructions[split - 1].ClosureSlot);
            if (width is not { CanStoreDirectly: true } ||
                !TryResolveExecutableGlobal(code, name, scope, out var length) ||
                !ExecutionContext.IsLengthBuiltin(length)) return false;
        }

        try
        {
            scope.EnterFunctionCall(callSpan);
            var previous = scope.State.CurrentException;
            try
            {
                if (width is not null)
                {
                    scope.CheckExecution(instructions[0].Span);
                    var lengthCall = code.CallSites[instructions[0].CallSiteIndex];
                    var value = InvokeSingleInput<object>(text, LengthOperation, lengthCall.CallSpan, scope);
                    if (!value.TryGetInteger(out var count))
                        throw new InvalidOperationException("Exact text length did not produce a machine integer.");
                    var index = 1;
                    var computed = EvaluateClosedIntegerSequence<FactoryBindingIntegerSource>(code, count, scope, ref index);
                    scope.CheckExecution(instructions[index].Span);
                    width.StoreSlot(computed);
                }
                scope.CheckExecution(instructions[split].Span);
                var words = ExecuteCasedSplit(code, split, text, scope, returnConsumedByIteration: true);
                try
                {
                    scope.CheckExecution(instructions[^1].Span);
                    result = ExecutableSlot.FromObject(words);
                }
                catch
                {
                    if (words is ExecutableTemporaryValue owned) owned.Dispose();
                    throw;
                }
                return true;
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
            PyFunctionBinding.AnnotateException(error, scope, function.Name, callSpan);
            throw;
        }
    }

    private static object ExecuteCasedSplit(ExecutableCodeObject code, int index, PyString receiver,
        ExecutionContext context, bool returnConsumedByIteration)
    {
        var recipe = (ExecutableCasedSplit)code.CallSites[code.Instructions[index + 1].CallSiteIndex].SplitPrefix!;
        var resultCall = code.CallSites[code.Instructions[index + 3].CallSiteIndex];
        context.EnterInterpreterFrame(recipe.CasingCallSpan);
        try
        {
            context.CheckExecution(recipe.CasingMemberSpan);
            context.CheckExecution(recipe.CasingCallSpan);
            context.CheckExecution(recipe.SplitMemberSpan);
            context.CheckExecution(recipe.SplitCallSpan);
            return resultCall.ConsumedByIteration || returnConsumedByIteration && resultCall.ReturnedDirectly
                ? PyStringOps.PrepareCasedWhitespaceIteration(receiver, recipe.Upper, context, recipe.SplitCallSpan)
                : PyStringOps.SplitCasedWhitespace(receiver, recipe.Upper,
                    context.MemoryGovernor, recipe.SplitCallSpan, context);
        }
        catch (Exception error) when (TryTranslateCallableException(error, recipe.SplitCallSpan, out var translated)) { throw translated; }
        finally { context.LeaveInterpreterFrame(); }
    }

    private static bool TryReadAsciiTrim(ExecutableCallSite call, object target, ReadOnlySpan<ExecutableSlot> values,
        int start, [NotNullWhen(true)] out PyString? text, out byte trim)
    {
        text = null;
        trim = default;
        if (!ReferenceEquals(target, StringMembers.RStripMethod) ||
            values[start].Reference is not PyString input ||
            values[start + call.ArgumentOffset].Reference is not PyString { ByteLength: 1 } chars) return false;
        text = input;
        trim = chars.Utf8Span[0];
        return true;
    }

    private static PyList? ExecuteTrimmedSplit(ExecutableTrimmedSplit recipe, PyString text, byte trim,
        LythonSourceSpan trimSpan, LythonSourceSpan splitSpan, Span<ExecutableSlot> unpacked, ExecutionContext context)
    {
        int end;
        context.EnterInterpreterFrame(trimSpan);
        try { end = PyStringOps.FindAsciiRightTrimEnd(text, trim, context, trimSpan); }
        finally { context.LeaveInterpreterFrame(); }

        context.CheckExecution(recipe.MemberSpan);
        context.CheckExecution(recipe.SeparatorSpan);
        context.EnterInterpreterFrame(splitSpan);
        try { return PyStringOps.SplitUtf8Prefix(text, end, recipe.Separator, unpacked, context, splitSpan); }
        finally { context.LeaveInterpreterFrame(); }
    }

    internal sealed partial class ExecutionContext
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryExecuteCasedSplitMember(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            // The fallback can reread the original operand. This guard neither
            // invokes a method nor changes the receiver's slot.
            if (ReadOperand(ref execution, instruction.InputStart, scope, 0) is not PyString receiver) return false;
            // The immediate chain stays within this basic block. Original
            // operand layout gives both calls the same result position; publish
            // through the final call, including its local-store continuation.
            try
            {
                var result = ExecuteCasedSplit(execution.Code, execution.InstructionIndex, receiver, invocation ?? scope,
                    execution.ReturnConsumedByIteration);
                execution.InstructionIndex += 3;
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }

        private static bool TryExecuteTrimmedSplit(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, ExecutableTrimmedSplit recipe, ExecutableCallSite call, int start, object target)
        {
            if (!TryReadAsciiTrim(call, target, execution.Stack.Values, start, out var text, out var trim)) return false;
            var finalIndex = execution.InstructionIndex + 3;
            ref readonly var final = ref execution.Code.Instructions[finalIndex];
            if (final.InputStart != start) return false;
            var split = execution.Code.CallSites[final.CallSiteIndex];
            var unpacked = PrepareUnpackedCallReturn(ref execution, split, start, out var originalExtent);
            var list = ExecuteTrimmedSplit(recipe, text, trim, call.CallSpan, split.CallSpan, unpacked, invocation ?? scope);
            execution.InstructionIndex = finalIndex;
            PublishSplitResult(ref execution, invocation, scope, start, unpacked.Length, originalExtent, list);
            return true;
        }
    }
}
