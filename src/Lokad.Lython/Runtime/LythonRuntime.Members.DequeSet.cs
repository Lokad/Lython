using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class DequeMembers
    {
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
                    catch (InvalidOperationException)
                    {
                        throw new LythonRuntimeException("IndexError", "pop from an empty deque", span);
                    }
                }),
                "popleft" => BoundCallable.CreateNoArguments(deque, "deque.popleft", static (receiver, span, _) =>
                {
                    try
                    {
                        return receiver.PopLeft();
                    }
                    catch (InvalidOperationException)
                    {
                        throw new LythonRuntimeException("IndexError", "pop from an empty deque", span);
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
                }),
                "clear" => BoundCallable.CreateNoArguments(deque, "deque.clear", static (receiver, _, _) =>
                {
                    receiver.Clear();
                    return PyNone.Instance;
                }),
                "copy" => BoundCallable.CreateNoArguments(
                    deque,
                    "deque.copy",
                    static (receiver, span, context) => new PyDeque(receiver, receiver.MaxLength, context.MemoryGovernor, span)),
                "count" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.count(value) expects one argument.", span);
                    }

                    return new BigInteger(deque.CountValue(arguments[0]));
                }),
                "index" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, deque.Count, 0, context, span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, deque.Count, deque.Count, context, span);
                    var index = deque.IndexOf(arguments[0], start, stop);
                    if (index < 0)
                    {
                        throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in deque", span);
                    }

                    return new BigInteger(index);
                }, "deque.index", ["value", "start", "stop"], 1),
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
                    catch (InvalidOperationException ex) when (ex.Message == "deque already at its maximum size")
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }

                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }, "deque.insert", ["index", "value"]),
                "remove" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.remove(value) expects one argument.", span);
                    }

                    if (!deque.RemoveValue(arguments[0]))
                    {
                        throw new LythonRuntimeException("ValueError", ToReprPyString(arguments[0], context).AsString() + " is not in deque", span);
                    }

                    return PyNone.Instance;
                }, "deque.remove", ["value"]),
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
                }, "deque.rotate", ["n"], 0),
                "__iter__" => BoundCallable.CreateNoArguments(deque, "deque.__iter__", static (receiver, span, context) => new PyEnumerableIterator(receiver, span, context)),
                "__len__" => BoundCallable.CreateNoArguments(deque, "deque.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(deque, arguments[0], span);
                }, "deque.__contains__", ["item"]),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(deque, arguments[0], span, context);
                }, "deque.__getitem__", ["index"]),
                "__setitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__setitem__(index, value) expects two arguments.", span);
                    }

                    SetSubscriptValue(deque, arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "deque.__setitem__", ["index", "value"]),
                "__delitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__delitem__(index) expects one argument.", span);
                    }

                    DeleteSubscriptValue(deque, arguments[0], span, context);
                    return PyNone.Instance;
                }, "deque.__delitem__", ["index"]),
                "__add__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__add__(value) expects one argument.", span);
                    }

                    return EvaluateAdd(deque, arguments[0], context, span);
                }, "deque.__add__", ["value"]),
                "__mul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__mul__(value) expects one argument.", span);
                    }

                    return EvaluateMultiply(deque, arguments[0], context, span);
                }, "deque.__mul__", ["value"]),
                "__rmul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.__rmul__(value) expects one argument.", span);
                    }

                    return EvaluateMultiply(arguments[0], deque, context, span);
                }, "deque.__rmul__", ["value"]),
                "__hash__" => PyNone.Instance,
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
        public static bool TryGetMember(PySet set, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "add" => BoundCallable.Create((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.Add(ValidateSetItem(arguments[0], span, context.MemoryGovernor));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }, OnePositional("set.add", "value")),
                "discard" => BoundCallable.Create((arguments, span, context) =>
                {
                    set.Remove(ValidateSetItem(arguments[0], span, context.MemoryGovernor));
                    return PyNone.Instance;
                }, OnePositional("set.discard", "value")),
                "remove" => BoundCallable.Create((arguments, span, context) =>
                {
                    var candidate = ValidateSetItem(arguments[0], span, context.MemoryGovernor);
                    if (!set.Remove(candidate))
                    {
                        throw new LythonRuntimeException("KeyError", "set item was not found.", span, null, arguments[0]);
                    }

                    return PyNone.Instance;
                }, OnePositional("set.remove", "value")),
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
                    var result = new PySet(set, context.MemoryGovernor, span);
                    Update(result, arguments, span, context);
                    return result;
                }, VariadicPositional("set.union")),
                "intersection" => BoundCallable.Create((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        IntersectWithIterable(result, argument, span, context);
                    }

                    return result;
                }, VariadicPositional("set.intersection")),
                "difference" => BoundCallable.Create((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        result.ExceptWith(MaterializeSet(argument, span, context));
                    }

                    return result;
                }, VariadicPositional("set.difference")),
                "symmetric_difference" => BoundCallable.Create((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    result.SymmetricExceptWith(MaterializeSet(arguments[0], span, context));
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, OnePositional("set.symmetric_difference", "other")),
                "isdisjoint" => BoundCallable.Create((arguments, span, context) =>
                {
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (set.Contains(ValidateSetItem(item, span, context.MemoryGovernor)))
                        {
                            return false;
                        }
                    }

                    return true;
                }, OnePositional("set.isdisjoint", "other")),
                "issubset" => BoundCallable.Create((arguments, span, context) =>
                    IsSubsetOfIterable(set, arguments[0], span, context),
                    OnePositional("set.issubset", "other")),
                "issuperset" => BoundCallable.Create((arguments, span, context) =>
                {
                    foreach (var item in ToSequence(arguments[0], span, context))
                    {
                        if (!set.Contains(ValidateSetItem(item, span, context.MemoryGovernor)))
                        {
                            return false;
                        }
                    }

                    return true;
                }, OnePositional("set.issuperset", "other")),
                "update" => BoundCallable.Create((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    Update(set, arguments, span, context);
                    return PyNone.Instance;
                }, VariadicPositional("set.update")),
                "intersection_update" => BoundCallable.Create((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        IntersectWithIterable(set, argument, span, context);
                    }

                    return PyNone.Instance;
                }, VariadicPositional("set.intersection_update")),
                "difference_update" => BoundCallable.Create((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        set.ExceptWith(MaterializeSet(argument, span, context));
                    }

                    return PyNone.Instance;
                }, VariadicPositional("set.difference_update")),
                "symmetric_difference_update" => BoundCallable.Create((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.SymmetricExceptWith(MaterializeSet(arguments[0], span, context));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }, OnePositional("set.symmetric_difference_update", "other")),
                "__iter__" => BoundCallable.CreateNoArguments(set, "set.__iter__", static (receiver, span, context) => new PyEnumerableIterator(receiver, span, context)),
                "__len__" => BoundCallable.CreateNoArguments(set, "set.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(set, arguments[0], span);
                }, "set.__contains__", ["item"]),
                "__or__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__or__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseOr(set, right, context, span);
                }, "set.__or__", ["value"]),
                "__and__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__and__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseAnd(set, right, context, span);
                }, "set.__and__", ["value"]),
                "__sub__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__sub__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateSubtract(set, right, context, span);
                }, "set.__sub__", ["value"]),
                "__xor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__xor__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet right)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return EvaluateBitwiseXor(set, right, context, span);
                }, "set.__xor__", ["value"]),
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
                }, "set.__ror__", ["value"]),
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
                }, "set.__rand__", ["value"]),
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
                }, "set.__rsub__", ["value"]),
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
                }, "set.__rxor__", ["value"]),
                "__ior__" => BoundCallable.Create((arguments, span, _) =>
                {
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
                }, "set.__ior__", ["value"]),
                "__iand__" => BoundCallable.Create((arguments, span, _) =>
                {
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
                }, "set.__iand__", ["value"]),
                "__isub__" => BoundCallable.Create((arguments, span, _) =>
                {
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
                }, "set.__isub__", ["value"]),
                "__ixor__" => BoundCallable.Create((arguments, span, _) =>
                {
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
                }, "set.__ixor__", ["value"]),
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return set.SetEquals(other);
                }, "set.__eq__", ["value"]),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PySet other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !set.SetEquals(other);
                }, "set.__ne__", ["value"]),
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
                }, "set.__lt__", ["value"]),
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
                }, "set.__le__", ["value"]),
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
                }, "set.__gt__", ["value"]),
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
                }, "set.__ge__", ["value"]),
                "__hash__" => PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static LythonCallableSignature OnePositional(string name, string parameterName)
            => LythonCallableSignature.Create(name, [parameterName], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);

        private static LythonCallableSignature VariadicPositional(string name)
            => LythonCallableSignature.Create(name, requiredCount: 0);

        internal static PySet AsSetOperand(object value, LythonSourceSpan span, ExecutionContext context)
            => value as PySet ?? MaterializeSet(value, span, context);

        private static PySet MaterializeSet(object value, LythonSourceSpan span, ExecutionContext context)
        {
            var result = new PySet(context.MemoryGovernor, span);
            foreach (var item in ToSequence(value, span, context))
            {
                result.Add(ValidateSetItem(item, span, context.MemoryGovernor));
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
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
                var candidate = ValidateSetItem(item, span, context.MemoryGovernor);
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
                var candidate = ValidateSetItem(item, span, context.MemoryGovernor);
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
                    target.Add(ValidateSetItem(item, span, context.MemoryGovernor));
                    context.ObserveCollectionCount(target.Count, span);
                }
            }
        }
    }

}
