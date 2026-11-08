using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private static InstructionPause ExecuteComparisonInstruction(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction, bool asynchronous,
            out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan)
        {
            pending = default;
            pendingSpan = null;
            var first = ReadOperand(ref execution, instruction.InputStart, scope, 0);
            var second = ReadOperand(ref execution, instruction.SecondInput, scope, 1);
            try
            {
                if (TryEvaluateNumericComparison(instruction.BinaryOperator, first, second!, scope, instruction.Span, out var truth))
                    pending = new(CompleteComparisonBranch(ref execution, scope, instruction, truth));
                else if (first is PyString && second is PyString)
                {
                    // Exact strings already complete synchronously under
                    // either API, without a lexical-context promotion.
                    var result = EvaluateBinaryOperator(instruction.BinaryOperator, first, second!, scope, instruction.Span);
                    pending = new(CompleteComparisonBranch(ref execution, scope, instruction, IsTruthy(result, scope, instruction.Span)));
                }
                else if (invocation is null) return InstructionPause.ContextRequired;
                else if (!asynchronous)
                {
                    var result = EvaluateBinaryOperator(instruction.BinaryOperator, first, second!, invocation, instruction.Span);
                    execution.Stack.RetireTo(instruction.LiveExtent);
                    invocation.CheckExecution(instruction.Span);
                    pending = new(CompleteComparisonBranch(ref execution, invocation, instruction, IsTruthy(result, invocation, instruction.Span)));
                }
                else
                {
                    pending = invocation.ExecuteComparisonBranchAsync(instruction, first, second);
                    if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                }
                return InstructionPause.Continue;
            }
            catch { execution.Stack.RetireTo(instruction.LiveExtent); throw; }
        }

        private static bool CompleteComparisonBranch(ref ExecutableWorkState execution, ExecutionContext context,
            in ExecutableInstruction instruction, bool truth)
        {
            execution.Stack.RetireTo(instruction.LiveExtent);
            context.CheckExecution(instruction.Span);
            if (truth) return false;
            execution.InstructionIndex = instruction.TargetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteComparisonBranchAsync(ExecutableInstruction instruction, object first, object second)
        {
            try
            {
                var result = await EvaluateBinaryOperatorAsync(instruction.BinaryOperator, first, second!, this, instruction.Span).ConfigureAwait(false);
                _execution.Stack.RetireTo(instruction.LiveExtent);
                CheckExecution(instruction.Span);
                var truth = await IsTruthyAsync(result, this, instruction.Span).ConfigureAwait(false);
                return CompleteComparisonBranch(ref _execution, this, instruction, truth);
            }
            catch { _execution.Stack.RetireTo(instruction.LiveExtent); throw; }
        }

    }
}
