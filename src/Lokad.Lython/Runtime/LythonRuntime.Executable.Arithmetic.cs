using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Completed builtin value computation borrows final slots. It neither owns
    // operand retirement/publication nor manufactures asynchronous call state.
    // Unknown protocol values return before executing their operation.
    private static bool TryEvaluateBuiltinArithmetic(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int index, ExecutionContext scope, out ExecutableSlot result)
    {
        result = default;
        ref readonly var instruction = ref code.Instructions[index];
        var augmented = instruction.OpCode == ExecutableOpCode.Augmented;
        if (augmented && instruction.AugmentedOperator == AugmentedAssignmentOperatorSyntax.MatrixMultiply)
            return false;
        if (TryReadDoubleOperand(code, values, instruction.InputStart, out var left))
        {
            if (instruction.OpCode == ExecutableOpCode.Unary)
            {
                if (instruction.UnaryOperator == ExecutableUnaryOperator.Minus)
                { result.SetDouble(-left); return true; }
            }
            else if (TryReadDoubleOperand(code, values, instruction.SecondInput, out var right))
            {
                var floatingOperation = augmented ? AugmentedBinaryOperator(instruction.AugmentedOperator) : instruction.BinaryOperator;
                switch (floatingOperation)
                {
                    case BinaryOperatorSyntax.Add: result.SetDouble(left + right); return true;
                    case BinaryOperatorSyntax.Subtract: result.SetDouble(left - right); return true;
                    case BinaryOperatorSyntax.Multiply: result.SetDouble(left * right); return true;
                }
            }
        }
        var first = ReadExecutableOperand(code, values, instruction.InputStart, index, 0, scope);
        if (instruction.OpCode == ExecutableOpCode.Unary)
        {
            if (instruction.UnaryOperator == ExecutableUnaryOperator.Not ? !CanUseNativeTruth(first)
                : first is not (BigInteger or double or bool)) return false;
            result.Object = EvaluateExecutableUnary(instruction.UnaryOperator, first, scope, instruction.Span);
            return true;
        }
        var second = ReadExecutableOperand(code, values, instruction.SecondInput, index, 1, scope);
        if (!augmented && TryEvaluateScalarComparison(instruction.BinaryOperator, first, second,
            scope, instruction.Span, out var truth))
        {
            result.Object = RuntimeValue(truth);
            return true;
        }
        var operation = augmented ? AugmentedBinaryOperator(instruction.AugmentedOperator) : instruction.BinaryOperator;
        if (first is PyString && second is PyString)
        {
            if (operation != BinaryOperatorSyntax.Add) return false;
        }
        else
        {
            if (first is not (BigInteger or double or bool) || second is not (BigInteger or double or bool))
                return false;
            if (!(operation switch
            {
                BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or BinaryOperatorSyntax.Multiply
                    or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo or BinaryOperatorSyntax.Divide
                    or BinaryOperatorSyntax.Equal or BinaryOperatorSyntax.NotEqual or BinaryOperatorSyntax.Less
                    or BinaryOperatorSyntax.LessEqual or BinaryOperatorSyntax.Greater or BinaryOperatorSyntax.GreaterEqual => true,
                BinaryOperatorSyntax.BitwiseOr or BinaryOperatorSyntax.BitwiseXor or BinaryOperatorSyntax.BitwiseAnd
                    or BinaryOperatorSyntax.LeftShift or BinaryOperatorSyntax.RightShift => first is not double && second is not double,
                _ => false,
            })) return false;
        }
        result.Object = augmented
            ? EvaluateAugmentedBuiltin(first, second, instruction.AugmentedOperator, scope, instruction.Span)
            : EvaluateBinaryOperator(instruction.BinaryOperator, first, second, scope, instruction.Span);
        return true;
    }

    private static bool TryReadDoubleOperand(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int source, out double value)
    {
        if (source >= 0) return values[source].TryGetDouble(out value);
        if (code.Constants[~source] is double floating) { value = floating; return true; }
        value = 0;
        return false;
    }

    private static object ReadExecutableOperand(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int source, int index, int input, ExecutionContext scope)
    {
        if (source < 0) return RuntimeValue(code.Constants[~source]);
        var value = values[source].Object;
        if (source < code.FirstTemporarySlot)
        {
            if (value is ExecutableCell cell) value = cell.Value;
            if (value is null) return LoadMissingLocal(code, source, code.OperandReadSpan(index, input), scope);
        }
        return value.RequireNotNull();
    }

    internal sealed partial class ExecutionContext
    {
        private static bool TryExecuteBuiltinArithmetic(ref ExecutableWorkState execution,
            ExecutionContext scope, ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            // Skipping intermediates is valid only without private cleanup or
            // pending async users. Their existing general path preserves order.
            if (!execution.Stack.CanRetireWithoutCleanup(instruction.ResultSlot)) return false;
            try
            {
                var end = execution.InstructionIndex;
                ExecutableSlot value = default;
                var completed = instruction.OpCode != ExecutableOpCode.Unary &&
                    TryEvaluateIntegerInstruction(execution.Code, execution.Stack.Values, end, scope, out end, out value);
                if (!completed && !TryEvaluateBuiltinArithmetic(execution.Code, execution.Stack.Values, end, scope, out value))
                    return false;
                execution.InstructionIndex = end;
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[end], value);
                return true;
            }
            catch { execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }
    }
}
