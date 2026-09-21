using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Direct __mul__/__rmul__ calls convert the count through __index__
    // like CPython, so plain non-integers fail naming the argument while
    // the operators keep the sequence-shaped text; instances coerce (or
    // raise) inside the shared core, so they pass through untouched.
    private static void RequireRepeatCount(object count, ExecutionContext context, LythonSourceSpan span)
    {
        if (count is BigInteger || count is bool)
        {
            return;
        }

        if (count is PyInstance instance &&
            instance.TryGetAttribute("__index__", context, span, out var member) &&
            member is ICallable)
        {
            return;
        }

        throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(count, context) + "' object cannot be interpreted as an integer", span);
    }

    internal static class ListMembers
    {
        // Async twins for the searching members: the synchronous lambdas below
        // run contextually in both modes, but a suspending __eq__ (for example
        // over delayed host reads) needs awaited dispatch under RunAsync.
        private static async ValueTask<object> CountAsync(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "list.count(value) expects one argument.", span);
            }

            var count = 0;
            foreach (var item in list)
            {
                if (await MembershipEqualsAsync(item, arguments[0], context, span).ConfigureAwait(false))
                {
                    count++;
                }
            }

            return new BigInteger(count);
        }

        private static async ValueTask<object> IndexAsync(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "list.index(value[, start[, stop]]) expects one to three arguments.", span);
            }

            var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, list.Count, 0, context, span);
            var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, list.Count, list.Count, context, span);
            for (var i = start; i < stop; i++)
            {
                if (await MembershipEqualsAsync(list[i], arguments[0], context, span).ConfigureAwait(false))
                {
                    return new BigInteger(i);
                }
            }

            throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in list", span);
        }

        private static async ValueTask<object> RemoveAsync(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "list.remove(value) expects one argument.", span);
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (await MembershipEqualsAsync(list[i], arguments[0], context, span).ConfigureAwait(false))
                {
                    list.RemoveAt(i);
                    return PyNone.Instance;
                }
            }

            throw new LythonRuntimeException("ValueError", "list.remove(x): x not in list", span);
        }

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
                "index" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "list.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, list.Count, 0, context, span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, list.Count, list.Count, context, span);
                    for (var i = start; i < stop; i++)
                    {
                        if (MembershipEquals(list[i], arguments[0], context, span))
                        {
                            return new BigInteger(i);
                        }
                    }

                    throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in list", span);
                }, (arguments, span, context) => IndexAsync(list, arguments, span, context), "list.index", ["value", "start", "stop"], 1),
                "count" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.count(value) expects one argument.", span);
                    }

                    var count = 0;
                    foreach (var item in list)
                    {
                        if (MembershipEquals(item, arguments[0], context, span))
                        {
                            count++;
                        }
                    }

                    return new BigInteger(count);
                }, (arguments, span, context) => CountAsync(list, arguments, span, context), "list.count", ["value"]),
                "insert" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "list.insert(index, value) expects two arguments.", span);
                    }

                    var index = ExpectListInsertIndex(arguments[0], context, span);
                    list.AttachMemoryGovernor(context.MemoryGovernor, span);
                    list.Insert(index, arguments[1]);
                    context.ObserveCollectionCount(list.Count, span);
                    return PyNone.Instance;
                }, "list.insert", ["index", "value"]),
                "remove" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.remove(value) expects one argument.", span);
                    }

                    for (var i = 0; i < list.Count; i++)
                    {
                        if (MembershipEquals(list[i], arguments[0], context, span))
                        {
                            list.RemoveAt(i);
                            return PyNone.Instance;
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "list.remove(x): x not in list", span);
                }, (arguments, span, context) => RemoveAsync(list, arguments, span, context), "list.remove", ["value"]),
                "pop" => BoundCallable.Create((arguments, span, context) =>
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
                        : PyIndexing.NormalizePopIndex(CoerceIndexProtocol(arguments[0], context, span), list.Count, span);
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
                "__iter__" => BoundCallable.CreateNoArguments(list, "list.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var listIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(listIterResult, PyIteratorBase.IteratorValueBytes);
                    return listIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(list, "list.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.ContainsWithProtocols(list, arguments[0], context, span);
                }, "list.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(list, arguments[0], span, context);
                }, "list.__getitem__", ["index"]),
                "__setitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__setitem__(index, value) expects two arguments.", span);
                    }

                    SetSubscriptValue(list, arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "list.__setitem__", ["index", "value"]),
                "__delitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__delitem__(index) expects one argument.", span);
                    }

                    DeleteSubscriptValue(list, arguments[0], span, context);
                    return PyNone.Instance;
                }, "list.__delitem__", ["index"]),
                "__add__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__add__(value) expects one argument.", span);
                    }

                    return EvaluateAdd(list, arguments[0], context, span);
                }, "list.__add__", ["value"]),
                "__mul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__mul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(list, arguments[0], context, span);
                }, "list.__mul__", ["value"]),
                "__rmul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__rmul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(arguments[0], list, context, span);
                }, "list.__rmul__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyList other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyEquality.AreEqual(list, other);
                }, "list.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyList other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyEquality.AreEqual(list, other);
                }, "list.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyList other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(list, other, span, "<") < 0;
                }, "list.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyList other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(list, other, span, "<=") <= 0;
                }, "list.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyList other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(list, other, span, ">") > 0;
                }, "list.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyList other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(list, other, span, ">=") >= 0;
                }, "list.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "list.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var listReversedResult = new PyReversedIterator(list.Length, list.GetIndex);
                    context.Services.State.CallTemporaries.TrackFreshMutable(listReversedResult, PyIteratorBase.IteratorValueBytes);
                    return listReversedResult;
                }, "list.__reversed__"),
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
            using var sorted = SortItems(list, keyArgument, reverse, span, context);
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
            using var sorted = await SortItemsAsync(list, keyArgument, reverse, span, context).ConfigureAwait(false);
            list.ReplaceAll(sorted);
            return PyNone.Instance;
        }

        private static int ExpectListInsertIndex(object value, ExecutionContext context, LythonSourceSpan span)
        {
            // The index coerces through __index__ like CPython; failures name the
            // type instead of the builtin signature.
            var coerced = CoerceIndexProtocol(value, context, span);
            if (!PyNumberOps.TryAsInteger(coerced, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
            }

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
        private static async ValueTask<object> CountAsync(int count, Func<int, object> getItem, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "tuple.count(value) expects one argument.", span);
            }

            var itemCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (await MembershipEqualsAsync(getItem(i), arguments[0], context, span).ConfigureAwait(false))
                {
                    itemCount++;
                }
            }

            return new BigInteger(itemCount);
        }

        private static async ValueTask<object> IndexAsync(int count, Func<int, object> getItem, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "tuple.index(value[, start[, stop]]) expects one to three arguments.", span);
            }

            var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, count, 0, context, span);
            var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, count, count, context, span);
            for (var i = start; i < stop; i++)
            {
                if (await MembershipEqualsAsync(getItem(i), arguments[0], context, span).ConfigureAwait(false))
                {
                    return new BigInteger(i);
                }
            }

            throw new LythonRuntimeException("ValueError", "tuple.index(x): x not in tuple", span);
        }

        public static bool TryGetMember(PyTuple tuple, string name, [MaybeNullWhen(false)] out object value)
            => TryGetMember(tuple, name, tuple.Count, index => tuple[index], out value);

        public static bool TryGetMember(PyNamedTupleObject namedTuple, string name, [MaybeNullWhen(false)] out object value)
            => TryGetMember(namedTuple, name, namedTuple.Count, namedTuple.GetItem, out value);

        public static bool TryGetMember(PyTypingNamedTupleObject namedTuple, string name, [MaybeNullWhen(false)] out object value)
            => TryGetMember(namedTuple, name, namedTuple.Count, namedTuple.GetItem, out value);

        private static bool TryGetMember(object source, string name, int count, Func<int, object> getItem, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "index" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, count, 0, context, span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, count, count, context, span);
                    for (var i = start; i < stop; i++)
                    {
                        if (MembershipEquals(getItem(i), arguments[0], context, span))
                        {
                            return new BigInteger(i);
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "tuple.index(x): x not in tuple", span);
                }, (arguments, span, context) => IndexAsync(count, getItem, arguments, span, context), "tuple.index", ["value", "start", "stop"], 1),
                "count" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.count(value) expects one argument.", span);
                    }

                    var itemCount = 0;
                    for (var i = 0; i < count; i++)
                    {
                        if (MembershipEquals(getItem(i), arguments[0], context, span))
                        {
                            itemCount++;
                        }
                    }

                    return new BigInteger(itemCount);
                }, (arguments, span, context) => CountAsync(count, getItem, arguments, span, context), "tuple.count", ["value"]),
                "__iter__" => BoundCallable.CreateNoArguments(source, "tuple.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var tupleIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(tupleIterResult, PyIteratorBase.IteratorValueBytes);
                    return tupleIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(source, "tuple.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.ContainsWithProtocols(source, arguments[0], context, span);
                }, "tuple.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(source, arguments[0], span, context);
                }, "tuple.__getitem__", ["index"]),
                "__add__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__add__(value) expects one argument.", span);
                    }

                    return EvaluateAdd(source, arguments[0], context, span);
                }, "tuple.__add__", ["value"]),
                "__mul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__mul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(source, arguments[0], context, span);
                }, "tuple.__mul__", ["value"]),
                "__rmul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__rmul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(arguments[0], source, context, span);
                }, "tuple.__rmul__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__eq__(value) expects one argument.", span);
                    }

                    if (!PyTupleLike.TryGetItems(arguments[0], out var _items))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyEquality.AreEqual(source, arguments[0]);
                }, "tuple.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__ne__(value) expects one argument.", span);
                    }

                    if (!PyTupleLike.TryGetItems(arguments[0], out var _items))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyEquality.AreEqual(source, arguments[0]);
                }, "tuple.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__lt__(value) expects one argument.", span);
                    }

                    if (!PyTupleLike.TryGetItems(arguments[0], out var _items))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(source, arguments[0], span, "<") < 0;
                }, "tuple.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__le__(value) expects one argument.", span);
                    }

                    if (!PyTupleLike.TryGetItems(arguments[0], out var _items))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(source, arguments[0], span, "<=") <= 0;
                }, "tuple.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__gt__(value) expects one argument.", span);
                    }

                    if (!PyTupleLike.TryGetItems(arguments[0], out var _items))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(source, arguments[0], span, ">") > 0;
                }, "tuple.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__ge__(value) expects one argument.", span);
                    }

                    if (!PyTupleLike.TryGetItems(arguments[0], out var _items))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyComparison.Compare(source, arguments[0], span, ">=") >= 0;
                }, "tuple.__ge__", ["value"]),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "tuple.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(source, span);
                }, "tuple.__hash__"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    // Tri-state numerics shared by the int/float/bool comparison dunders.
    // int slots take the integer tower only and decline floats to the
    // reflected slot like CPython; float slots take the whole tower.
    // Anything else declines with NotImplemented on every dunder, ordering
    // included — the operator machinery raises once both sides decline.
    private static bool TryAsIntegerOperand(object value, out BigInteger integer)
    {
        switch (value)
        {
            case BigInteger big:
                integer = big;
                return true;
            case int small:
                integer = new BigInteger(small);
                return true;
            case bool flag:
                integer = flag ? BigInteger.One : BigInteger.Zero;
                return true;
            default:
                integer = default;
                return false;
        }
    }

    private static bool TryAsFloatOperand(object value, out PyNumber number)
    {
        if (value is int small)
        {
            number = PyNumber.FromInteger(new BigInteger(small));
            return true;
        }

        return PyNumberOps.TryAsNumber(value, out number);
    }


    internal static class NoneMembers
    {
        public static bool TryGetMember(PyNone none, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyNone)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return true;
                }, "None.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyNone)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return false;
                }, "None.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__lt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "None.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__le__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "None.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__gt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "None.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__ge__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "None.__ge__", ["value"]),
                "__bool__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__bool__() expects no arguments.", span);
                    }

                    return false;
                }, "None.__bool__"),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "None.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(none, span);
                }, "None.__hash__"),
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
                "is_integer" => BoundCallable.Create((arguments, span, context) =>
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
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__eq__(value) expects one argument.", span);
                    }

                    if (!TryAsIntegerOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return integer.Value == other;
                }, "int.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__ne__(value) expects one argument.", span);
                    }

                    if (!TryAsIntegerOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return integer.Value != other;
                }, "int.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__lt__(value) expects one argument.", span);
                    }

                    if (!TryAsIntegerOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return integer.Value.CompareTo(other) < 0;
                }, "int.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__le__(value) expects one argument.", span);
                    }

                    if (!TryAsIntegerOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return integer.Value.CompareTo(other) <= 0;
                }, "int.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__gt__(value) expects one argument.", span);
                    }

                    if (!TryAsIntegerOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return integer.Value.CompareTo(other) > 0;
                }, "int.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__ge__(value) expects one argument.", span);
                    }

                    if (!TryAsIntegerOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return integer.Value.CompareTo(other) >= 0;
                }, "int.__ge__", ["value"]),
                "__bool__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__bool__() expects no arguments.", span);
                    }

                    return integer.Value != BigInteger.Zero;
                }, "int.__bool__"),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(integer.Value, span);
                }, "int.__hash__"),
                "__int__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__int__() expects no arguments.", span);
                    }

                    return integer.Value;
                }, "int.__int__"),
                "__index__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__index__() expects no arguments.", span);
                    }

                    return integer.Value;
                }, "int.__index__"),
                "__trunc__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__trunc__() expects no arguments.", span);
                    }

                    return integer.Value;
                }, "int.__trunc__"),
                "__floor__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__floor__() expects no arguments.", span);
                    }

                    return integer.Value;
                }, "int.__floor__"),
                "__ceil__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__ceil__() expects no arguments.", span);
                    }

                    return integer.Value;
                }, "int.__ceil__"),
                "__float__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__float__() expects no arguments.", span);
                    }

                    try
                    {
                        return PyNumberOps.BigIntegerToDouble(integer.Value);
                    }
                    catch (OverflowException ex)
                    {
                        throw new LythonRuntimeException("OverflowError", ex.Message, span);
                    }
                }, "int.__float__"),
                "__round__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "int.__round__([ndigits]) expects zero or one argument.", span);
                    }

                    if (arguments.Length == 1 && arguments[0] is PyNone)
                    {
                        throw new LythonRuntimeException("TypeError", "'NoneType' object cannot be interpreted as an integer", span);
                    }

                    return Round(arguments.Length == 0 ? [integer.Value] : [integer.Value, arguments[0]], span, context);
                }, "int.__round__"),
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
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__eq__(value) expects one argument.", span);
                    }

                    if (!TryAsFloatOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyNumberOps.AreEqual(PyNumber.FromFloat(number), other);
                }, "float.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__ne__(value) expects one argument.", span);
                    }

                    if (!TryAsFloatOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyNumberOps.AreEqual(PyNumber.FromFloat(number), other);
                }, "float.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__lt__(value) expects one argument.", span);
                    }

                    if (!TryAsFloatOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyNumberOps.TryCompare(PyNumber.FromFloat(number), other, out var comparison) && comparison < 0;
                }, "float.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__le__(value) expects one argument.", span);
                    }

                    if (!TryAsFloatOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyNumberOps.TryCompare(PyNumber.FromFloat(number), other, out var comparison) && comparison <= 0;
                }, "float.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__gt__(value) expects one argument.", span);
                    }

                    if (!TryAsFloatOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyNumberOps.TryCompare(PyNumber.FromFloat(number), other, out var comparison) && comparison > 0;
                }, "float.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__ge__(value) expects one argument.", span);
                    }

                    if (!TryAsFloatOperand(arguments[0], out var other))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyNumberOps.TryCompare(PyNumber.FromFloat(number), other, out var comparison) && comparison >= 0;
                }, "float.__ge__", ["value"]),
                "__bool__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__bool__() expects no arguments.", span);
                    }

                    return number != 0.0;
                }, "float.__bool__"),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(number, span);
                }, "float.__hash__"),
                "__int__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__int__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(FloatToInteger(number, span, Math.Truncate), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, "float.__int__"),
                "__float__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__float__() expects no arguments.", span);
                    }

                    return number;
                }, "float.__float__"),
                "__trunc__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__trunc__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(FloatToInteger(number, span, Math.Truncate), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, "float.__trunc__"),
                "__floor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__floor__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(FloatToInteger(number, span, Math.Floor), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, "float.__floor__"),
                "__ceil__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__ceil__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(FloatToInteger(number, span, Math.Ceiling), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, "float.__ceil__"),
                "__round__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "float.__round__([ndigits]) expects zero or one argument.", span);
                    }

                    return Round(arguments.Length == 0 ? [number] : [number, arguments[0]], span, context);
                }, "float.__round__"),
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
                "__iter__" => BoundCallable.CreateNoArguments(range, "range.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var rangeIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(rangeIterResult, PyIteratorBase.IteratorValueBytes);
                    return rangeIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(range, "range.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(range, arguments[0], span);
                }, "range.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(range, arguments[0], span, context);
                }, "range.__getitem__", ["index"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyRange other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyEquality.AreEqual(range, other);
                }, "range.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyRange other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyEquality.AreEqual(range, other);
                }, "range.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__lt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "range.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__le__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "range.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__gt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "range.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__ge__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "range.__ge__", ["value"]),
                "__bool__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__bool__() expects no arguments.", span);
                    }

                    return range.IsTruthy();
                }, "range.__bool__"),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(range, span);
                }, "range.__hash__"),
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "range.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberRangeReversedResult = new PyEnumerableIterator(range.GetSlice(PyNone.Instance, PyNone.Instance, BigInteger.MinusOne, span), span, context, "range_iterator");
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberRangeReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberRangeReversedResult;
                }, "range.__reversed__"),
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
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span);
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
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.pop(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span);
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
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
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
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.setdefault(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span);
                    if (dict.TryGetValue(key, out var found))
                    {
                        return found;
                    }

                    var defaultValue = arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                    dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                    dict.SetItem(key, defaultValue);
                    return defaultValue;
                }, "dict.setdefault", ["key", "default"], 1),
                "__iter__" => BoundCallable.CreateNoArguments(dict, "dict.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var dictIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(dictIterResult, PyIteratorBase.IteratorValueBytes);
                    return dictIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(dict, "dict.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(dict, arguments[0], span);
                }, "dict.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(dict, arguments[0], span, context);
                }, "dict.__getitem__", ["index"]),
                "__setitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__setitem__(index, value) expects two arguments.", span);
                    }

                    SetSubscriptValue(dict, arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "dict.__setitem__", ["index", "value"]),
                "__delitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__delitem__(index) expects one argument.", span);
                    }

                    DeleteSubscriptValue(dict, arguments[0], span, context);
                    return PyNone.Instance;
                }, "dict.__delitem__", ["index"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__or__(value) expects one argument.", span);
                    }

                    if (MergeUnionPairs(arguments[0]) is not { } right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    var merged = new PyDict(context.MemoryGovernor, span);
                    foreach (var pair in dict)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    foreach (var pair in right)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    return merged;
                }, "dict.__or__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__ror__(value) expects one argument.", span);
                    }

                    if (MergeUnionPairs(arguments[0]) is not { } left)
                    {
                        return PyNotImplemented.Instance;
                    }

                    var merged = new PyDict(context.MemoryGovernor, span);
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    foreach (var pair in left)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    foreach (var pair in dict)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    return merged;
                }, "dict.__ror__", ["value"]),
                "__ior__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__ior__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                    UpdateDictionaryFromSource(dict, arguments[0], context, span);
                    context.ObserveCollectionCount(dict.Count, span);
                    return dict;
                }, "dict.__ior__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PyDict or PyDefaultDict or PyCounter))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyEquality.AreEqual(dict, arguments[0]);
                }, "dict.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PyDict or PyDefaultDict or PyCounter))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyEquality.AreEqual(dict, arguments[0]);
                }, "dict.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__lt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "dict.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__le__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "dict.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__gt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "dict.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__ge__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "dict.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberDictReversedResult = dict.CreateReversedKeysIterator(context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberDictReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberDictReversedResult;
                }, "dict.__reversed__"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    // abc views decline non-iterables with NotImplemented on set-operation
    // dunders (unlike C dict views, which raise); the probe enumerates
    // nothing, so one-shot iterables reach the real conversion intact.
    private static bool IsIterableOperand(object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (value is PyInstance instance)
        {
            return instance.TryGetAttribute("__iter__", context, span, out _);
        }

        try
        {
            _ = PyIteration.ToSequence(value, span);
            return true;
        }
        catch (LythonRuntimeException ex) when (IsNonIterableFailure(ex, value))
        {
            return false;
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
                "__iter__" => BoundCallable.CreateNoArguments(view, "dict_keys.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var dictKeysIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(dictKeysIterResult, PyIteratorBase.IteratorValueBytes);
                    return dictKeysIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(view, "dict_keys.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(view, arguments[0], span);
                }, "dict_keys.__contains__", ["item"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__or__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_keys.__or__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__ror__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_keys.__ror__", ["value"]),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__and__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_keys.__and__", ["value"]),
                "__rand__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__rand__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_keys.__rand__", ["value"]),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__sub__(value) expects one argument.", span);
                    }

                    return EvaluateSubtract(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_keys.__sub__", ["value"]),
                "__rsub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__rsub__(value) expects one argument.", span);
                    }

                    return EvaluateSubtract(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_keys.__rsub__", ["value"]),
                "__xor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__xor__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_keys.__xor__", ["value"]),
                "__rxor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__rxor__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_keys.__rxor__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_keys.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_keys.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_keys.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_keys.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_keys.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_keys.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_keys.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberKeysReversedResult = view.Source.CreateReversedKeysIterator(context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberKeysReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberKeysReversedResult;
                }, "dict_keys.__reversed__"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryGetValuesMember(DictValuesView view, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__iter__" => BoundCallable.CreateNoArguments(view, "dict_values.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var dictValuesIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(dictValuesIterResult, PyIteratorBase.IteratorValueBytes);
                    return dictValuesIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(view, "dict_values.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_values.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberValuesReversedResult = view.Source.CreateReversedValuesIterator(context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberValuesReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberValuesReversedResult;
                }, "dict_values.__reversed__"),
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
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
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
                "__iter__" => BoundCallable.CreateNoArguments(view, "dict_items.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var dictItemsIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(dictItemsIterResult, PyIteratorBase.IteratorValueBytes);
                    return dictItemsIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(view, "dict_items.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(view, arguments[0], span);
                }, "dict_items.__contains__", ["item"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__or__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_items.__or__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__ror__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_items.__ror__", ["value"]),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__and__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_items.__and__", ["value"]),
                "__rand__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__rand__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_items.__rand__", ["value"]),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__sub__(value) expects one argument.", span);
                    }

                    return EvaluateSubtract(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_items.__sub__", ["value"]),
                "__rsub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__rsub__(value) expects one argument.", span);
                    }

                    return EvaluateSubtract(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_items.__rsub__", ["value"]),
                "__xor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__xor__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "dict_items.__xor__", ["value"]),
                "__rxor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__rxor__(value) expects one argument.", span);
                    }

                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "dict_items.__rxor__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_items.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_items.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_items.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_items.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_items.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "dict_items.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict_items.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberItemsReversedResult = view.Source.CreateReversedItemsIterator(context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberItemsReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberItemsReversedResult;
                }, "dict_items.__reversed__"),
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
                "__iter__" => BoundCallable.CreateNoArguments(view, "ChainMap.keys.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var chainMapKeysIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(chainMapKeysIterResult, PyIteratorBase.IteratorValueBytes);
                    return chainMapKeysIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(view, "ChainMap.keys.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(view, arguments[0], span);
                }, "ChainMap.keys.__contains__", ["item"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__or__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.keys.__or__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__ror__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.keys.__ror__", ["value"]),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__and__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.keys.__and__", ["value"]),
                "__rand__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__rand__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.keys.__rand__", ["value"]),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__sub__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateSubtract(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.keys.__sub__", ["value"]),
                "__rsub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__rsub__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateSubtract(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.keys.__rsub__", ["value"]),
                "__xor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__xor__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.keys.__xor__", ["value"]),
                "__rxor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__rxor__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.keys.__rxor__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.keys.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.keys.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.keys.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.keys.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.keys.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.keys.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.keys.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
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
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
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
                "__iter__" => BoundCallable.CreateNoArguments(view, "ChainMap.items.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var chainMapItemsIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(chainMapItemsIterResult, PyIteratorBase.IteratorValueBytes);
                    return chainMapItemsIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(view, "ChainMap.items.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(view, arguments[0], span);
                }, "ChainMap.items.__contains__", ["item"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__or__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.items.__or__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__ror__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseOr(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.items.__ror__", ["value"]),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__and__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.items.__and__", ["value"]),
                "__rand__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__rand__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseAnd(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.items.__rand__", ["value"]),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__sub__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateSubtract(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.items.__sub__", ["value"]),
                "__rsub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__rsub__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateSubtract(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.items.__rsub__", ["value"]),
                "__xor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__xor__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(view, span, context), SetMembers.AsSetOperand(arguments[0], span, context), context, span);
                }, "ChainMap.items.__xor__", ["value"]),
                "__rxor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__rxor__(value) expects one argument.", span);
                    }

                    if (!IsIterableOperand(arguments[0], span, context))
                    {
                        return PyNotImplemented.Instance;
                    }


                    return EvaluateBitwiseXor(SetMembers.AsSetOperand(arguments[0], span, context), SetMembers.AsSetOperand(view, span, context), context, span);
                }, "ChainMap.items.__rxor__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.items.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !SetMembers.AsSetOperand(view, span, context).SetEquals(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.items.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.items.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSubsetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.items.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsProperSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.items.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.items.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PySet or DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return SetMembers.AsSetOperand(view, span, context).IsSupersetOf(SetMembers.AsSetOperand(arguments[0], span, context));
                }, "ChainMap.items.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryGetChainMapValuesMember(ChainMapValuesView view, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__iter__" => BoundCallable.CreateNoArguments(view, "ChainMap.values.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var chainMapValuesIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(chainMapValuesIterResult, PyIteratorBase.IteratorValueBytes);
                    return chainMapValuesIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(view, "ChainMap.values.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ChainMap.values.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(view, arguments[0], span);
                }, "ChainMap.values.__contains__", ["item"]),
                "__hash__" => PyNone.Instance,
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

                    var key = ValidateDictionaryKey(arguments[0], span);
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
                "pop" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.pop(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span);
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

                    var key = ValidateDictionaryKey(arguments[0], span);
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
                    var copyResult = new PyDefaultDict(receiver.DefaultFactory, copy);
                    context.Services.State.CallTemporaries.TrackFreshMutable(copyResult, copyResult.CommittedStorageBytes);
                    return copyResult;
                }),
                "clear" => BoundCallable.CreateNoArguments(dict, "defaultdict.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                "__iter__" => BoundCallable.CreateNoArguments(dict, "defaultdict.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var defaultDictIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(defaultDictIterResult, PyIteratorBase.IteratorValueBytes);
                    return defaultDictIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(dict, "defaultdict.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(dict, arguments[0], span);
                }, "defaultdict.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(dict, arguments[0], span, context);
                }, "defaultdict.__getitem__", ["index"]),
                "__setitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__setitem__(index, value) expects two arguments.", span);
                    }

                    SetSubscriptValue(dict, arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "defaultdict.__setitem__", ["index", "value"]),
                "__delitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__delitem__(index) expects one argument.", span);
                    }

                    DeleteSubscriptValue(dict, arguments[0], span, context);
                    return PyNone.Instance;
                }, "defaultdict.__delitem__", ["index"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__or__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    if (MergeUnionPairs(arguments[0]) is null)
                    {
                        return PyNotImplemented.Instance;
                    }

                    var factory = dict.DefaultFactory;
                    var merged = new PyDict(context.MemoryGovernor, span);
                    foreach (var pair in dict.Items)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    foreach (var pair in MergeUnionPairs(arguments[0]).RequireNotNull())
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    var unionResult = new PyDefaultDict(factory, merged);
                    context.Services.State.CallTemporaries.TrackFreshMutable(unionResult, unionResult.CommittedStorageBytes);
                    return unionResult;
                }, "defaultdict.__or__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__ror__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    if (MergeUnionPairs(arguments[0]) is null)
                    {
                        return PyNotImplemented.Instance;
                    }

                    var factory = arguments[0] is PyDefaultDict leftDefault ? leftDefault.DefaultFactory : dict.DefaultFactory;
                    var merged = new PyDict(context.MemoryGovernor, span);
                    foreach (var pair in MergeUnionPairs(arguments[0]).RequireNotNull())
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    foreach (var pair in dict.Items)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    context.MemoryGovernor.Reserve(64L, span);
                    context.MemoryGovernor.Commit(64L);
                    var unionResult = new PyDefaultDict(factory, merged);
                    context.Services.State.CallTemporaries.TrackFreshMutable(unionResult, unionResult.CommittedStorageBytes);
                    return unionResult;
                }, "defaultdict.__ror__", ["value"]),
                "__ior__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__ior__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                    UpdateDictionaryFromSource(dict.InnerDict, arguments[0], context, span);
                    context.ObserveCollectionCount(dict.Count, span);
                    return dict;
                }, "defaultdict.__ior__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PyDict or PyDefaultDict or PyCounter))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyEquality.AreEqual(dict, arguments[0]);
                }, "defaultdict.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not (PyDict or PyDefaultDict or PyCounter))
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyEquality.AreEqual(dict, arguments[0]);
                }, "defaultdict.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__lt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "defaultdict.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__le__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "defaultdict.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__gt__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "defaultdict.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__ge__(value) expects one argument.", span);
                    }

                    return PyNotImplemented.Instance;
                }, "defaultdict.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberDefaultDictReversedResult = dict.InnerDict.CreateReversedKeysIterator(context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberDefaultDictReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberDefaultDictReversedResult;
                }, "defaultdict.__reversed__"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class IteratorMembers
    {
        public static bool TryGetMember(IPyIteratorValue iterator, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__iter__" => BoundCallable.CreateNoArguments(iterator, "iterator.__iter__", static (receiver, _, _) => receiver),
                "__next__" => BoundCallable.CreateNoArguments(iterator, "iterator.__next__", static (receiver, span, context) =>
                {
                    if (receiver.TryMoveNext(out var item))
                    {
                        return LythonRuntime.RuntimeValue(item);
                    }

                    throw new LythonRuntimeException("StopIteration", "", span);
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class CounterMembers
    {
        // Fresh copies reclaim through the pool once dropped; the later funnel
        // no-ops on the already-tracked value through reference-identity dedup.
        private static PyCounter TrackCounterCopy(PyCounter receiver, ExecutionContext context, LythonSourceSpan span)
        {
            var copy = new PyCounter(receiver, context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(copy, copy.CommittedStorageBytes);
            return copy;
        }

        public static bool TryGetMember(PyCounter counter, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections"),
                "get" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span);
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
                        total = AddCounterCounts(total, ExpectCounterCount(pair.Value, span), span, context.MemoryGovernor, context.Services.State.CallTemporaries);
                    }

                    return total;
                }),
                "most_common" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.most_common([n]) expects zero or one argument.", span);
                    }

                    // heapq.nlargest matches sorted(reverse) even on ties, so equal
                    // counts keep insertion order; List.Sort is unstable, while
                    // OrderByDescending is documented stable.
                    List<KeyValuePair<object, object>> sortedItems;
                    try
                    {
                        sortedItems = counter.Items
                            .OrderByDescending(
                            pair => pair.Value,
                            Comparer<object>.Create((left, right) => CompareCounterCounts(left, right, span, "<")))
                            .ToList();
                    }
                    catch (InvalidOperationException ex) when (ex.InnerException is LythonRuntimeException lythonFailure
                        && (lythonFailure.ExceptionType is "TypeError"
                        || lythonFailure.Identity == LythonRuntime.ModuleException("decimal", "InvalidOperation")))
                    {
                        throw lythonFailure;
                    }

                    int? limit = null;
                    if (arguments.Length == 1)
                    {
                        limit = CoerceMostCommonLimit(arguments[0], sortedItems.Count, context, span);
                    }

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
                    static (receiver, span, context) => TrackCounterCopy(receiver, context, span)),
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
                "pop" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.pop(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span);
                    if (!counter.TryGetValue(key, out var found))
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

                    counter.Remove(key);
                    return found;
                }, "Counter.pop", ["key", "default"], 1),
                "__iter__" => BoundCallable.CreateNoArguments(counter, "Counter.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var counterIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(counterIterResult, PyIteratorBase.IteratorValueBytes);
                    return counterIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(counter, "Counter.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(counter, arguments[0], span);
                }, "Counter.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(counter, arguments[0], span, context);
                }, "Counter.__getitem__", ["index"]),
                "__setitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__setitem__(index, value) expects two arguments.", span);
                    }

                    SetSubscriptValue(counter, arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "Counter.__setitem__", ["index", "value"]),
                "__delitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__delitem__(index) expects one argument.", span);
                    }

                    DeleteSubscriptValue(counter, arguments[0], span, context);
                    return PyNone.Instance;
                }, "Counter.__delitem__", ["index"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__or__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    if (arguments[0] is not PyCounter right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return BuildCounterBinaryResult(counter, right, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) >= 0 ? lhs : rhs, keepPositiveOnly: true, span, context);
                }, "Counter.__or__", ["value"]),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__and__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return BuildCounterBinaryResult(counter, right, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) < 0 ? lhs : rhs, keepPositiveOnly: true, span, context);
                }, "Counter.__and__", ["value"]),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__sub__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return BuildCounterBinaryResult(counter, right, (lhs, rhs) => SubtractCounterCounts(lhs, rhs, span, counter.OwnerMemoryGovernor ?? right.OwnerMemoryGovernor, context.Services.State.CallTemporaries), keepPositiveOnly: true, span, context);
                }, "Counter.__sub__", ["value"]),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__ror__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    if (MergeUnionPairs(arguments[0]) is null)
                    {
                        return PyNotImplemented.Instance;
                    }

                    var merged = new PyDict(context.MemoryGovernor, span);
                    foreach (var pair in MergeUnionPairs(arguments[0]).RequireNotNull())
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    foreach (var pair in counter.Items)
                    {
                        merged.SetItem(pair.Key, pair.Value);
                    }

                    return merged;
                }, "Counter.__ror__", ["value"]),
                "__ior__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__ior__(value) expects one argument.", span);
                    }
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

                    var other = arguments[0];
                    if (IsInPlaceMergeOperand(other))
                    {
                        MergeCounterUnionInPlace(counter, other, span);
                        return counter;
                    }

                    // CPython unions through other.items(), so exotic
                    // mappings flow through the same max-merge instead of
                    // failing; a missing items member is the house
                    // AttributeError like the |= statement path.
                    if (!TryResolveRuntimeMember(other, "items", context, span, out var itemsMember))
                    {
                        throw PyMemberAccess.CreateMissingMemberError(other, "items", span, context);
                    }

                    if (itemsMember is not ICallable itemsCallable)
                    {
                        throw new LythonRuntimeException("TypeError", " + RuntimeErrors.OperandTypeName(itemsMember) +  object is not callable", span);
                    }

                    foreach (var element in ToSequence(RuntimeValue(itemsCallable.Invoke([], span, context)), span, context))
                    {
                        var values = MaterializeUnpackingSequence(element, span, context);
                        if (values.Length != 2)
                        {
                            throw new LythonRuntimeException("ValueError", DescribeLoopArityMismatch(2, values.Length), span);
                        }

                        MergeCounterUnionPair(counter, values[0], values[1], span);
                    }

                    PurgeCounterNonPositive(counter, span);
                    return counter;
                }, "Counter.__ior__", ["value"]),
                "__iand__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__iand__(value) expects one argument.", span);
                    }

                    var other = arguments[0];
                    var keys = new List<object>();
                    foreach (var pair in counter.Items)
                    {
                        keys.Add(pair.Key);
                    }

                    foreach (var key in keys)
                    {
                        var otherCount = ReadSubscriptValue(other, key, span, _);
                        var current = counter.TryGetValue(key, out var found) ? found : BigInteger.Zero;
                        var count = CompareCounterCounts(otherCount, current, span, "<") < 0 ? otherCount : current;
                        if (CompareCounterCounts(count, BigInteger.Zero, span, ">") > 0)
                        {
                            counter.SetItem(key, count);
                        }
                        else
                        {
                            counter.Remove(key);
                        }
                    }

                    return counter;
                }, "Counter.__iand__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyEquality.CountersEqual(counter, other);
                }, "Counter.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyEquality.CountersEqual(counter, other);
                }, "Counter.__ne__", ["value"]),
                "__lt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return MultisetLessEqual(counter, other, context, span) && !PyEquality.CountersEqual(counter, other);
                }, "Counter.__lt__", ["value"]),
                "__le__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return MultisetLessEqual(counter, other, context, span);
                }, "Counter.__le__", ["value"]),
                "__gt__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return MultisetLessEqual(other, counter, context, span) && !PyEquality.CountersEqual(counter, other);
                }, "Counter.__gt__", ["value"]),
                "__ge__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyCounter other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return MultisetLessEqual(other, counter, context, span);
                }, "Counter.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var memberCounterReversedResult = counter.InnerDict.CreateReversedKeysIterator(context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(memberCounterReversedResult, PyIteratorBase.IteratorValueBytes);
                    return memberCounterReversedResult;
                }, "Counter.__reversed__"),
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
                    catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && (ex.Message == "Object is not iterable." || ex.Message == "'" + RuntimeErrors.OperandTypeName(source) + "' object is not iterable"))
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
