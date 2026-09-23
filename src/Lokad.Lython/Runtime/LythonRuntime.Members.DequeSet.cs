using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class DequeMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature DequeInsertSignature = LythonCallableSignature.Create("deque.insert", ["index", "value"]);
        private static readonly LythonCallableSignature DequeRotateSignature = LythonCallableSignature.Create("deque.rotate", ["n"], 0);
        private static readonly LythonCallableSignature DequeContainsSignature = LythonCallableSignature.Create("deque.__contains__", ["item"]);
        private static readonly LythonCallableSignature DequeGetItemSignature = LythonCallableSignature.Create("deque.__getitem__", ["index"]);
        private static readonly LythonCallableSignature DequeSetItemSignature = LythonCallableSignature.Create("deque.__setitem__", ["index", "value"]);
        private static readonly LythonCallableSignature DequeDelItemSignature = LythonCallableSignature.Create("deque.__delitem__", ["index"]);
        private static readonly LythonCallableSignature DequeAddSignature = LythonCallableSignature.Create("deque.__add__", ["value"]);
        private static readonly LythonCallableSignature DequeMulSignature = LythonCallableSignature.Create("deque.__mul__", ["value"]);
        private static readonly LythonCallableSignature DequeRMulSignature = LythonCallableSignature.Create("deque.__rmul__", ["value"]);
        private static readonly LythonCallableSignature DequeReversedSignature = LythonCallableSignature.Create("deque.__reversed__");
        private static async ValueTask<object> CountAsync(PyDeque deque, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "deque.count(value) expects one argument.", span);
            }

            return new BigInteger(await deque.CountValueAsync(arguments[0], context, span).ConfigureAwait(false));
        }

        private static async ValueTask<object> IndexAsync(PyDeque deque, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "deque.index(value[, start[, stop]]) expects one to three arguments.", span);
            }

            var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, deque.Count, 0, context, span);
            var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, deque.Count, deque.Count, context, span);
            var index = await deque.IndexOfAsync(arguments[0], start, stop, context, span).ConfigureAwait(false);
            if (index < 0)
            {
                throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in deque", span);
            }

            return new BigInteger(index);
        }

        private static async ValueTask<object> RemoveAsync(PyDeque deque, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "deque.remove(value) expects one argument.", span);
            }

            if (!await deque.RemoveValueAsync(arguments[0], context, span).ConfigureAwait(false))
            {
                throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in deque", span);
            }

            return PyNone.Instance;
        }

        // Fresh copies reclaim through the pool once dropped; the later funnel
        // no-ops on the already-tracked value through reference-identity dedup.
        private static PyDeque TrackDequeCopy(PyDeque receiver, ExecutionContext context, LythonSourceSpan span)
        {
            var copy = new PyDeque(receiver, receiver.MaxLength, context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(copy, copy.CommittedStorageBytes);
            return copy;
        }

        public static bool TryGetMember(PyDeque deque, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections"),
                "maxlen" => deque.MaxLength is int maxLength ? new BigInteger(maxLength) : PyNone.Instance,
                "append" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.append(value) expects one argument.", span);
                    }

                    deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                    deque.Append(arguments[0]);
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "appendleft" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.appendleft(value) expects one argument.", span);
                    }

                    deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                    deque.AppendLeft(arguments[0]);
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "pop" => BoundCallable.CreateNoArguments(deque, "deque.pop", static (receiver, span, _) =>
                {
                    try
                    {
                        return receiver.Pop();
                    }
                    catch (PyDequeEmptyException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }
                }),
                "popleft" => BoundCallable.CreateNoArguments(deque, "deque.popleft", static (receiver, span, _) =>
                {
                    try
                    {
                        return receiver.PopLeft();
                    }
                    catch (PyDequeEmptyException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }
                }),
                "extend" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.extend(iterable) expects one argument.", span);
                    }

                    deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                    deque.Extend(ToSequence(arguments[0], span, context));
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }, async (arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.extend(iterable) expects one argument.", span);
                    }

                    deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                    deque.Extend(await PyIteration.MaterializeAsync(arguments[0], span, context).ConfigureAwait(false));
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "extendleft" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.extendleft(iterable) expects one argument.", span);
                    }

                    deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                    deque.ExtendLeft(ToSequence(arguments[0], span, context));
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }, async (arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.extendleft(iterable) expects one argument.", span);
                    }

                    deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                    deque.ExtendLeft(await PyIteration.MaterializeAsync(arguments[0], span, context).ConfigureAwait(false));
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "clear" => BoundCallable.CreateNoArguments(deque, "deque.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                "copy" => BoundCallable.CreateNoArguments(
                    deque,
                    "deque.copy",
                    static (receiver, span, context) => TrackDequeCopy(receiver, context, span)),
                "count" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.count(value) expects one argument.", span);
                    }

                    return new BigInteger(deque.CountValue(arguments[0], context, span));
                }, (arguments, span, context) => CountAsync(deque, arguments, span, context), "deque.count", ["value"]),
                "index" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, deque.Count, 0, context, span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, deque.Count, deque.Count, context, span);
                    var index = deque.IndexOf(arguments[0], start, stop, context, span);
                    if (index < 0)
                    {
                        throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in deque", span);
                    }

                    return new BigInteger(index);
                }, (arguments, span, context) => IndexAsync(deque, arguments, span, context), "deque.index", ["value", "start", "stop"], 1),
                "insert" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.insert(index, value) expects two arguments.", span);
                    }

                    var index = ExpectDequeInsertIndex(arguments[0], context, span);
                    try
                    {
                        deque.AttachMemoryGovernor(context.MemoryGovernor, span);
                        deque.Insert(index, arguments[1]);
                    }
                    catch (PyDequeFullException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }

                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }, DequeInsertSignature),
                "remove" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.remove(value) expects one argument.", span);
                    }

                    if (!deque.RemoveValue(arguments[0], context, span))
                    {
                        throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in deque", span);
                    }

                    return PyNone.Instance;
                }, (arguments, span, context) => RemoveAsync(deque, arguments, span, context), "deque.remove", ["value"]),
                "reverse" => BoundCallable.CreateNoArguments(deque, "deque.reverse", static (receiver, _, _) =>
                {
                    receiver.Reverse();
                    return PyNone.Instance;
                }),
                "rotate" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.rotate([n]) expects zero or one integer argument.", span);
                    }

                    var offset = CoerceRotateOffset(arguments, context, span);
                    deque.Rotate(offset);
                    return PyNone.Instance;
                }, DequeRotateSignature),
                "__iter__" => BoundCallable.CreateNoArguments(deque, "deque.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var dequeIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(dequeIterResult, PyIteratorBase.IteratorValueBytes);
                    return dequeIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(deque, "deque.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(deque, arguments[0], span);
                }, DequeContainsSignature),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(deque, arguments[0], span, context);
                }, DequeGetItemSignature),
                "__setitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__setitem__(index, value) expects two arguments.", span);
                    }

                    SetSubscriptValue(deque, arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, DequeSetItemSignature),
                "__delitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__delitem__(index) expects one argument.", span);
                    }

                    DeleteSubscriptValue(deque, arguments[0], span, context);
                    return PyNone.Instance;
                }, DequeDelItemSignature),
                "__add__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__add__(value) expects one argument.", span);
                    }

                    return EvaluateAdd(deque, arguments[0], context, span);
                }, DequeAddSignature),
                "__mul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__mul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(deque, arguments[0], context, span);
                }, DequeMulSignature),
                "__rmul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__rmul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(arguments[0], deque, context, span);
                }, DequeRMulSignature),
                "__hash__" => PyNone.Instance,
                "__reversed__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__reversed__() expects no arguments.", span);
                    }

                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var dequeReversedResult = new PyReversedIterator(deque.Length, deque.GetIndex);
                    context.Services.State.CallTemporaries.TrackFreshMutable(dequeReversedResult, PyIteratorBase.IteratorValueBytes);
                    return dequeReversedResult;
                }, DequeReversedSignature),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static int ExpectDequeInsertIndex(object value, ExecutionContext context, LythonSourceSpan span)
        {
            // The index coerces through __index__ like CPython; failures name the
            // type instead of the builtin signature.
            var coerced = CoerceIndexProtocol(value, context, span);
            if (!Numbers.PyNumberOps.TryAsInteger(coerced, out var integer))
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

        private static BigInteger CoerceRotateOffset(object[] arguments, ExecutionContext context, LythonSourceSpan span)
        {
            if (arguments.Length == 0)
            {
                return BigInteger.One;
            }

            // The count coerces through __index__ like CPython; failures name
            // the type instead of the builtin signature.
            var coerced = CoerceIndexProtocol(arguments[0], context, span);
            if (!Numbers.PyNumberOps.TryAsInteger(coerced, out var offset))
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(arguments[0], context) + "' object cannot be interpreted as an integer", span);
            }

            return offset;
        }

    private static BigInteger ExpectInteger(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return integer;
    }

    internal static class SetMembers
    {
        // N17: remaining fixed signatures hoisted per family (update kin already static).
        private static readonly LythonCallableSignature SetContainsSignature = LythonCallableSignature.Create("set.__contains__", ["item"]);
        private static readonly LythonCallableSignature SetOrSignature = LythonCallableSignature.Create("set.__or__", ["value"]);
        private static readonly LythonCallableSignature SetAndSignature = LythonCallableSignature.Create("set.__and__", ["value"]);
        private static readonly LythonCallableSignature SetSubSignature = LythonCallableSignature.Create("set.__sub__", ["value"]);
        private static readonly LythonCallableSignature SetXorSignature = LythonCallableSignature.Create("set.__xor__", ["value"]);
        private static readonly LythonCallableSignature SetROrSignature = LythonCallableSignature.Create("set.__ror__", ["value"]);
        private static readonly LythonCallableSignature SetRAndSignature = LythonCallableSignature.Create("set.__rand__", ["value"]);
        private static readonly LythonCallableSignature SetRSubSignature = LythonCallableSignature.Create("set.__rsub__", ["value"]);
        private static readonly LythonCallableSignature SetRXorSignature = LythonCallableSignature.Create("set.__rxor__", ["value"]);
        private static readonly LythonCallableSignature SetIOrSignature = LythonCallableSignature.Create("set.__ior__", ["value"]);
        private static readonly LythonCallableSignature SetIAndSignature = LythonCallableSignature.Create("set.__iand__", ["value"]);
        private static readonly LythonCallableSignature SetISubSignature = LythonCallableSignature.Create("set.__isub__", ["value"]);
        private static readonly LythonCallableSignature SetIXorSignature = LythonCallableSignature.Create("set.__ixor__", ["value"]);
        private static readonly LythonCallableSignature SetEqSignature = LythonCallableSignature.Create("set.__eq__", ["value"]);
        private static readonly LythonCallableSignature SetNeSignature = LythonCallableSignature.Create("set.__ne__", ["value"]);
        private static readonly LythonCallableSignature SetLtSignature = LythonCallableSignature.Create("set.__lt__", ["value"]);
        private static readonly LythonCallableSignature SetLeSignature = LythonCallableSignature.Create("set.__le__", ["value"]);
        private static readonly LythonCallableSignature SetGtSignature = LythonCallableSignature.Create("set.__gt__", ["value"]);
        private static readonly LythonCallableSignature SetGeSignature = LythonCallableSignature.Create("set.__ge__", ["value"]);
        public static bool TryGetMember(PySet set, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "add" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.Add(ValidateSetItem(arguments[0], span));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }, SetAddSignature),
                "discard" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.Remove(ValidateSetItem(arguments[0], span));
                    return PyNone.Instance;
                }, SetDiscardSignature),
                "remove" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    var candidate = ValidateSetItem(arguments[0], span);
                    if (!set.Remove(candidate))
                    {
                        throw new LythonRuntimeException("KeyError", "set item was not found.", span, null, arguments[0]);
                    }

                    return PyNone.Instance;
                }, SetRemoveSignature),
                "copy" => BoundCallable.CreateNoArguments(
                    set,
                    "set.copy",
                    static (receiver, span, context) => new PySet(receiver, context.MemoryGovernor, span)),
                "clear" => BoundCallable.CreateNoArguments(set, "set.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                "pop" => BoundCallable.CreateNoArguments(set, "set.pop", static (receiver, span, context) =>
                {
                    if (!receiver.TryPop(out var item))
                    {
                        throw new LythonRuntimeException(
                            "KeyError",
                            "pop from an empty set",
                            span,
                            null,
                            PyString.FromString("pop from an empty set", context.MemoryGovernor, span));
                    }

                    return item;
                }),
                "union" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    var result = new PySet(set, context.MemoryGovernor, span);
                    Update(result, arguments, span, context);
                    return result;
                }, SetUnionSignature),
                "intersection" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    var result = new PySet(set, context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        IntersectWithIterable(result, argument, span, context);
                    }

                    return result;
                }, SetIntersectionSignature),
                "difference" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    var result = new PySet(set, context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        result.ExceptWith(MaterializeSet(argument, span, context));
                    }

                    return result;
                }, SetDifferenceSignature),
                "symmetric_difference" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    var result = new PySet(set, context.MemoryGovernor, span);
                    result.SymmetricExceptWith(MaterializeSet(arguments[0], span, context));
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, SetSymmetricDifferenceSignature),
                "isdisjoint" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (set.Contains(ValidateSetItem(item, span)))
                        {
                            return false;
                        }
                    }

                    return true;
                }, SetIsDisjointSignature),
                "issubset" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    return IsSubsetOfIterable(set, arguments[0], span, context);
                },
                    SetIsSubsetSignature),
                "issuperset" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (!set.Contains(ValidateSetItem(item, span)))
                        {
                            return false;
                        }
                    }

                    return true;
                }, SetIsSupersetSignature),
                "update" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    Update(set, arguments, span, context);
                    return PyNone.Instance;
                }, SetUpdateSignature, async (arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    await UpdateAsync(set, arguments, span, context).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "intersection_update" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        IntersectWithIterable(set, argument, span, context);
                    }

                    return PyNone.Instance;
                }, SetIntersectionUpdateSignature, async (arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        await IntersectWithIterableAsync(set, argument, span, context).ConfigureAwait(false);
                    }

                    return PyNone.Instance;
                }),
                "difference_update" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        set.ExceptWith(MaterializeSet(argument, span, context));
                    }

                    return PyNone.Instance;
                }, SetDifferenceUpdateSignature, async (arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        set.ExceptWith(await MaterializeSetAsync(argument, span, context).ConfigureAwait(false));
                    }

                    return PyNone.Instance;
                }),
                "symmetric_difference_update" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.SymmetricExceptWith(MaterializeSet(arguments[0], span, context));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }, SetSymmetricDifferenceUpdateSignature, async (arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.SymmetricExceptWith(await MaterializeSetAsync(arguments[0], span, context).ConfigureAwait(false));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }),
                "__iter__" => BoundCallable.CreateNoArguments(set, "set.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var setIterResult = new PyEnumerableIterator(receiver, span, context);
                    context.Services.State.CallTemporaries.TrackFreshMutable(setIterResult, PyIteratorBase.IteratorValueBytes);
                    return setIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(set, "set.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(set, arguments[0], span);
                }, SetContainsSignature),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__or__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseOr(set, right, context, span);
                }, SetOrSignature),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__and__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseAnd(set, right, context, span);
                }, SetAndSignature),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__sub__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateSubtract(set, right, context, span);
                }, SetSubSignature),
                "__xor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__xor__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseXor(set, right, context, span);
                }, SetXorSignature),
                "__ror__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__ror__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet left)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseOr(left, set, context, span);
                }, SetROrSignature),
                "__rand__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__rand__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet left)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseAnd(left, set, context, span);
                }, SetRAndSignature),
                "__rsub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__rsub__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet left)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateSubtract(left, set, context, span);
                }, SetRSubSignature),
                "__rxor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__rxor__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet left)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseXor(left, set, context, span);
                }, SetRXorSignature),
                "__ior__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__ior__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    set.UnionWith(right);
                    return set;
                }, SetIOrSignature),
                "__iand__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__iand__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    set.IntersectWith(right);
                    return set;
                }, SetIAndSignature),
                "__isub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__isub__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    set.ExceptWith(right);
                    return set;
                }, SetISubSignature),
                "__ixor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__ixor__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    set.SymmetricExceptWith(right);
                    return set;
                }, SetIXorSignature),
                "__eq__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return set.SetEquals(other);
                }, SetEqSignature),
                "__ne__" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !set.SetEquals(other);
                }, SetNeSignature),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return set.IsProperSubsetOf(other);
                }, SetLtSignature),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return set.IsSubsetOf(other);
                }, SetLeSignature),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return set.IsProperSupersetOf(other);
                }, SetGtSignature),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return set.IsSupersetOf(other);
                }, SetGeSignature),
                "__hash__" => PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        // Fixed-shape member signatures as shared immutable facts: every call
        // above passes a compile-time literal, so each signature is built once
        // instead of rebuilding the parameter-name array and consulting the
        // signature interner (lookup plus locking) on every member resolution.
        // The interner still returns these same instances; hoisting only
        // removes the per-resolution rebuild.
        private static readonly LythonCallableSignature SetAddSignature = LythonCallableSignature.Create("set.add", ["value"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetDiscardSignature = LythonCallableSignature.Create("set.discard", ["value"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetRemoveSignature = LythonCallableSignature.Create("set.remove", ["value"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetSymmetricDifferenceSignature = LythonCallableSignature.Create("set.symmetric_difference", ["other"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetIsDisjointSignature = LythonCallableSignature.Create("set.isdisjoint", ["other"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetIsSubsetSignature = LythonCallableSignature.Create("set.issubset", ["other"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetIsSupersetSignature = LythonCallableSignature.Create("set.issuperset", ["other"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetSymmetricDifferenceUpdateSignature = LythonCallableSignature.Create("set.symmetric_difference_update", ["other"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature SetUnionSignature = LythonCallableSignature.Create("set.union", requiredCount: 0);
        private static readonly LythonCallableSignature SetIntersectionSignature = LythonCallableSignature.Create("set.intersection", requiredCount: 0);
        private static readonly LythonCallableSignature SetDifferenceSignature = LythonCallableSignature.Create("set.difference", requiredCount: 0);
        private static readonly LythonCallableSignature SetUpdateSignature = LythonCallableSignature.Create("set.update", requiredCount: 0);
        private static readonly LythonCallableSignature SetIntersectionUpdateSignature = LythonCallableSignature.Create("set.intersection_update", requiredCount: 0);
        private static readonly LythonCallableSignature SetDifferenceUpdateSignature = LythonCallableSignature.Create("set.difference_update", requiredCount: 0);

        internal static PySet AsSetOperand(object value, LythonSourceSpan span, ExecutionContext context)
            => value as PySet ?? MaterializeSet(value, span, context);

        private static PySet MaterializeSet(object value, LythonSourceSpan span, ExecutionContext context)
        {
            var result = new PySet(context.MemoryGovernor, span);
            foreach (var item in ToSequence(value, span, context))
            {
                result.Add(ValidateSetItem(item, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
            return result;
        }

        private static async ValueTask<PySet> MaterializeSetAsync(object value, LythonSourceSpan span, ExecutionContext context)
        {
            // Async twin of the materializer above: validation and accounting
            // stay identical while each pull can suspend.
            var result = new PySet(context.MemoryGovernor, span);
            await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
            {
                result.Add(ValidateSetItem(item, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
            return result;
        }

        private static async ValueTask IntersectWithIterableAsync(PySet target, object value, LythonSourceSpan span, ExecutionContext context)
        {
            // Async twin of the streaming intersection above: PySet operands
            // stay on the shared pure-memory path while other iterables await
            // each pull, preserving the early-exit once nothing more can drop.
            if (value is PySet other)
            {
                target.IntersectWith(other);
                return;
            }

            var retained = new PySet(context.MemoryGovernor, span);
            await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
            {
                var candidate = ValidateSetItem(item, span);
                if (target.Contains(candidate))
                {
                    retained.Add(candidate);
                    context.ObserveCollectionCount(retained.Count, span);
                    if (target.Count > 0 && retained.Count == target.Count)
                    {
                        break;
                    }
                }
            }

            target.IntersectWith(retained);
        }

        private static void IntersectWithIterable(PySet target, object value, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is PySet other)
            {
                target.IntersectWith(other);
                return;
            }

            var retained = new PySet(context.MemoryGovernor, span);
            foreach (var item in ToSequence(value, span, context))
            {
                var candidate = ValidateSetItem(item, span);
                if (target.Contains(candidate))
                {
                    retained.Add(candidate);
                    context.ObserveCollectionCount(retained.Count, span);
                    if (target.Count > 0 && retained.Count == target.Count)
                    {
                        break;
                    }
                }
            }

            target.IntersectWith(retained);
        }

        private static bool IsSubsetOfIterable(PySet set, object value, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is PySet other)
            {
                return set.IsSubsetOf(other);
            }

            var found = new PySet(context.MemoryGovernor, span);
            foreach (var item in ToSequence(value, span, context))
            {
                var candidate = ValidateSetItem(item, span);
                if (set.Contains(candidate))
                {
                    found.Add(candidate);
                    context.ObserveCollectionCount(found.Count, span);
                    if (set.Count > 0 && found.Count == set.Count)
                    {
                        return true;
                    }
                }
            }

            return found.Count == set.Count;
        }

        private static void Update(PySet target, object[] iterables, LythonSourceSpan span, ExecutionContext context)
        {
            foreach (var iterable in iterables)
            {
                foreach (var item in ToSequence(iterable, span, context))
                {
                    target.Add(ValidateSetItem(item, span));
                    context.ObserveCollectionCount(target.Count, span);
                }
            }
        }

        private static async ValueTask UpdateAsync(PySet target, object[] iterables, LythonSourceSpan span, ExecutionContext context)
        {
            foreach (var iterable in iterables)
            {
                await foreach (var item in ToSequenceAsync(iterable, span, context).ConfigureAwait(false))
                {
                    target.Add(ValidateSetItem(item, span));
                    context.ObserveCollectionCount(target.Count, span);
                }
            }
        }
    }

}
