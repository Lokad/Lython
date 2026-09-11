using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class ListMembers
    {
        public static bool TryGetMember(PyList list, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "append" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.append(value) expects one argument.", span);
                    }

                    list.AttachMemoryGovernor(context.MemoryGovernor, span);
                    list.Add(arguments[0]);
                    context.ObserveCollectionCount(list.Count, span);
                    return PyNone.Instance;
                }, "list.append", ["value"]),
                "extend" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.extend(iterable) expects one argument.", span);
                    }

                    list.AttachMemoryGovernor(context.MemoryGovernor, span);
                    list.AddRange(ToSequence(arguments[0], span, context), context, span);
                    context.ObserveCollectionCount(list.Count, span);
                    return PyNone.Instance;
                }, "list.extend", ["iterable"]),
                "index" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "list.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, list.Count, 0, "list.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, list.Count, list.Count, "list.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                    for (var i = start; i < stop; i++)
                    {
                        if (AreEqual(list[i], arguments[0]))
                        {
                            return new BigInteger(i);
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "list.index(value): value is not in list", span);
                }, "list.index", ["value", "start", "stop"], 1),
                "count" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.count(value) expects one argument.", span);
                    }

                    var count = 0;
                    foreach (var item in list)
                    {
                        if (AreEqual(item, arguments[0]))
                        {
                            count++;
                        }
                    }

                    return new BigInteger(count);
                }, "list.count", ["value"]),
                "insert" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "list.insert(index, value) expects two arguments.", span);
                    }

                    var index = ExpectListInsertIndex(arguments[0], span);
                    list.AttachMemoryGovernor(context.MemoryGovernor, span);
                    list.Insert(index, arguments[1]);
                    context.ObserveCollectionCount(list.Count, span);
                    return PyNone.Instance;
                }, "list.insert", ["index", "value"]),
                "remove" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.remove(value) expects one argument.", span);
                    }

                    for (var i = 0; i < list.Count; i++)
                    {
                        if (AreEqual(list[i], arguments[0]))
                        {
                            list.RemoveAt(i);
                            return PyNone.Instance;
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "list.remove(value): value is not in list", span);
                }, "list.remove", ["value"]),
                "pop" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.pop([index]) expects zero or one argument.", span);
                    }

                    if (list.Count == 0)
                    {
                        throw new LythonRuntimeException("IndexError", "pop from empty list", span);
                    }

                    var index = arguments.Length == 0
                        ? list.Count - 1
                        : PyIndexing.NormalizeIndex(arguments[0], list.Count, span);
                    var item = list[index];
                    list.RemoveAt(index);
                    return item;
                }, "list.pop", ["index"], 0),
                "reverse" => BoundCallable.CreateNoArguments(list, "list.reverse", static (receiver, _, _) =>
                {
                    receiver.Reverse();
                    return PyNone.Instance;
                }),
                "sort" => BoundCallable.Create(
                    (arguments, span, context) => SortList(list, arguments, span, context),
                    LythonCallableSignature.Create("list.sort", ["key", "reverse"], requiredCount: 0, maximumPositionalArgumentCount: 0),
                    (arguments, span, context) => SortListAsync(list, arguments, span, context)),
                "copy" => BoundCallable.CreateNoArguments(
                    list,
                    "list.copy",
                    static (receiver, span, context) => new PyList(receiver, context.MemoryGovernor, span)),
                "clear" => BoundCallable.CreateNoArguments(list, "list.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object SortList(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var keyArgument = arguments.Length >= 1 ? arguments[0] : null;

            var reverse = false;
            if (arguments.Length >= 2)
            {
                reverse = IsTruthy(arguments[1]);
            }

            // R13: an invalid key only fails when the list is non-empty and the
            // key would actually be called.
            using var sorted = SortItems(list, keyArgument, reverse, span, context, "list.sort(..., key=...) expects a callable or None.");
            list.ReplaceAll(sorted);
            return PyNone.Instance;
        }

        private static async ValueTask<object> SortListAsync(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var keyArgument = arguments.Length >= 1 ? arguments[0] : null;

            var reverse = false;
            if (arguments.Length >= 2)
            {
                reverse = IsTruthy(arguments[1]);
            }

            // R13: an invalid key only fails when the list is non-empty and the
            // key would actually be called.
            using var sorted = await SortItemsAsync(list, keyArgument, reverse, span, context, "list.sort(..., key=...) expects a callable or None.").ConfigureAwait(false);
            list.ReplaceAll(sorted);
            return PyNone.Instance;
        }

        private static int ExpectListInsertIndex(object value, LythonSourceSpan span)
        {
            var integer = ExpectInteger(value, "list.insert(index, value) expects an integer index.", span);
            if (integer < int.MinValue)
            {
                return int.MinValue;
            }

            if (integer > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)integer;
        }
    }

    // Range membership is arithmetic (never enumerated): integer-likes by
    // value plus integral doubles by truncation, everything else absent.
    internal static bool TryRangeIndex(PyRange range, BigInteger candidate, out BigInteger position)
    {
        position = BigInteger.Zero;
        var step = BigInteger.Abs(range.Step);
        if (step.IsZero)
        {
            return false;
        }

        if (range.Step > 0)
        {
            if (candidate < range.Start || candidate >= range.Stop)
            {
                return false;
            }
        }
        else if (candidate > range.Start || candidate <= range.Stop)
        {
            return false;
        }

        var offset = range.Step > 0 ? candidate - range.Start : range.Start - candidate;
        if (offset % step != BigInteger.Zero)
        {
            return false;
        }

        position = offset / step;
        return true;
    }

    internal static bool RangeContains(PyRange range, object candidate)
    {
        BigInteger number;
        switch (candidate)
        {
            case BigInteger big:
                number = big;
                break;
            case int small:
                number = new BigInteger(small);
                break;
            case bool flag:
                number = flag ? BigInteger.One : BigInteger.Zero;
                break;
            case double floating when floating == Math.Truncate(floating) && !double.IsInfinity(floating):
                number = new BigInteger(floating);
                break;
            default:
                return false;
        }

        return TryRangeIndex(range, number, out _);
    }

    internal static class TupleMembers
    {
        public static bool TryGetMember(PyTuple tuple, string name, [MaybeNullWhen(false)] out object value)
            => TryGetMember(name, tuple.Count, index => tuple[index], out value);

        public static bool TryGetMember(PyNamedTupleObject namedTuple, string name, [MaybeNullWhen(false)] out object value)
            => TryGetMember(name, namedTuple.Count, namedTuple.GetItem, out value);

        public static bool TryGetMember(PyTypingNamedTupleObject namedTuple, string name, [MaybeNullWhen(false)] out object value)
            => TryGetMember(name, namedTuple.Count, namedTuple.GetItem, out value);

        private static bool TryGetMember(string name, int count, Func<int, object> getItem, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "index" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, count, 0, "tuple.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, count, count, "tuple.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                    for (var i = start; i < stop; i++)
                    {
                        if (AreEqual(getItem(i), arguments[0]))
                        {
                            return new BigInteger(i);
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "tuple.index(value): value is not in tuple", span);
                }, "tuple.index", ["value", "start", "stop"], 1),
                "count" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.count(value) expects one argument.", span);
                    }

                    var itemCount = 0;
                    for (var i = 0; i < count; i++)
                    {
                        if (AreEqual(getItem(i), arguments[0]))
                        {
                            itemCount++;
                        }
                    }

                    return new BigInteger(itemCount);
                }, "tuple.count", ["value"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class IntMembers
    {
        public static bool TryGetMember(object receiver, string name, [MaybeNullWhen(false)] out object value)
        {
            var integer = receiver switch
            {
                BigInteger big => big,
                int small => new BigInteger(small),
                bool flag => flag ? BigInteger.One : BigInteger.Zero,
                _ => (BigInteger?)null,
            };

            if (integer is null)
            {
                value = PyNone.Instance;
                return false;
            }

            value = name switch
            {
                "bit_length" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.bit_length() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return new BigInteger(BigInteger.Abs(integer.Value).GetBitLength());
                }, "int.bit_length"),
                "bit_count" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.bit_count() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    var remaining = BigInteger.Abs(integer.Value);
                    var ones = 0;
                    while (remaining != BigInteger.Zero)
                    {
                        if (!remaining.IsEven)
                        {
                            ones++;
                        }

                        remaining >>= 1;
                    }

                    return new BigInteger(ones);
                }, "int.bit_count"),
                "conjugate" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.conjugate() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return integer.Value;
                }, "int.conjugate"),
                "as_integer_ratio" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.as_integer_ratio() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return new PyTuple([integer.Value, BigInteger.One], context.MemoryGovernor, span);
                }, "int.as_integer_ratio"),
                "is_integer" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.is_integer() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return true;
                }, "int.is_integer"),
                "numerator" => integer.Value,
                "denominator" => BigInteger.One,
                "real" => integer.Value,
                "imag" => BigInteger.Zero,
                "to_bytes" => new RawBoundCallable((arguments, span, context) => IntToBytes(integer.Value, arguments, span, context)) { BoundName = "int.to_bytes", BoundReceiver = receiver },
                "from_bytes" => receiver is bool
                    ? new BuiltinTypeMethod("bool", "from_bytes", bindsOwner: true, BoolFromBytes)
                    : new BuiltinTypeMethod("int", "from_bytes", bindsOwner: true, IntFromBytes),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class FloatMembers
    {
        public static bool TryGetMember(double number, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "conjugate" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.conjugate() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return number;
                }, "float.conjugate"),
                "as_integer_ratio" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.as_integer_ratio() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return FloatAsIntegerRatio(number, span);
                }, "float.as_integer_ratio"),
                "is_integer" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.is_integer() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return !double.IsInfinity(number) && !double.IsNaN(number) && number == Math.Truncate(number);
                }, "float.is_integer"),
                "real" => number,
                "imag" => 0.0,
                "hex" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.hex() takes no arguments (" + arguments.Length + " given)", span);
                    }

                    return PyString.FromString(FloatToHex(number));
                }, "float.hex"),
                "fromhex" => new BuiltinTypeMethod("float", "fromhex", bindsOwner: true, FloatFromHex),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        // Exact binary ratio like CPython (lowest terms, since the
        // denominator starts as a power of two).
        private static object FloatAsIntegerRatio(double value, LythonSourceSpan span)
        {
            if (double.IsInfinity(value))
            {
                throw new LythonRuntimeException("OverflowError", "cannot convert Infinity to integer ratio", span);
            }

            if (double.IsNaN(value))
            {
                throw new LythonRuntimeException("ValueError", "cannot convert NaN to integer ratio", span);
            }

            var bits = BitConverter.DoubleToInt64Bits(value);
            var rawExponent = (int)((bits >> 52) & 0x7FFL);
            var mantissa = (ulong)(bits & 0xFFFFFFFFFFFFFL);
            BigInteger numerator;
            BigInteger denominator;
            if (rawExponent == 0)
            {
                numerator = mantissa;
                denominator = BigInteger.One << 1074;
            }
            else
            {
                var exponent = rawExponent - 1075;
                mantissa |= 1UL << 52;
                if (exponent >= 0)
                {
                    numerator = (BigInteger)mantissa << exponent;
                    denominator = BigInteger.One;
                }
                else
                {
                    numerator = mantissa;
                    denominator = BigInteger.One << -exponent;
                }
            }

            if (bits < 0)
            {
                numerator = BigInteger.Negate(numerator);
            }

            while (numerator.IsEven && denominator.IsEven)
            {
                numerator >>= 1;
                denominator >>= 1;
            }

            return new PyTuple([numerator, denominator]);
        }
    }

    // Exact CPython round-trip formatting (13 lowercase digits, signed
    // decimal exponent, short zero form, plain infinities and nan).
    internal static string FloatToHex(double number)
    {
        if (double.IsNaN(number))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(number))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(number))
        {
            return "-inf";
        }

        var bits = BitConverter.DoubleToInt64Bits(number);
        var negative = bits < 0;
        var exponent = (int)((bits >> 52) & 0x7FFL);
        var fraction = (ulong)(bits & 0xFFFFFFFFFFFFFL);
        string body;
        if (exponent == 0)
        {
            body = fraction == 0
                ? "0x0.0p+0"
                : "0x0." + fraction.ToString("x13") + "p-1022";
        }
        else
        {
            var unbiased = exponent - 1023;
            body = "0x1." + fraction.ToString("x13") + "p" + (unbiased >= 0 ? "+" : "") + unbiased;
        }

        return negative ? "-" + body : body;
    }

    // Parses the CPython hexadecimal float grammar (optional sign and 0x,
    // int/frac hex runs with at least one digit total, optional binary
    // exponent, case-insensitive inf/infinity/nan, ASCII-space padding).
    // Arbitrary digit runs stay exact: the exponent rides a BigInteger so no
    // magnitude can overflow the parser itself.
    internal static double FloatFromHex(string text, LythonSourceSpan span)
    {
        var index = 0;
        while (index < text.Length && IsHexFloatSpace(text[index]))
        {
            index++;
        }

        var negative = false;
        if (index < text.Length && (text[index] == '+' || text[index] == '-'))
        {
            negative = text[index] == '-';
            index++;
        }

        var rest = text.Substring(index).TrimEnd(HexFloatSpaces);
        var lowered = rest.ToLowerInvariant();
        if (lowered is "inf" or "infinity")
        {
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (lowered is "nan")
        {
            return double.NaN;
        }

        var mantissa = BigInteger.Zero;
        var digits = 0;
        if (index + 1 < text.Length && text[index] == '0' && (text[index + 1] == 'x' || text[index + 1] == 'X'))
        {
            index += 2;
        }

        while (index < text.Length && IsHexDigit(text[index]))
        {
            mantissa = (mantissa << 4) | HexValue(text[index]);
            digits++;
            index++;
        }

        var fractionDigits = 0;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && IsHexDigit(text[index]))
            {
                mantissa = (mantissa << 4) | HexValue(text[index]);
                digits++;
                fractionDigits++;
                index++;
            }
        }

        if (digits == 0)
        {
            throw InvalidHexFloat(span);
        }

        var exponent = BigInteger.Zero;
        if (index < text.Length && (text[index] == 'p' || text[index] == 'P'))
        {
            index++;
            var exponentNegative = false;
            if (index < text.Length && (text[index] == '+' || text[index] == '-'))
            {
                exponentNegative = text[index] == '-';
                index++;
            }

            var exponentDigits = 0;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                exponent = exponent * 10 + (text[index] - '0');
                exponentDigits++;
                index++;
            }

            if (exponentDigits == 0)
            {
                throw InvalidHexFloat(span);
            }

            if (exponentNegative)
            {
                exponent = BigInteger.Negate(exponent);
            }
        }

        while (index < text.Length && IsHexFloatSpace(text[index]))
        {
            index++;
        }

        if (index != text.Length)
        {
            throw InvalidHexFloat(span);
        }

        var scaled = DoubleFromExact(negative ? -1 : 1, mantissa, exponent - 4 * fractionDigits);
        if (double.IsInfinity(scaled))
        {
            throw new LythonRuntimeException("OverflowError", "hexadecimal value too large to represent as a float", span);
        }

        return scaled;
    }

    private static readonly char[] HexFloatSpaces = [' ', '\t', '\n', '\v', '\f', '\r'];

    private static bool IsHexFloatSpace(char value) => value is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    private static bool IsHexDigit(char value)
        => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');

    private static LythonRuntimeException InvalidHexFloat(LythonSourceSpan span)
        => new("ValueError", "invalid hexadecimal floating-point string", span);

    // Rounds an exact binary rational (sign * mantissa * 2^exponent) to
    // double, half-even; unrepresentable magnitudes exit before any shift,
    // so every shift below stays bounded by the input size.
    internal static double DoubleFromExact(int sign, BigInteger mantissa, BigInteger exponent)
    {
        if (mantissa.IsZero)
        {
            return sign < 0 ? -0.0 : 0.0;
        }

        var top = (long)mantissa.GetBitLength() - 1;
        if (exponent > 1023)
        {
            return sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (exponent < -1076 - top)
        {
            return sign < 0 ? -0.0 : 0.0;
        }

        var magnitude = top + (long)exponent;
        if (magnitude > 1023)
        {
            return sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (magnitude < -1076)
        {
            return sign < 0 ? -0.0 : 0.0;
        }

        var normal = magnitude >= -1022;
        var target = normal ? magnitude - 52 : -1074;
        var shift = (long)exponent - target;
        BigInteger significand;
        var roundUp = false;
        if (shift >= 0)
        {
            significand = mantissa << (int)shift;
        }
        else if (-shift > int.MaxValue)
        {
            significand = BigInteger.Zero;
        }
        else
        {
            var drop = (int)(-shift);
            significand = mantissa >> drop;
            var remainder = mantissa & ((BigInteger.One << drop) - 1);
            var half = BigInteger.One << (drop - 1);
            var compare = remainder.CompareTo(half);
            roundUp = compare > 0 || (compare == 0 && !significand.IsEven);
        }

        if (roundUp)
        {
            significand += BigInteger.One;
        }

        long result = normal ? magnitude : -1074;
        if (normal && significand == (BigInteger.One << 53))
        {
            significand = BigInteger.One << 52;
            result++;
            if (result > 1023)
            {
                return sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;
            }
        }

        long bits = normal
            ? ((result + 1023) << 52) | (long)(significand - (BigInteger.One << 52))
            : (long)significand;
        if (sign < 0)
        {
            bits |= long.MinValue;
        }

        return BitConverter.Int64BitsToDouble(bits);
    }

    internal static class RangeMembers
    {
        public static bool TryGetMember(PyRange range, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "start" => range.Start,
                "stop" => range.Stop,
                "step" => range.Step,
                "index" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.index() takes exactly one argument (" + arguments.Length + " given)", span);
                    }

                    if (arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool)
                    {
                        throw new LythonRuntimeException("ValueError", "sequence.index(x): x not in sequence", span);
                    }

                    var candidate = arguments[0] switch
                    {
                        BigInteger big => big,
                        int small => new BigInteger(small),
                        _ => BigInteger.One,
                    };

                    if (!TryRangeIndex(range, candidate, out var position))
                    {
                        var rendered = arguments[0] is bool flag ? (flag ? "True" : "False") : candidate.ToString();
                        throw new LythonRuntimeException("ValueError", rendered + " is not in range", span);
                    }

                    return position;
                }, "range.index", ["value"]),
                "count" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.count() takes exactly one argument (" + arguments.Length + " given)", span);
                    }

                    return RangeContains(range, arguments[0]) ? BigInteger.One : BigInteger.Zero;
                }, "range.count", ["value"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class DictMembers
    {
        public static bool TryGetMember(PyDict dict, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "get" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    return dict.TryGetValue(key, out var found)
                        ? found
                        : arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                }, "dict.get", ["key", "default"], 1),
                "keys" => BoundCallable.CreateNoArguments(dict, "dict.keys", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictKeysView(receiver);
                }),
                "values" => BoundCallable.CreateNoArguments(dict, "dict.values", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictValuesView(receiver);
                }),
                "items" => BoundCallable.CreateNoArguments(dict, "dict.items", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictItemsView(receiver);
                }),
                "update" => new RawBoundCallable((arguments, span, context) => UpdateDictionary(dict, arguments, span, context)) { BoundName = "dict.update", BoundReceiver = dict },
                "pop" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.pop(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    if (!dict.TryGetValue(key, out var found))
                    {
                        if (arguments.Length == 2)
                        {
                            return arguments[1];
                        }

                        var renderedKey = key switch
                        {
                            PyString text => text.AsString(),
                            _ => null
                        };
                        throw new LythonRuntimeException(
                            "KeyError",
                            renderedKey is null ? "Key was not found." : $"Key '{renderedKey}' was not found.",
                            span,
                            null,
                            arguments[0]);
                    }

                    dict.Remove(key);
                    return found;
                }, "dict.pop", ["key", "default"], 1),
                "popitem" => BoundCallable.CreateNoArguments(dict, "dict.popitem", static (receiver, span, context) =>
                {
                    if (!receiver.TryRemoveLast(out var key, out var value))
                    {
                        throw new LythonRuntimeException("KeyError", "popitem(): dictionary is empty", span, null, PyString.FromString("popitem(): dictionary is empty"));
                    }

                    return new PyTuple([key, value], context.MemoryGovernor, span);
                }),
                "copy" => BoundCallable.CreateNoArguments(
                    dict,
                    "dict.copy",
                    static (receiver, span, context) => new PyDict(receiver, context.MemoryGovernor, span)),
                "clear" => BoundCallable.CreateNoArguments(dict, "dict.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                "fromkeys" => new BuiltinTypeMethod("dict", "fromkeys", bindsOwner: true, DictFromKeys),
                "setdefault" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.setdefault(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    if (dict.TryGetValue(key, out var found))
                    {
                        return found;
                    }

                    var defaultValue = arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                    dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                    dict.SetItem(key, defaultValue);
                    return defaultValue;
                }, "dict.setdefault", ["key", "default"], 1),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class DictViewMembers
    {
        public static bool TryGetKeysMember(DictKeysView view, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "isdisjoint" => BoundCallable.Create((arguments, span, context) =>
                {
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (view.Source.ContainsKey(ValidateDictionaryKey(item, span)))
                        {
                            return false;
                        }
                    }

                    return true;
                }, OnePositional("dict_keys.isdisjoint", "other")),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryGetItemsMember(DictItemsView view, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "isdisjoint" => BoundCallable.Create((arguments, span, context) =>
                {
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (item is PyTuple pair && pair.Count == 2 &&
                            view.Source.TryGetValue(ValidateDictionaryKey(pair[0], span), out var found) &&
                            PyEquality.AreEqual(found, pair[1]))
                        {
                            return false;
                        }
                    }

                    return true;
                }, OnePositional("dict_items.isdisjoint", "other")),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryGetChainMapKeysMember(ChainMapKeysView view, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "isdisjoint" => BoundCallable.Create((arguments, span, context) =>
                {
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (view.Owner.TryGetMergedValue(ValidateDictionaryKey(item, span), out _))
                        {
                            return false;
                        }
                    }

                    return true;
                }, OnePositional("ChainMap.keys.isdisjoint", "other")),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryGetChainMapItemsMember(ChainMapItemsView view, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "isdisjoint" => BoundCallable.Create((arguments, span, context) =>
                {
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (item is PyTuple pair && pair.Count == 2 &&
                            view.Owner.TryGetMergedValue(ValidateDictionaryKey(pair[0], span), out var found) &&
                            PyEquality.AreEqual(found, pair[1]))
                        {
                            return false;
                        }
                    }

                    return true;
                }, OnePositional("ChainMap.items.isdisjoint", "other")),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static LythonCallableSignature OnePositional(string name, string parameterName)
            => LythonCallableSignature.Create(name, [parameterName], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    }

    internal static class DefaultDictMembers
    {
        public static bool TryGetMember(PyDefaultDict dict, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections"),
                "default_factory" => dict.DefaultFactory ?? PyNone.Instance,
                "get" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    return dict.TryGetValue(key, out var found)
                        ? found
                        : arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                }, "defaultdict.get", ["key", "default"], 1),
                "keys" => BoundCallable.CreateNoArguments(dict, "defaultdict.keys", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictKeysView(receiver.InnerDict);
                }),
                "values" => BoundCallable.CreateNoArguments(dict, "defaultdict.values", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictValuesView(receiver.InnerDict);
                }),
                "items" => BoundCallable.CreateNoArguments(dict, "defaultdict.items", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictItemsView(receiver.InnerDict);
                }),
                "update" => new RawBoundCallable((arguments, span, context) => dict.UpdateFrom(arguments, context, span)) { BoundName = "defaultdict.update", BoundReceiver = dict },
                "pop" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.pop(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    if (!dict.TryGetValue(key, out var found))
                    {
                        if (arguments.Length == 2)
                        {
                            return arguments[1];
                        }

                        var renderedKey = key switch
                        {
                            PyString text => text.AsString(),
                            _ => null
                        };
                        throw new LythonRuntimeException(
                            "KeyError",
                            renderedKey is null ? "Key was not found." : $"Key '{renderedKey}' was not found.",
                            span,
                            null,
                            arguments[0]);
                    }

                    dict.Remove(key);
                    return found;
                }, "defaultdict.pop", ["key", "default"], 1),
                "popitem" => BoundCallable.CreateNoArguments(dict, "defaultdict.popitem", static (receiver, span, context) =>
                {
                    if (!receiver.TryRemoveLast(out var key, out var value))
                    {
                        throw new LythonRuntimeException("KeyError", "popitem(): dictionary is empty", span, null, PyString.FromString("popitem(): dictionary is empty"));
                    }

                    return new PyTuple([key, value], context.MemoryGovernor, span);
                }),
                "setdefault" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.setdefault(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    if (dict.TryGetValue(key, out var found))
                    {
                        return found;
                    }

                    var defaultValue = arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                    dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                    dict.SetItem(key, defaultValue);
                    return defaultValue;
                }, "defaultdict.setdefault", ["key", "default"], 1),
                "copy" => BoundCallable.CreateNoArguments(dict, "defaultdict.copy", static (receiver, span, context) =>
                {
                    var copy = new PyDict(context.MemoryGovernor, span);
                    foreach (var pair in receiver.Items)
                    {
                        copy.SetItem(pair.Key, pair.Value);
                    }

                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new PyDefaultDict(receiver.DefaultFactory, copy);
                }),
                "clear" => BoundCallable.CreateNoArguments(dict, "defaultdict.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class CounterMembers
    {
        public static bool TryGetMember(PyCounter counter, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections"),
                "get" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    return counter.TryGetValue(key, out var found)
                        ? found
                        : arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
                }, "Counter.get", ["key", "default"], 1),
                "update" => new CounterUpdateCallable(counter, subtract: false),
                "subtract" => new CounterUpdateCallable(counter, subtract: true),
                "total" => BoundCallable.CreateNoArguments(counter, "Counter.total", static (receiver, span, context) =>
                {
                    object total = BigInteger.Zero;
                    foreach (var pair in receiver.Items)
                    {
                        total = AddCounterCounts(total, ExpectCounterCount(pair.Value, span), span, context.MemoryGovernor);
                    }

                    return total;
                }),
                "most_common" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.most_common([n]) expects zero or one argument.", span);
                    }

                    int? limit = null;
                    if (arguments.Length == 1)
                    {
                        var integer = ExpectInteger(arguments[0], "Counter.most_common([n]) expects n to be an integer.", span);
                        if (integer < 0)
                        {
                            limit = 0;
                        }
                        else
                        {
                            if (integer > int.MaxValue)
                            {
                                throw new LythonRuntimeException("OverflowError", "Counter.most_common() limit is too large.", span);
                            }

                            limit = (int)integer;
                        }
                    }

                    var sortedItems = counter.Items.ToList();
                    sortedItems.Sort((left, right) => CompareCounterCounts(right.Value, left.Value, span));

                    var count = limit is null ? sortedItems.Count : Math.Min(limit.Value, sortedItems.Count);
                    var items = new object[count];
                    for (var i = 0; i < count; i++)
                    {
                        var pair = sortedItems[i];
                        items[i] = PyTuple.FromOwnedArray([pair.Key, pair.Value], context.MemoryGovernor, span);
                    }

                    return new PyList(items, context.MemoryGovernor, span);
                }, "Counter.most_common", ["n"], 0),
                "elements" => BoundCallable.CreateNoArguments(counter, "Counter.elements", static (receiver, span, context) =>
                {
                    var items = new List<object>();
                    foreach (var pair in receiver.Items)
                    {
                        if (!PyNumberOps.TryAsInteger(pair.Value, out var count))
                        {
                            throw new LythonRuntimeException("TypeError", "Counter.elements() counts must be integers.", span);
                        }
                        if (count <= 0)
                        {
                            continue;
                        }

                        for (var i = BigInteger.Zero; i < count; i++)
                        {
                            items.Add(pair.Key);
                        }
                    }

                    return new PyList(items, context.MemoryGovernor, span);
                }),
                "copy" => BoundCallable.CreateNoArguments(
                    counter,
                    "Counter.copy",
                    static (receiver, span, context) => new PyCounter(receiver, context.MemoryGovernor, span)),
                "clear" => BoundCallable.CreateNoArguments(counter, "Counter.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                "keys" => BoundCallable.CreateNoArguments(counter, "Counter.keys", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictKeysView(receiver.InnerDict);
                }),
                "values" => BoundCallable.CreateNoArguments(counter, "Counter.values", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictValuesView(receiver.InnerDict);
                }),
                "items" => BoundCallable.CreateNoArguments(counter, "Counter.items", static (receiver, span, context) =>
                {
                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    return new DictItemsView(receiver.InnerDict);
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private sealed class CounterUpdateCallable : ICallable, IPyRenderableValue
        {
            private readonly PyCounter _counter;
            private readonly bool _subtract;

            public CounterUpdateCallable(PyCounter counter, bool subtract)
            {
                _counter = counter;
                _subtract = subtract;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                object? source = null;
                var hasSource = false;
                var positionalCount = 0;
                var keywordItems = new List<KeyValuePair<string, object>>();

                foreach (var argument in arguments)
                {
                    if (argument.IsPositional)
                    {
                        if (positionalCount >= 1)
                        {
                            throw new LythonRuntimeException("TypeError", $"Counter.{Name}([iterable], **kwargs) expects at most one positional argument.", span);
                        }

                        source = argument.Value;
                        hasSource = true;
                        positionalCount++;
                        continue;
                    }

                    if (argument.KeywordName is "iterable" or "mapping")
                    {
                        if (hasSource)
                        {
                            throw new LythonRuntimeException("TypeError", $"Counter.{Name}(...) got multiple values for iterable.", span);
                        }

                        source = argument.Value;
                        hasSource = true;
                        continue;
                    }

                    keywordItems.Add(new(argument.KeywordName, argument.Value));
                }

                if (hasSource)
                {
                    try
                    {
                        PopulateCounter(_counter, source.RequireNotNull(), span, context, _subtract);
                    }
                    catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
                    {
                        throw new LythonRuntimeException("TypeError", $"Counter.{Name}(iterable) expects one iterable or mapping argument.", span);
                    }
                }

                PopulateCounterKeywords(_counter, keywordItems, span, context, _subtract);
                return PyNone.Instance;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString($"Counter.{Name}");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            private string Name => _subtract ? "subtract" : "update";
        }
    }
}
