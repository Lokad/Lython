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
    private static void GuardIntegerPower(PyNumber lhs, PyNumber rhs, ExecutionContext context, LythonSourceSpan span)
    {
        if (lhs.IsFloat || rhs.IsFloat || rhs.Integer < BigInteger.Zero)
        {
            return;
        }

        var baseBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs.Integer);
        var exponent = rhs.Integer;
        var resultBits = baseBits switch
        {
            0 => 0,
            1 => 1,
            _ when exponent > long.MaxValue => long.MaxValue,
            _ => RuntimeMemoryEstimates.SaturatingMultiply(baseBits, (long)exponent)
        };

        GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), context, span);
    }

    private static void GuardIntegerLeftShift(BigInteger lhs, BigInteger rhs, ExecutionContext context, LythonSourceSpan span)
    {
        if (rhs < BigInteger.Zero)
        {
            return;
        }

        var lhsBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs);
        var shiftBits = rhs > long.MaxValue ? long.MaxValue : (long)rhs;
        var resultBits = RuntimeMemoryEstimates.SaturatingAdd(lhsBits, shiftBits);
        GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), context, span);
    }

    private static void GuardIntegerResultBytes(long estimatedBytes, ExecutionContext context, LythonSourceSpan span)
    {
        context.MemoryGovernor.EnsureCanReserve(estimatedBytes, span);
    }

    private static object EvaluateBitwiseOr(object left, object right, LythonSourceSpan span)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean | rightBoolean;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) >= 0 ? lhs : rhs, keepPositiveOnly: true, span);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.UnionWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '|'.", span);
        }

        return PyNumberOps.BitwiseOr(lhs, rhs);
    }

    private static object EvaluateBitwiseXor(object left, object right, LythonSourceSpan span)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean ^ rightBoolean;
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.SymmetricExceptWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '^'.", span);
        }

        return PyNumberOps.BitwiseXor(lhs, rhs);
    }

    private static object EvaluateBitwiseAnd(object left, object right, LythonSourceSpan span)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean & rightBoolean;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) <= 0 ? lhs : rhs, keepPositiveOnly: true, span);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.IntersectWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '&'.", span);
        }

        return PyNumberOps.BitwiseAnd(lhs, rhs);
    }

    private static object EvaluateLeftShift(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '<<'.", span);
        }

        try
        {
            GuardIntegerLeftShift(lhs, rhs, context, span);
            return PyNumberOps.LeftShift(lhs, rhs);
        }
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '<<'.", span);
        }
    }

    private static object EvaluateRightShift(object left, object right, LythonSourceSpan span)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }

        try
        {
            return PyNumberOps.RightShift(lhs, rhs);
        }
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }
    }

    private static object EvaluateUnaryPlus(object operand, LythonSourceSpan span)
    {
        if (operand is PyCounter positiveCounter)
        {
            return BuildCounterUnaryResult(positiveCounter, count => count, keepPositiveOnly: true, span);
        }

        if (operand is PyDecimal)
        {
            return operand;
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: false, out var positiveNormalDist))
        {
            return positiveNormalDist;
        }

        if (!PyNumberOps.TryAsNumber(operand, out _))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span);
        }

        return operand.RequireNotNull();
    }

    private static object EvaluateUnaryMinus(object operand, LythonSourceSpan span)
    {
        if (operand is PyCounter negativeCounter)
        {
            return BuildCounterUnaryResult(negativeCounter, count => NegateCounterCount(count, span), keepPositiveOnly: true, span);
        }

        if (operand is PyTimedelta)
        {
            return PyDateTimeOps.Negate(operand, span);
        }

        if (operand is PyDecimal decimalValue)
        {
            return new PyDecimal(-decimalValue.Value, decimalValue.Exponent);
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: true, out var negativeNormalDist))
        {
            return negativeNormalDist;
        }

        if (!PyNumberOps.TryAsNumber(operand, out var numeric))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span);
        }

        return PyNumberOps.Negate(numeric);
    }

    private static PyCounter BuildCounterUnaryResult(
        PyCounter source,
        Func<object, object> transform,
        bool keepPositiveOnly,
        LythonSourceSpan span)
    {
        var result = CreateCounterResult(source, null, span);
        foreach (var pair in source.Items)
        {
            var count = transform(ExpectCounterCount(pair.Value, span));
            if (keepPositiveOnly && CompareCounterCounts(count, BigInteger.Zero, span) <= 0)
            {
                continue;
            }

            result.SetItem(pair.Key, count);
        }

        return result;
    }

    private static PyCounter BuildCounterBinaryResult(
        PyCounter left,
        PyCounter right,
        Func<object, object, object> combine,
        bool keepPositiveOnly,
        LythonSourceSpan span)
    {
        var result = CreateCounterResult(left, right, span);
        var seen = new HashSet<object>(PyValueComparer.Instance);
        foreach (var pair in left.Items)
        {
            seen.Add(pair.Key);
            AddResult(pair.Key, pair.Value, right.GetCount(pair.Key));
        }

        foreach (var pair in right.Items)
        {
            if (seen.Add(pair.Key))
            {
                AddResult(pair.Key, BigInteger.Zero, pair.Value);
            }
        }

        return result;

        void AddResult(object key, object leftCount, object rightCount)
        {
            var count = combine(ExpectCounterCount(leftCount, span), ExpectCounterCount(rightCount, span));
            if (!keepPositiveOnly || CompareCounterCounts(count, BigInteger.Zero, span) > 0)
            {
                result.SetItem(key, count);
            }
        }
    }

    internal static object AddCounterCounts(object left, object right, LythonSourceSpan span)
    {
        left = ExpectCounterCount(left, span);
        right = ExpectCounterCount(right, span);
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Add(left, right, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return PyNumberOps.Add(lhs, rhs);
    }

    private static object SubtractCounterCounts(object left, object right, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Subtract(left, right, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return PyNumberOps.Subtract(lhs, rhs);
    }

    private static object NegateCounterCount(object value, LythonSourceSpan span)
        => value is PyDecimal decimalValue
            ? new PyDecimal(-decimalValue.Value, decimalValue.Exponent)
            : PyNumberOps.TryAsNumber(value, out var number)
                ? PyNumberOps.Negate(number)
                : throw new LythonRuntimeException("TypeError", "Counter mapping values must be numeric.", span);

    private static int CompareCounterCounts(object left, object right, LythonSourceSpan span)
        => PyComparison.Compare(left, right, span);

    private static PyCounter CreateCounterResult(PyCounter left, PyCounter? right, LythonSourceSpan span)
    {
        var governor = left.OwnerMemoryGovernor ?? right?.OwnerMemoryGovernor;
        var allocationSpan = left.AllocationSpan ?? right?.AllocationSpan ?? span;
        return governor is null ? new PyCounter() : new PyCounter(governor, allocationSpan);
    }

    private static object EvaluateBitwiseNot(object operand, LythonSourceSpan span)
    {
        if (!PyNumberOps.TryAsInteger(operand, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not an integer.", span);
        }

        return PyNumberOps.BitwiseNot(integer);
    }

    private static bool TryGetNumericOperands(object left, object right, out PyNumber lhs, out PyNumber rhs)
    {
        if (PyNumberOps.TryAsNumber(left, out lhs) && PyNumberOps.TryAsNumber(right, out rhs))
        {
            return true;
        }

        lhs = default;
        rhs = default;
        return false;
    }

    private static bool TryGetIntegerOperands(object left, object right, out BigInteger lhs, out BigInteger rhs)
    {
        if (PyNumberOps.TryAsInteger(left, out lhs) && PyNumberOps.TryAsInteger(right, out rhs))
        {
            return true;
        }

        lhs = default;
        rhs = default;
        return false;
    }

    private static PyString ConcatStrings(PyString left, PyString right, ExecutionContext context, LythonSourceSpan span)
    {
        var resultLength = RuntimeMemoryEstimates.SaturatingAdd(left.Length, right.Length);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        return left.Concat(right, context.MemoryGovernor, span);
    }

    private static PyString RepeatString(PyString text, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return PyString.Empty;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "String repetition is too large.", span);
        }

        var resultLength = RuntimeMemoryEstimates.SaturatingMultiply(text.Length, (long)count);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        return text.Repeat((int)count, context.MemoryGovernor, span);
    }

    private static PyList RepeatList(PyList list, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        var repeatCount = ToListRepeatCount(count, span);
        if (repeatCount == 0 || list.Count == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        var totalLength = (long)list.Count * repeatCount;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        for (var i = 0; i < repeatCount; i++)
        {
            result.AddRange(list);
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static int ToListRepeatCount(BigInteger count, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return 0;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        return (int)count;
    }

    private static object EvaluateDictLiteral(DictLiteralExpressionSyntax dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is DictionaryUnpackingItemSyntax unpacking)
            {
                var mapping = RuntimeValue(EvaluateExpression(unpacking.Mapping, context));
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Span, context.MemoryGovernor), RuntimeValue(pair.Value));
                    context.ObserveCollectionCount(result.Count, dict.Span);
                }

                continue;
            }

            var keyValue = (DictionaryKeyValueItemSyntax)item;
            result.SetItem(
                ValidateDictionaryKey(EvaluateExpression(keyValue.Key, context), keyValue.Key.Span, context.MemoryGovernor),
                RuntimeValue(EvaluateExpression(keyValue.Value, context)));
            context.ObserveCollectionCount(result.Count, dict.Span);
        }

        return result;
    }

    private static object EvaluateSetLiteral(SetLiteralExpressionSyntax set, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, set.Span);
        for (var i = 0; i < set.Items.Count; i++)
        {
            var value = RuntimeValue(EvaluateExpression(set.Items[i], context));
            if (!set.UnpackingFlags[i])
            {
                result.Add(ValidateSetItem(value, set.Items[i].Span, context.MemoryGovernor));
                context.ObserveCollectionCount(result.Count, set.Span);
                continue;
            }

            foreach (var item in ToSequence(value, set.Items[i].Span, context))
            {
                result.Add(ValidateSetItem(RuntimeValue(item), set.Items[i].Span, context.MemoryGovernor));
                context.ObserveCollectionCount(result.Count, set.Span);
            }
        }

        return result;
    }

    private static IEnumerable<KeyValuePair<object, object>> EnumerateMappingItems(
        object mapping,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        switch (mapping)
        {
            case PyDict dict:
                return dict.Items;
            case PyDefaultDict defaultDict:
                return defaultDict.Items;
            case PyCounter counter:
                return counter.Items;
            case PyChainMap chainMap:
                return chainMap.Iterate().Select(key =>
                    new KeyValuePair<object, object>(key, chainMap.GetSubscript(key, span)));
            case PyInstance instance:
                if (!TryResolveRuntimeMember(instance, "keys", context, span, out var keysMember))
                {
                    break;
                }

                var keys = InvokeCallableTarget(keysMember, span, span, context, () => []);
                return ToSequence(keys, span, context).Select(key =>
                    new KeyValuePair<object, object>(key, GetUserItem(instance, key, context, span)));
        }

        throw new LythonRuntimeException("TypeError", "Object is not a mapping.", span);
    }

    private static object EvaluateFormattedString(FormattedStringExpressionSyntax formatted, ExecutionContext context)
        => EvaluateFormattedStringParts(formatted.Parts, context, formatted.Span);

    private static PyString EvaluateFormattedStringParts(
        IReadOnlyList<FormattedStringPartSyntax> parts,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        foreach (var part in parts)
        {
            switch (part)
            {
                case FormattedStringTextPartSyntax text:
                    builder.AppendString(text.Text);
                    break;
                case FormattedStringExpressionPartSyntax expression:
                    var formatSpecifier = expression.FormatSpecifierParts is null
                        ? expression.FormatSpecifier
                        : EvaluateFormattedStringParts(expression.FormatSpecifierParts, context, span).AsString();
                    builder.Append(FormatInterpolatedStringPart(
                        EvaluateExpression(expression.Expression, context),
                        expression.Conversion,
                        formatSpecifier,
                        context,
                        span));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown formatted string part: {part.GetType().Name}");
            }
        }

        var value = builder.ToPyStringAndRelease();
        context.ObserveString(value, span);
        return value;
    }

    private static PyString ToInterpolatedPyString(object value, ExecutionContext context)
        => PyRendering.ToInterpolatedPyString(value, new PyRenderingContext(context));

    private static string ToInterpolatedString(object value, ExecutionContext context)
        => PyRendering.ToInterpolatedString(value, new PyRenderingContext(context));

    private static PyString ToPythonPyString(object value, ExecutionContext context)
        => PyRendering.ToPythonPyString(value, new PyRenderingContext(context));

    private static string ToPythonString(object value, ExecutionContext context)
        => PyRendering.ToPythonString(value, new PyRenderingContext(context));

    private static PyString ToReprPyString(object value, ExecutionContext context)
        => PyRendering.ToReprPyString(value, new PyRenderingContext(context));

    private static PyString FormatInterpolatedStringPart(
        object value,
        char? conversion,
        string? formatSpecifier,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var converted = conversion switch
        {
            null => null,
            's' => ToInterpolatedPyString(value, context),
            'r' or 'a' => ToReprPyString(value, context),
            _ => throw new LythonRuntimeException("ValueError", $"Unknown conversion specifier '!{conversion}'.", span)
        };

        if (string.IsNullOrEmpty(formatSpecifier))
        {
            return converted ?? ToInterpolatedPyString(value, context);
        }

        var formatted = FormatInterpolatedStringValue(
            converted ?? value,
            formatSpecifier,
            context,
            span);
        return PyString.FromString(formatted, context.MemoryGovernor, span);
    }

    private static string FormatInterpolatedStringValue(
        object value,
        string formatSpecifier,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (value is PyInstance instance &&
            instance.TryGetAttribute("__format__", context, span, out var formatMember) &&
            formatMember is ICallable formatCallable)
        {
            var formatted = formatCallable.Invoke(
                [CallArgumentValue.Positional(PyString.FromString(formatSpecifier, context.MemoryGovernor, span))],
                span,
                context);
            if (!PyStringOps.TryAsString(formatted, out var formattedText))
            {
                throw new LythonRuntimeException("TypeError", "__format__ returned non-string", span);
            }

            return formattedText.AsString();
        }

        if (value is PyDate or PyTime or PyDateTime)
        {
            return PyDateTimeOps.FormatValue(value, PyString.FromString(formatSpecifier), span).AsString();
        }

        var spec = ParseInterpolatedFormatSpecifier(formatSpecifier, span);
        if (TryFormatNumericValue(value, spec, context, span, out var numericText, out var numericPrefixLength))
        {
            return ApplyInterpolatedFormatPadding(numericText, spec, numericPrefixLength, numeric: true, span);
        }

        if (RequiresNumericFormat(spec))
        {
            throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires a numeric value.", span);
        }

        if (spec.Sign is not null || spec.Alternate || spec.Grouping is not null || spec.Align == '=')
        {
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{formatSpecifier}' for string value.", span);
        }

        var text = value switch
        {
            PyString pyString => pyString.AsString(),
            string raw => raw,
            _ => ToInterpolatedString(value, context)
        };

        if (spec.Type is not null and not 's')
        {
            throw new LythonRuntimeException("ValueError", $"Unknown format code '{spec.Type}' for string value.", span);
        }

        if (spec.Precision is { } precision)
        {
            text = text.Length <= precision ? text : text[..precision];
        }

        return ApplyInterpolatedFormatPadding(text, spec, numericPrefixLength: 0, numeric: false, span);
    }

    private static bool TryFormatNumericValue(
        object value,
        InterpolatedFormatSpecifier spec,
        ExecutionContext context,
        LythonSourceSpan span,
        out string text,
        out int numericPrefixLength)
    {
        text = string.Empty;
        numericPrefixLength = 0;

        if (TryGetIntegerFormatValue(value, out var integer))
        {
            if (spec.Type is 'f' or 'F' or 'g' or 'G' or '%')
            {
                return TryFormatFloatingValue((double)integer, spec, span, out text, out numericPrefixLength);
            }

            text = FormatIntegerValue(integer, spec, span, out numericPrefixLength);
            return true;
        }

        if (value is double floating)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires an integer value.", span);
            }

            return TryFormatFloatingValue(floating, spec, span, out text, out numericPrefixLength);
        }

        if (value is PyDecimal decimalValue)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires an integer value.", span);
            }

            return TryFormatFloatingValue((double)decimalValue.Value, spec, span, out text, out numericPrefixLength);
        }

        _ = context;
        return false;
    }

    private static bool TryGetIntegerFormatValue(object value, out BigInteger integer)
    {
        switch (value)
        {
            case BigInteger bigInteger:
                integer = bigInteger;
                return true;
            case int intValue:
                integer = new BigInteger(intValue);
                return true;
            case bool boolValue:
                integer = boolValue ? BigInteger.One : BigInteger.Zero;
                return true;
            default:
                integer = BigInteger.Zero;
                return false;
        }
    }

    private static string FormatIntegerValue(
        BigInteger value,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        out int numericPrefixLength)
    {
        if (spec.Precision is not null)
        {
            throw new LythonRuntimeException("ValueError", "Precision is not allowed in integer format specifiers.", span);
        }

        var type = spec.Type ?? 'd';
        var negative = value.Sign < 0;
        var magnitude = BigInteger.Abs(value);
        string digits;
        string prefix;
        switch (type)
        {
            case 'd':
            case 'n':
                digits = magnitude.ToString(CultureInfo.InvariantCulture);
                prefix = string.Empty;
                break;
            case 'b':
                digits = ToUnsignedBaseString(magnitude, 2, upper: false);
                prefix = spec.Alternate ? "0b" : string.Empty;
                break;
            case 'o':
                digits = ToUnsignedBaseString(magnitude, 8, upper: false);
                prefix = spec.Alternate ? "0o" : string.Empty;
                break;
            case 'x':
                digits = ToUnsignedBaseString(magnitude, 16, upper: false);
                prefix = spec.Alternate ? "0x" : string.Empty;
                break;
            case 'X':
                digits = ToUnsignedBaseString(magnitude, 16, upper: true);
                prefix = spec.Alternate ? "0X" : string.Empty;
                break;
            default:
                throw new LythonRuntimeException("ValueError", $"Unknown integer format code '{type}'.", span);
        }

        if (spec.Grouping is { } grouping)
        {
            if (type is not ('d' or 'n'))
            {
                throw new LythonRuntimeException("ValueError", "Grouping is only supported for decimal integer formatting.", span);
            }

            digits = GroupDigits(digits, grouping);
        }

        var sign = FormatNumericSign(negative, spec.Sign);
        numericPrefixLength = sign.Length + prefix.Length;
        return sign + prefix + digits;
    }

    private static bool TryFormatFloatingValue(
        double value,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        out string text,
        out int numericPrefixLength)
    {
        var type = spec.Type;
        var precision = spec.Precision;
        if (type is 's' or 'b' or 'o' or 'x' or 'X' or 'd' or 'n')
        {
            text = string.Empty;
            numericPrefixLength = 0;
            return false;
        }

        if (spec.Alternate)
        {
            throw new LythonRuntimeException("ValueError", "Alternate floating-point formatting is not supported.", span);
        }

        var formatted = type switch
        {
            null when precision is null => value.ToString(CultureInfo.InvariantCulture),
            null => value.ToString("G" + precision.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'f' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'F' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'g' => value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'G' => value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            '%' => (value * 100.0).ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%",
            _ => throw new LythonRuntimeException("ValueError", $"Unknown floating-point format code '{type}'.", span)
        };

        if (type == 'F')
        {
            formatted = formatted.ToUpperInvariant();
        }

        if (spec.Grouping is { } grouping)
        {
            formatted = GroupFloatingDigits(formatted, grouping);
        }

        text = ApplyNumericSign(formatted, spec.Sign);
        numericPrefixLength = GetNumericPrefixLength(text);
        return true;
    }

    private static InterpolatedFormatSpecifier ParseInterpolatedFormatSpecifier(string text, LythonSourceSpan span)
    {
        var index = 0;
        char? fill = null;
        char? align = null;

        if (index + 1 < text.Length && IsFormatAlign(text[index + 1]))
        {
            fill = text[index];
            align = text[index + 1];
            index += 2;
        }
        else if (index < text.Length && IsFormatAlign(text[index]))
        {
            align = text[index];
            index++;
        }

        char? sign = null;
        if (index < text.Length && text[index] is '+' or '-' or ' ')
        {
            sign = text[index++];
        }

        var alternate = false;
        if (index < text.Length && text[index] == '#')
        {
            alternate = true;
            index++;
        }

        var zeroPad = false;
        if (index < text.Length && text[index] == '0')
        {
            zeroPad = true;
            index++;
        }

        int? width = null;
        var widthStart = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        if (index > widthStart)
        {
            width = int.Parse(text[widthStart..index], CultureInfo.InvariantCulture);
        }

        char? grouping = null;
        if (index < text.Length && text[index] is ',' or '_')
        {
            grouping = text[index++];
        }

        int? precision = null;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            var precisionStart = index;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
            }

            if (precisionStart == index)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}'.", span);
            }

            precision = int.Parse(text[precisionStart..index], CultureInfo.InvariantCulture);
        }

        char? type = null;
        if (index < text.Length)
        {
            type = text[index++];
        }

        if (index != text.Length)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}'.", span);
        }

        return new InterpolatedFormatSpecifier(fill, align, sign, alternate, zeroPad, width, grouping, precision, type);
    }

    private static bool RequiresNumericFormat(InterpolatedFormatSpecifier spec)
        => spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n' or 'f' or 'F' or 'g' or 'G' or '%';

    private static bool IsFormatAlign(char value)
        => value is '<' or '>' or '^' or '=';

    private static string ApplyInterpolatedFormatPadding(
        string text,
        InterpolatedFormatSpecifier spec,
        int numericPrefixLength,
        bool numeric,
        LythonSourceSpan span)
    {
        if (spec.Width is not { } width || text.Length >= width)
        {
            return text;
        }

        var align = spec.Align ?? (numeric ? '>' : '<');
        var fill = spec.Fill ?? (spec.ZeroPad ? '0' : ' ');
        if (numeric && spec.ZeroPad && spec.Align is null)
        {
            align = '=';
            fill = '0';
        }

        if (align == '=' && !numeric)
        {
            throw new LythonRuntimeException("ValueError", "'=' alignment requires a numeric value.", span);
        }

        var padding = width - text.Length;
        var padText = new string(fill, padding);
        return align switch
        {
            '<' => text + padText,
            '>' => padText + text,
            '^' => new string(fill, padding / 2) + text + new string(fill, padding - padding / 2),
            '=' => text[..numericPrefixLength] + padText + text[numericPrefixLength..],
            _ => throw new LythonRuntimeException("ValueError", $"Unknown alignment option '{align}'.", span)
        };
    }

    private static string FormatNumericSign(bool negative, char? sign)
        => negative
            ? "-"
            : sign switch
            {
                '+' => "+",
                ' ' => " ",
                _ => string.Empty
            };

    private static string ApplyNumericSign(string text, char? sign)
    {
        if (text.StartsWith("-", StringComparison.Ordinal) ||
            text.StartsWith("+", StringComparison.Ordinal) ||
            text.StartsWith(" ", StringComparison.Ordinal))
        {
            return text;
        }

        return sign switch
        {
            '+' => "+" + text,
            ' ' => " " + text,
            _ => text
        };
    }

    private static int GetNumericPrefixLength(string text)
        => text.Length > 0 && text[0] is '-' or '+' or ' ' ? 1 : 0;

    private static string ToUnsignedBaseString(BigInteger value, int radix, bool upper)
    {
        if (value.IsZero)
        {
            return "0";
        }

        const string lowerDigits = "0123456789abcdef";
        const string upperDigits = "0123456789ABCDEF";
        var digits = upper ? upperDigits : lowerDigits;
        var builder = new StringBuilder();
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, radix, out var remainder);
            builder.Append(digits[(int)remainder]);
        }

        for (var left = 0; left < builder.Length / 2; left++)
        {
            var right = builder.Length - left - 1;
            (builder[left], builder[right]) = (builder[right], builder[left]);
        }

        return builder.ToString();
    }

    private static string GroupDigits(string digits, char separator)
    {
        var builder = new StringBuilder(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                builder.Append(separator);
            }

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    private static string GroupFloatingDigits(string text, char separator)
    {
        var signLength = GetNumericPrefixLength(text);
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissaEnd = exponentIndex >= 0 ? exponentIndex : text.Length;
        var decimalIndex = text.IndexOf('.');
        if (decimalIndex < 0 || decimalIndex > mantissaEnd)
        {
            decimalIndex = mantissaEnd;
        }

        var grouped = GroupDigits(text[signLength..decimalIndex], separator);
        return text[..signLength] + grouped + text[decimalIndex..];
    }

    private sealed record InterpolatedFormatSpecifier(
        char? Fill,
        char? Align,
        char? Sign,
        bool Alternate,
        bool ZeroPad,
        int? Width,
        char? Grouping,
        int? Precision,
        char? Type);

}
