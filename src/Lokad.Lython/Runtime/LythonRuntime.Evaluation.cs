using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object EvaluateExpression(ExpressionSyntax expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            var value = expression switch
            {
                IdentifierExpressionSyntax identifier => ResolveIdentifier(identifier, context),
                StringLiteralExpressionSyntax literal => CreateString(literal.Value, context, literal.Span),
                IntegerLiteralExpressionSyntax integer => ParseInteger(integer),
                FloatLiteralExpressionSyntax floating => ParseFloat(floating),
                BooleanLiteralExpressionSyntax boolean => boolean.Value,
                NoneLiteralExpressionSyntax => PyNone.Instance,
                FormattedStringExpressionSyntax formatted => EvaluateFormattedString(formatted, context),
                ListLiteralExpressionSyntax list => CreateListLiteral(list, context),
                ListComprehensionExpressionSyntax listComprehension => EvaluateListComprehension(listComprehension, context),
                GeneratorExpressionSyntax generator => EvaluateGeneratorExpression(generator, context),
                DictLiteralExpressionSyntax dict => EvaluateDictLiteral(dict, context),
                SetLiteralExpressionSyntax set => EvaluateSetLiteral(set, context),
                SetComprehensionExpressionSyntax setComprehension => EvaluateSetComprehension(setComprehension, context),
                DictComprehensionExpressionSyntax dictComprehension => EvaluateDictComprehension(dictComprehension, context),
                TupleLiteralExpressionSyntax tuple => CreateTupleLiteral(tuple, context),
                ParenthesizedExpressionSyntax parenthesized => EvaluateExpression(parenthesized.Inner, context),
                BytesLiteralExpressionSyntax bytes => CreateBytes(bytes.Value.ToArray(), context, bytes.Span),
                MemberExpressionSyntax member => ResolveMember(member, context),
                CallExpressionSyntax call => InvokeCall(call, context),
                SubscriptExpressionSyntax subscript => EvaluateSubscript(subscript, context),
                SliceExpressionSyntax slice => EvaluateSlice(slice, context),
                BinaryExpressionSyntax binary => EvaluateBinary(binary, context),
                ChainedComparisonExpressionSyntax chainedComparison => EvaluateChainedComparison(chainedComparison, context),
                UnaryExpressionSyntax unary => EvaluateUnary(unary, context),
                ConditionalExpressionSyntax conditional => EvaluateConditional(conditional, context),
                AssignmentExpressionSyntax assignment => EvaluateAssignmentExpression(assignment, context),
                LambdaExpressionSyntax lambda => CreateLambda(lambda, context),
                _ => throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}")
            };

            context.ObserveValue(value, expression.Span);
            return value;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or RegexParseException)
        {
            var runtime = new LythonRuntimeException("RuntimeError", ex.Message, expression.Span);
            runtime.SetSourcePathIfMissing(context.SourcePath);
            throw runtime;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }

        static PyList CreateListLiteral(ListLiteralExpressionSyntax list, ExecutionContext context)
        {
            if (list.UnpackingFlags.Any(flag => flag))
            {
                var expanded = new PyList([], context.MemoryGovernor, list.Span);
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var value = RuntimeValue(EvaluateExpression(list.Items[i], context));
                    if (!list.UnpackingFlags[i])
                    {
                        expanded.Add(value);
                        context.ObserveCollectionCount(expanded.Count, list.Span);
                        continue;
                    }

                    foreach (var item in ToSequence(value, list.Items[i].Span, context))
                    {
                        expanded.Add(RuntimeValue(item));
                        context.ObserveCollectionCount(expanded.Count, list.Span);
                    }
                }

                return expanded;
            }

            context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
            var items = new object[list.Items.Count];
            for (var i = 0; i < list.Items.Count; i++)
            {
                items[i] = RuntimeValue(EvaluateExpression(list.Items[i], context));
            }

            return new PyList(items, context.MemoryGovernor, list.Span);
        }

        static PyTuple CreateTupleLiteral(TupleLiteralExpressionSyntax tuple, ExecutionContext context)
        {
            if (!tuple.UnpackingFlags.Any(flag => flag))
            {
                return CreateTuple(
                    tuple.Items.Count,
                    i => RuntimeValue(EvaluateExpression(tuple.Items[i], context)),
                    context,
                    tuple.Span);
            }

            var expanded = new List<object>();
            for (var i = 0; i < tuple.Items.Count; i++)
            {
                var value = RuntimeValue(EvaluateExpression(tuple.Items[i], context));
                if (!tuple.UnpackingFlags[i])
                {
                    EnsureTupleExpansionCapacity(expanded.Count + 1, context, tuple.Span);
                    expanded.Add(value);
                    continue;
                }

                foreach (var item in ToSequence(value, tuple.Items[i].Span, context))
                {
                    EnsureTupleExpansionCapacity(expanded.Count + 1, context, tuple.Span);
                    expanded.Add(RuntimeValue(item));
                }
            }

            context.ObserveCollectionCount(expanded.Count, tuple.Span);
            return new PyTuple(expanded, context.MemoryGovernor, tuple.Span);
        }
    }

    private static void EnsureTupleExpansionCapacity(int count, ExecutionContext context, LythonSourceSpan span)
    {
        context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), span);
        context.ObserveCollectionCount(count, span);
    }

    private static object EvaluateBinary(BinaryExpressionSyntax binary, ExecutionContext context)
    {
        if (binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue.RequireNotNull()
                : EvaluateExpression(binary.Right, context).RequireNotNull();
        }

        if (binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return !IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue.RequireNotNull()
                : EvaluateExpression(binary.Right, context).RequireNotNull();
        }

        var left = EvaluateExpression(binary.Left, context);
        var right = EvaluateExpression(binary.Right, context);
        if (TryEvaluateNumericProtocol(binary.Operator, left, right, context, binary.Span, out var protocolResult))
        {
            return protocolResult;
        }

        return binary.Operator switch
        {
            BinaryOperatorSyntax.Add => EvaluateAdd(left, right, context, binary.Span),
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, binary.Span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, binary.Span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, binary.Span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, binary.Span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, context, binary.Span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, binary.Span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, binary.Span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, binary.Span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, binary.Span),
            BinaryOperatorSyntax.Less => EvaluateRichComparison(left, right, "__lt__", "__gt__", context, binary.Span, static value => value < 0),
            BinaryOperatorSyntax.LessEqual => EvaluateRichComparison(left, right, "__le__", "__ge__", context, binary.Span, static value => value <= 0),
            BinaryOperatorSyntax.Greater => EvaluateRichComparison(left, right, "__gt__", "__lt__", context, binary.Span, static value => value > 0),
            BinaryOperatorSyntax.GreaterEqual => EvaluateRichComparison(left, right, "__ge__", "__le__", context, binary.Span, static value => value >= 0),
            BinaryOperatorSyntax.Is => AreIdentical(left, right),
            BinaryOperatorSyntax.IsNot => !AreIdentical(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, context, binary.Span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, context, binary.Span),
            BinaryOperatorSyntax.Equal => AreEqualWithProtocols(left, right, context, binary.Span),
            BinaryOperatorSyntax.NotEqual => !AreEqualWithProtocols(left, right, context, binary.Span),
            _ => throw new InvalidOperationException($"Unknown binary operator: {binary.Operator}")
        };
    }
}
