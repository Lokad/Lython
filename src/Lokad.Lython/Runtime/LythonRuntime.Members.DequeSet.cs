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
                "index" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 2 ? arguments[1] : null, deque.Count, 0, "deque.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                    var stop = RuntimeArgumentValidation.NormalizeSearchBound(arguments.Length >= 3 ? arguments[2] : null, deque.Count, deque.Count, "deque.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                    var index = deque.IndexOf(arguments[0], start, stop);
                    if (index < 0)
                    {
                        throw new LythonRuntimeException("ValueError", "deque.index(value): value is not in deque", span);
                    }

                    return new BigInteger(index);
                }, "deque.index", ["value", "start", "stop"], 1),
                "insert" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.insert(index, value) expects two arguments.", span);
                    }

                    var index = ExpectDequeInsertIndex(arguments[0], span);
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
                "remove" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.remove(value) expects one argument.", span);
                    }

                    if (!deque.RemoveValue(arguments[0]))
                    {
                        throw new LythonRuntimeException("ValueError", "deque.remove(value): value is not in deque", span);
                    }

                    return PyNone.Instance;
                }, "deque.remove", ["value"]),
                "reverse" => BoundCallable.CreateNoArguments(deque, "deque.reverse", static (receiver, _, _) =>
                {
                    receiver.Reverse();
                    return PyNone.Instance;
                }),
                "rotate" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.rotate([n]) expects zero or one integer argument.", span);
                    }

                    var offset = arguments.Length == 0 ? BigInteger.One : ExpectInteger(arguments[0], "deque.rotate([n]) expects n to be an integer.", span);
                    deque.Rotate(offset);
                    return PyNone.Instance;
                }, "deque.rotate", ["n"], 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static int ExpectDequeInsertIndex(object value, LythonSourceSpan span)
        {
            var integer = ExpectInteger(value, "deque.insert(index, value) expects an integer index.", span);
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
