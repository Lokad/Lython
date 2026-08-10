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
                "maxlen" => deque.MaxLength is int maxLength ? new BigInteger(maxLength) : PyNone.Instance,
                "append" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.append(value) expects one argument.", span);
                    }

                    deque.Append(arguments[0]);
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "appendleft" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.appendleft(value) expects one argument.", span);
                    }

                    deque.AppendLeft(arguments[0]);
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "pop" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.pop() expects no arguments.", span);
                    }

                    try
                    {
                        return deque.Pop();
                    }
                    catch (InvalidOperationException)
                    {
                        throw new LythonRuntimeException("IndexError", "pop from an empty deque", span);
                    }
                }),
                "popleft" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.popleft() expects no arguments.", span);
                    }

                    try
                    {
                        return deque.PopLeft();
                    }
                    catch (InvalidOperationException)
                    {
                        throw new LythonRuntimeException("IndexError", "pop from an empty deque", span);
                    }
                }),
                "extend" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.extend(iterable) expects one argument.", span);
                    }

                    deque.Extend(ToSequence(arguments[0], span));
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "extendleft" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.extendleft(iterable) expects one argument.", span);
                    }

                    deque.ExtendLeft(ToSequence(arguments[0], span));
                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.clear() expects no arguments.", span);
                    }

                    deque.Clear();
                    return PyNone.Instance;
                }),
                "copy" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.copy() expects no arguments.", span);
                    }

                    return new PyDeque(deque, deque.MaxLength);
                }),
                "count" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.count(value) expects one argument.", span);
                    }

                    return new BigInteger(deque.CountValue(arguments[0]));
                }),
                "index" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = NormalizeDequeSearchBound(arguments.Length >= 2 ? arguments[1] : null, deque.Count, 0, span);
                    var stop = NormalizeDequeSearchBound(arguments.Length >= 3 ? arguments[2] : null, deque.Count, deque.Count, span);
                    var index = deque.IndexOf(arguments[0], start, stop);
                    if (index < 0)
                    {
                        throw new LythonRuntimeException("ValueError", "deque.index(value): value is not in deque", span);
                    }

                    return new BigInteger(index);
                }, "deque.index", ["value", "start", "stop"], 1),
                "insert" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.insert(index, value) expects two arguments.", span);
                    }

                    var index = ExpectDequeInsertIndex(arguments[0], span);
                    try
                    {
                        deque.Insert(index, arguments[1]);
                    }
                    catch (InvalidOperationException ex) when (ex.Message == "deque already at its maximum size")
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }

                    context.ObserveCollectionCount(deque.Count, span);
                    return PyNone.Instance;
                }, "deque.insert", ["index", "value"]),
                "remove" => new BoundCallable((arguments, span, _) =>
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
                "reverse" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.reverse() expects no arguments.", span);
                    }

                    deque.Reverse();
                    return PyNone.Instance;
                }),
                "rotate" => new BoundCallable((arguments, span, _) =>
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

        private static int NormalizeDequeSearchBound(object? value, int length, int defaultValue, LythonSourceSpan span)
        {
            if (value is null)
            {
                return defaultValue;
            }

            var integer = ExpectInteger(value, "deque.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
            if (integer < int.MinValue)
            {
                return 0;
            }

            if (integer > int.MaxValue)
            {
                return length;
            }

            var index = (int)integer;
            if (index < 0)
            {
                index += length;
            }

            return Math.Clamp(index, 0, length);
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
                "add" => new BoundCallable((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.Add(ValidateSetItem(arguments[0], span, context.MemoryGovernor));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }, OnePositional("set.add", "value")),
                "discard" => new BoundCallable((arguments, span, context) =>
                {
                    set.Remove(ValidateSetItem(arguments[0], span, context.MemoryGovernor));
                    return PyNone.Instance;
                }, OnePositional("set.discard", "value")),
                "remove" => new BoundCallable((arguments, span, context) =>
                {
                    var candidate = ValidateSetItem(arguments[0], span, context.MemoryGovernor);
                    if (!set.Remove(candidate))
                    {
                        throw new LythonRuntimeException("KeyError", "set item was not found.", span);
                    }

                    return PyNone.Instance;
                }, OnePositional("set.remove", "value")),
                "copy" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "set.copy() expects no arguments.", span);
                    }

                    return new PySet(set, context.MemoryGovernor, span);
                }),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "set.clear() expects no arguments.", span);
                    }

                    set.Clear();
                    return PyNone.Instance;
                }),
                "pop" => new BoundCallable((arguments, span, _) =>
                {
                    if (!set.TryPop(out var item))
                    {
                        throw new LythonRuntimeException("KeyError", "pop from an empty set", span);
                    }

                    return item;
                }, NoArguments("set.pop")),
                "union" => new BoundCallable((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    Update(result, arguments, span, context);
                    return result;
                }, VariadicPositional("set.union")),
                "intersection" => new BoundCallable((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        IntersectWithIterable(result, argument, span, context);
                    }

                    return result;
                }, VariadicPositional("set.intersection")),
                "difference" => new BoundCallable((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        result.ExceptWith(MaterializeSet(argument, span, context));
                    }

                    return result;
                }, VariadicPositional("set.difference")),
                "symmetric_difference" => new BoundCallable((arguments, span, context) =>
                {
                    var result = new PySet(set, context.MemoryGovernor, span);
                    result.SymmetricExceptWith(MaterializeSet(arguments[0], span, context));
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, OnePositional("set.symmetric_difference", "other")),
                "isdisjoint" => new BoundCallable((arguments, span, context) =>
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
                "issubset" => new BoundCallable((arguments, span, context) =>
                    IsSubsetOfIterable(set, arguments[0], span, context),
                    OnePositional("set.issubset", "other")),
                "issuperset" => new BoundCallable((arguments, span, context) =>
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
                "update" => new BoundCallable((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    Update(set, arguments, span, context);
                    return PyNone.Instance;
                }, VariadicPositional("set.update")),
                "intersection_update" => new BoundCallable((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        IntersectWithIterable(set, argument, span, context);
                    }

                    return PyNone.Instance;
                }, VariadicPositional("set.intersection_update")),
                "difference_update" => new BoundCallable((arguments, span, context) =>
                {
                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var argument in arguments)
                    {
                        set.ExceptWith(MaterializeSet(argument, span, context));
                    }

                    return PyNone.Instance;
                }, VariadicPositional("set.difference_update")),
                "symmetric_difference_update" => new BoundCallable((arguments, span, context) =>
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

        private static LythonCallableSignature NoArguments(string name) => new(name, []);

        private static LythonCallableSignature OnePositional(string name, string parameterName)
            => new(name, [parameterName], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);

        private static LythonCallableSignature VariadicPositional(string name)
            => new(name, ParameterNames: null, RequiredCount: 0, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.Positional, PositionalOnlyCount: 0);

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
