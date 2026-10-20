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
                // MG08: heap-sized integer literals own their magnitude storage like
                // arithmetic results; inline-range values stay free.
                IntegerLiteralExpressionSyntax integer => OwnHeapInteger(ParseInteger(integer), context.MemoryGovernor, integer.Span),
                ImaginaryLiteralExpressionSyntax imaginary => PyComplex.Create(0, PyNumberOps.ParseFloat(imaginary.ValueText[..^1]), context, imaginary.Span),
                FloatLiteralExpressionSyntax floating => ParseFloat(floating),
                BooleanLiteralExpressionSyntax boolean => boolean.Value,
                NoneLiteralExpressionSyntax => PyNone.Instance,
                EllipsisLiteralExpressionSyntax => PyEllipsis.Instance,
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
                SliceValueExpressionSyntax slice => CreateSliceValue(
                    slice.Start is null ? null : EvaluateExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateExpression(slice.Step, context), slice.Span, context),
                BinaryExpressionSyntax binary => EvaluateBinary(binary, context),
                ChainedComparisonExpressionSyntax chainedComparison => EvaluateChainedComparison(chainedComparison, context),
                UnaryExpressionSyntax unary => EvaluateUnary(unary, context),
                ConditionalExpressionSyntax conditional => EvaluateConditional(conditional, context),
                AssignmentExpressionSyntax assignment => EvaluateAssignmentExpression(assignment, context),
                LambdaExpressionSyntax lambda => CreateLambda(lambda, context),
                _ => throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}")
            };
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
            if (list.HasUnpacking)
            {
                var expanded = new PyList([], context.MemoryGovernor, list.Span);
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var value = RuntimeValue(EvaluateExpression(list.Items[i].Expression, context));
                    AppendListDisplayItem(expanded, value, list.Items[i].IsUnpacking, list.Items[i].Span, list.Span, context);
                }
                return expanded;
            }

            using var preflight5468 = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes(list.Items.Count), list.Span);
            var items = new object[list.Items.Count];
            for (var i = 0; i < list.Items.Count; i++)
            {
                items[i] = RuntimeValue(EvaluateExpression(list.Items[i].Expression, context));
            }

            var created = new PyList(items, context.MemoryGovernor, list.Span);
            return created;
        }

        static PyTuple CreateTupleLiteral(TupleLiteralExpressionSyntax tuple, ExecutionContext context)
        {
            if (!tuple.HasUnpacking)
            {
                var createdTuple = CreateTuple(
                    tuple.Items.Count,
                    i => RuntimeValue(EvaluateExpression(tuple.Items[i].Expression, context)),
                    context,
                    tuple.Span);
                return createdTuple;
            }

            using var storage = context.MemoryGovernor.ReserveTemporary(32, tuple.Span);
            var expanded = new List<object>();
            for (var i = 0; i < tuple.Items.Count; i++)
            {
                var value = RuntimeValue(EvaluateExpression(tuple.Items[i].Expression, context));
                AppendTupleDisplayItem(expanded, storage, value, tuple.Items[i].IsUnpacking, tuple.Items[i].Span, tuple.Span, context);
            }

            return FinishTupleDisplay(expanded, storage, tuple.Span, context);
        }
    }

    private static void AppendListDisplayItem(
        PyList expanded,
        object value,
        bool isUnpacking,
        LythonSourceSpan itemSpan,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        if (!isUnpacking)
        {
            AddListDisplayValue(expanded, value, displaySpan, context);
            return;
        }

        foreach (var item in ToSequence(value, itemSpan, context))
        {
            AddListDisplayValue(expanded, item, displaySpan, context);
        }
    }

    private static void AddListDisplayValue(
        PyList expanded,
        object value,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        expanded.Add(RuntimeValue(value));
        context.CheckExecution(displaySpan);
    }

    private static void AppendTupleDisplayItem(
        List<object> expanded,
        MemoryGovernor.TemporaryMemoryReservation storage,
        object value,
        bool isUnpacking,
        LythonSourceSpan itemSpan,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        if (!isUnpacking)
        {
            AddTupleDisplayValue(expanded, storage, value, displaySpan, context);
            return;
        }

        foreach (var item in ToSequence(value, itemSpan, context))
        {
            AddTupleDisplayValue(expanded, storage, item, displaySpan, context);
        }
    }

    private static void AddTupleDisplayValue(
        List<object> expanded,
        MemoryGovernor.TemporaryMemoryReservation storage,
        object value,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        context.CheckExecution(displaySpan);
        if (expanded.Count == expanded.Capacity)
        {
            var oldCapacity = expanded.Capacity;
            var nextCapacity = oldCapacity == 0 ? 4 : checked(oldCapacity * 2);
            // Both backings coexist during growth; release the old estimate
            // only after replacement. Keep the new backing funded across awaits.
            storage.Grow(16L * nextCapacity, displaySpan);
            expanded.Capacity = nextCapacity;
            storage.Shrink(16L * oldCapacity);
        }
        expanded.Add(RuntimeValue(value));
    }

    private static PyTuple FinishTupleDisplay(List<object> expanded,
        MemoryGovernor.TemporaryMemoryReservation storage, LythonSourceSpan span, ExecutionContext context)
    {
        context.CheckExecution(span);
        if (expanded.Count == 0) return PyTuple.Empty;
        storage.Grow(PyTuple.EstimateApproximateBytes(expanded.Count), span);
        return PyTuple.FromReservedOwnedArray(expanded.ToArray(), context.MemoryGovernor, span, storage);
    }

    private static object EvaluateBinary(BinaryExpressionSyntax binary, ExecutionContext context)
    {
        if (binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue
                : EvaluateExpression(binary.Right, context);
        }

        if (binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return !IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue
                : EvaluateExpression(binary.Right, context);
        }

        var left = EvaluateExpression(binary.Left, context);
        var right = EvaluateExpression(binary.Right, context);
        return EvaluateBinaryOperator(binary.Operator, left, right, context, binary.Span);
    }
}
