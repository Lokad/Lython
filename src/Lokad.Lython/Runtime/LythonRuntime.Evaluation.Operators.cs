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
    private static object EvaluateAssignmentExpression(AssignmentExpressionSyntax assignment, ExecutionContext context)
    {
        var value = EvaluateExpression(assignment.Expression, context);
        StoreName(assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static object EvaluateUnary(UnaryExpressionSyntax unary, ExecutionContext context)
    {
        var operand = EvaluateExpression(unary.Operand, context);
        return EvaluateUnaryOperator(unary.Operator, operand, context, unary.Span);
    }

    private static BigInteger ParseInteger(IntegerLiteralExpressionSyntax integer)
    {
        return PyNumberOps.ParseInteger(integer.ValueText);
    }

    private static double ParseFloat(FloatLiteralExpressionSyntax floating)
    {
        return PyNumberOps.ParseFloat(floating.ValueText);
    }

    private static object EvaluateAdd(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Add(left, right, span), context, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return ConcatStrings(leftText, rightText, context, span);
        }

        if (left is PyList leftList && right is PyList rightList)
        {
            var governor = leftList.OwnerMemoryGovernor ?? rightList.OwnerMemoryGovernor;
            var allocationSpan = leftList.AllocationSpan ?? rightList.AllocationSpan;
            var result = governor is null
                ? new PyList(leftList)
                : new PyList(leftList, governor, allocationSpan);
            result.AddRange(rightList);
            return result;
        }

        if (left is PyTuple leftTuple && right is PyTuple rightTuple)
        {
            var governor = leftTuple.OwnerMemoryGovernor ?? rightTuple.OwnerMemoryGovernor;
            var allocationSpan = leftTuple.AllocationSpan ?? rightTuple.AllocationSpan;
            return governor is null
                ? new PyTuple(leftTuple.Concat(rightTuple))
                : new PyTuple(leftTuple.Concat(rightTuple), governor, allocationSpan);
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => AddCounterCounts(lhs, rhs, span), keepPositiveOnly: true, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Add(left, right, span);
        }

        if (StatisticsModule.TryAddNormalDist(left, right, span, out var normalDistSum))
        {
            return normalDistSum;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '+'.", span);
        }

        return OwnHeapInteger(PyNumberOps.Add(lhs, rhs), context.MemoryGovernor, span);
    }

    internal static object AddRuntimeValues(object left, object right, ExecutionContext context, LythonSourceSpan span)
        => EvaluateAdd(left, right, context, span);

    private static object EvaluateSubtract(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.ExceptWith(rightSet);
            return result;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => SubtractCounterCounts(lhs, rhs, span), keepPositiveOnly: true, span);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Subtract(left, right, span), context, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Subtract(left, right, span);
        }

        if (StatisticsModule.TrySubtractNormalDist(left, right, span, out var normalDistDifference))
        {
            return normalDistDifference;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '-'.", span);
        }

        return OwnHeapInteger(PyNumberOps.Subtract(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateMultiply(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Multiply(left, right, span), context, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && TryRepeatCount(right, out var rightCount))
        {
            return RepeatString(leftText, rightCount, context, span);
        }

        if (PyStringOps.TryAsString(right, out var rightText) && TryRepeatCount(left, out var leftCount))
        {
            return RepeatString(rightText, leftCount, context, span);
        }

        if (left is PyList leftList && TryRepeatCount(right, out var rightRepeatCount))
        {
            return RepeatList(leftList, rightRepeatCount, context, span);
        }

        if (right is PyList rightList && TryRepeatCount(left, out var leftRepeatCount))
        {
            return RepeatList(rightList, leftRepeatCount, context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Multiply(left, right, span);
        }

        if (StatisticsModule.TryMultiplyNormalDist(left, right, span, out var normalDistProduct))
        {
            return normalDistProduct;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '*'.", span);
        }

        return OwnHeapInteger(PyNumberOps.Multiply(lhs, rhs), context.MemoryGovernor, span);
    }

    private static bool TryRepeatCount(object value, out BigInteger count)
    {
        if (value is BigInteger integer)
        {
            count = integer;
            return true;
        }

        if (value is bool flag)
        {
            count = flag ? BigInteger.One : BigInteger.Zero;
            return true;
        }

        count = default;
        return false;
    }

    private static object EvaluateDivide(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Divide(left, right, span), context, span);
        }

        if (left is PyPath leftPath)
        {
            if (right is PyPath rightPath)
            {
                return new PyPath(PathOps.Join(leftPath.Value, rightPath.Value));
            }

            if (PyStringOps.TryAsString(right, out var rightText))
            {
                return new PyPath(PathOps.Join(leftPath.Value, rightText));
            }
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Divide(left, right, span);
        }

        if (StatisticsModule.TryDivideNormalDist(left, right, span, out var normalDistQuotient))
        {
            return normalDistQuotient;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '/'.", span);
        }

        try
        {
            return PyNumberOps.TrueDivide(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "division by zero", span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateFloorDivide(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.FloorDivide(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '//'.", span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.FloorDivide(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "integer division or modulo by zero", span);
        }
    }

    private static object EvaluateModulo(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyString template)
        {
            return FormatPercentString(template, right, context, span);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Modulo(left, right, span), context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Modulo(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '%'.", span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.Modulo(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "integer division or modulo by zero", span);
        }
    }

    private static object EvaluatePower(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Power(left, right, span), context, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }

        try
        {
            if (lhs.IsZero && (rhs.IsFloat ? rhs.Floating < 0 : rhs.Integer < BigInteger.Zero))
            {
                throw new LythonRuntimeException("ZeroDivisionError", "0.0 cannot be raised to a negative power", span);
            }

            var leftValue = lhs.IsFloat ? lhs.Floating : (double)lhs.Integer;
            var rightValue = rhs.IsFloat ? rhs.Floating : (double)rhs.Integer;
            if (leftValue < 0 && double.IsFinite(rightValue) && rightValue != Math.Truncate(rightValue))
            {
                throw new LythonRuntimeException("TypeError", "complex results are not supported by Lython", span);
            }

            GuardIntegerPower(lhs, rhs, context.MemoryGovernor, span);
            return OwnHeapInteger(PyNumberOps.Power(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }
    }
}
