using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class ListMembers
    {
        public static bool TryGetMember(PyList list, string name, out object value)
        {
            value = name switch
            {
                "append" => new BoundCallable((arguments, span, context) =>
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
                "extend" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "list.extend(iterable) expects one argument.", span);
                    }

                    list.AttachMemoryGovernor(context.MemoryGovernor, span);
                    foreach (var item in ToSequence(arguments[0], span))
                    {
                        list.Add(item);
                        context.ObserveCollectionCount(list.Count, span);
                    }
                    return PyNone.Instance;
                }, "list.extend", ["iterable"]),
                "pop" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "list.pop() expects no arguments.", span);
                    }

                    if (list.Count == 0)
                    {
                        throw new LythonRuntimeException("IndexError", "pop from empty list", span);
                    }

                    var last = list[^1];
                    list.RemoveAt(list.Count - 1);
                    return last;
                }),
                "copy" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "list.copy() expects no arguments.", span);
                    }

                    return new PyList(list.ToArray(), context.MemoryGovernor, span);
                }),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "list.clear() expects no arguments.", span);
                    }

                    list.Clear();
                    return PyNone.Instance;
                }),
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class DictMembers
    {
        public static bool TryGetMember(PyDict dict, string name, out object value)
        {
            value = name switch
            {
                "get" => new BoundCallable((arguments, span, context) =>
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
                "keys" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.keys() expects no arguments.", span);
                    }

                    return new DictKeysView(dict);
                }),
                "values" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.values() expects no arguments.", span);
                    }

                    return new DictValuesView(dict);
                }),
                "items" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.items() expects no arguments.", span);
                    }

                    return new DictItemsView(dict);
                }),
                "update" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || arguments[0] is not PyDict source)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.update(mapping) expects one dictionary argument.", span);
                    }

                      foreach (var pair in source)
                      {
                          dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                          dict.SetItem(pair.Key, pair.Value);
                          context.ObserveCollectionCount(dict.Count, span);
                      }

                    return PyNone.Instance;
                }, "dict.update", ["mapping"]),
                "pop" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.pop(key) expects one key.", span);
                    }

                    var key = ValidateDictionaryKey(arguments[0], span, context.MemoryGovernor);
                    if (!dict.TryGetValue(key, out var found))
                    {
                        var renderedKey = key switch
                        {
                            PyString text => text.AsString(),
                            string text => text,
                            _ => null
                        };
                        throw new LythonRuntimeException(
                            "KeyError",
                            renderedKey is null ? "Key was not found." : $"Key '{renderedKey}' was not found.",
                            span);
                    }

                    dict.Remove(key);
                    return found;
                }, "dict.pop", ["key"]),
                "copy" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.copy() expects no arguments.", span);
                    }

                    return new PyDict(dict, context.MemoryGovernor, span);
                }),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "dict.clear() expects no arguments.", span);
                    }

                    dict.Clear();
                    return PyNone.Instance;
                }),
                "setdefault" => new BoundCallable((arguments, span, context) =>
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
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class DefaultDictMembers
    {
        public static bool TryGetMember(PyDefaultDict dict, string name, out object value)
        {
            value = name switch
            {
                "default_factory" => dict.DefaultFactory ?? PyNone.Instance,
                "get" => new BoundCallable((arguments, span, context) =>
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
                "keys" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.keys() expects no arguments.", span);
                    }

                    return new PyList(dict.Keys, context.MemoryGovernor, span);
                }),
                "values" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.values() expects no arguments.", span);
                    }

                    return new PyList(dict.Values, context.MemoryGovernor, span);
                }),
                "items" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.items() expects no arguments.", span);
                    }

                    return BuildItemsList(dict, context, span);
                }),
                "setdefault" => new BoundCallable((arguments, span, context) =>
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
                "copy" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.copy() expects no arguments.", span);
                    }

                    var copy = new PyDict(context.MemoryGovernor, span);
                    foreach (var pair in dict.Items)
                    {
                        copy.SetItem(pair.Key, pair.Value);
                    }

                    return new PyDefaultDict(dict.DefaultFactory, copy);
                }),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "defaultdict.clear() expects no arguments.", span);
                    }

                    dict.Clear();
                    return PyNone.Instance;
                }),
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class CounterMembers
    {
        public static bool TryGetMember(PyCounter counter, string name, out object value)
        {
            value = name switch
            {
                "get" => new BoundCallable((arguments, span, context) =>
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
                "update" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.update(iterable) expects one iterable or mapping argument.", span);
                    }

                    try
                    {
                        PopulateCounter(counter, arguments[0], span, context, subtract: false);
                    }
                    catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.update(iterable) expects one iterable or mapping argument.", span);
                    }

                    return PyNone.Instance;
                }, "Counter.update", ["iterable"]),
                "subtract" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.subtract(iterable) expects one iterable or mapping argument.", span);
                    }

                    try
                    {
                        PopulateCounter(counter, arguments[0], span, context, subtract: true);
                    }
                    catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.subtract(iterable) expects one iterable or mapping argument.", span);
                    }

                    return PyNone.Instance;
                }, "Counter.subtract", ["iterable"]),
                "most_common" => new BoundCallable((arguments, span, context) =>
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
                    sortedItems.Sort((left, right) => ExpectCounterCount(right.Value, span).CompareTo(ExpectCounterCount(left.Value, span)));

                    var count = limit is null ? sortedItems.Count : Math.Min(limit.Value, sortedItems.Count);
                    var items = new object[count];
                    for (var i = 0; i < count; i++)
                    {
                        var pair = sortedItems[i];
                        items[i] = new PyTuple([pair.Key, pair.Value], context.MemoryGovernor, span);
                    }

                    return new PyList(items, context.MemoryGovernor, span);
                }, "Counter.most_common", ["n"], 0),
                "elements" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.elements() expects no arguments.", span);
                    }

                    var items = new List<object>();
                    foreach (var pair in counter.Items)
                    {
                        var count = ExpectCounterCount(pair.Value, span);
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
                "copy" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.copy() expects no arguments.", span);
                    }

                    return new PyCounter(counter, context.MemoryGovernor, span);
                }),
                "clear" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.clear() expects no arguments.", span);
                    }

                    counter.Clear();
                    return PyNone.Instance;
                }),
                "keys" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.keys() expects no arguments.", span);
                    }

                    return new PyList(counter.Keys, context.MemoryGovernor, span);
                }),
                "values" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.values() expects no arguments.", span);
                    }

                    return new PyList(counter.Values, context.MemoryGovernor, span);
                }),
                "items" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.items() expects no arguments.", span);
                    }

                    return BuildItemsList(counter, context, span);
                }),
                _ => null!,
            };

            return value is not null;
        }
    }

    private static PyList BuildItemsList(IEnumerable<KeyValuePair<object, object>> pairs, ExecutionContext context, LythonSourceSpan span)
    {
        if (pairs is IReadOnlyCollection<KeyValuePair<object, object>> collection)
        {
            var items = new object[collection.Count];
            var index = 0;
            foreach (var pair in pairs)
            {
                items[index++] = new PyTuple([pair.Key, pair.Value], context.MemoryGovernor, span);
            }

            return new PyList(items, context.MemoryGovernor, span);
        }

        var list = new List<object>();
        foreach (var pair in pairs)
        {
            list.Add(new PyTuple([pair.Key, pair.Value], context.MemoryGovernor, span));
        }

        return new PyList(list, context.MemoryGovernor, span);
    }

    internal static class DequeMembers
    {
        public static bool TryGetMember(PyDeque deque, string name, out object value)
        {
            value = name switch
            {
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

                    return new PyDeque(deque);
                }),
                "count" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "deque.count(value) expects one argument.", span);
                    }

                    return new BigInteger(deque.CountValue(arguments[0]));
                }),
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
                    if (offset < int.MinValue || offset > int.MaxValue)
                    {
                        throw new LythonRuntimeException("OverflowError", "deque rotation is too large.", span);
                    }

                    deque.Rotate((int)offset);
                    return PyNone.Instance;
                }, "deque.rotate", ["n"], 0),
                _ => null!,
            };

            return value is not null;
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
        public static bool TryGetMember(PySet set, string name, out object value)
        {
            value = name switch
            {
                "add" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.add(value) expects one argument.", span);
                    }

                    set.AttachMemoryGovernor(context.MemoryGovernor, span);
                    set.Add(ValidateSetItem(arguments[0], span, context.MemoryGovernor));
                    context.ObserveCollectionCount(set.Count, span);
                    return PyNone.Instance;
                }, "set.add", ["value"]),
                "discard" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.discard(value) expects one argument.", span);
                    }

                    set.Remove(ValidateSetItem(arguments[0], span, context.MemoryGovernor));
                    return PyNone.Instance;
                }, "set.discard", ["value"]),
                "remove" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "set.remove(value) expects one argument.", span);
                    }

                    var candidate = ValidateSetItem(arguments[0], span, context.MemoryGovernor);
                    if (!set.Remove(candidate))
                    {
                        throw new LythonRuntimeException("KeyError", "set item was not found.", span);
                    }

                    return PyNone.Instance;
                }, "set.remove", ["value"]),
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
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class PathStatMembers
    {
        public static bool TryGetMember(LythonPathStat stat, string name, out object value)
        {
            value = name switch
            {
                "exists" => stat.Exists,
                "is_file" => stat.IsFile,
                "is_dir" => stat.IsDir,
                "size" => stat.Size,
                "modified_at" => stat.ModifiedAt,
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class ExceptionInstanceMembers
    {
        public static bool TryGetMember(PyException exception, string name, out object value)
        {
            value = name switch
            {
                "type" => exception.TypeName,
                "message" => exception.Message,
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class DecimalMembers
    {
        public static bool TryGetMember(PyDecimal decimalValue, string name, out object value)
        {
            value = name switch
            {
                "quantize" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2 || arguments[0] is not PyDecimal exponent)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.quantize(exp[, rounding]) expects a Decimal exponent and an optional rounding constant.", span);
                    }

                    return PyDecimalOps.Quantize(decimalValue, exponent, arguments.Length == 2 ? arguments[1] : PyNone.Instance, span);
                }, "Decimal.quantize", ["exp", "rounding"], 1),
                "normalize" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("normalize", decimalValue, arguments, span)),
                "sqrt" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("sqrt", decimalValue, arguments, span)),
                "exp" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("exp", decimalValue, arguments, span)),
                "ln" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("ln", decimalValue, arguments, span)),
                "log10" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("log10", decimalValue, arguments, span)),
                "copy_abs" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_abs", decimalValue, arguments, span)),
                "copy_negate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_negate", decimalValue, arguments, span)),
                "copy_sign" => new BoundCallable((arguments, span, _) => PyDecimalOps.CopySign(decimalValue, arguments, span), "Decimal.copy_sign", ["other"]),
                "to_integral_value" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("to_integral_value", decimalValue, arguments, span)),
                _ => null!,
            };

            return value is not null;
        }
    }

    internal static class PathMembers
    {
        public static bool TryGetMember(PyPath path, string name, out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(PathOps.BaseName(path.Value.AsString())),
                "suffix" => PyString.FromString(PathOps.Suffix(path.Value.AsString())),
                "stem" => PyString.FromString(PathOps.Stem(path.Value.AsString())),
                "parent" => new PyPath(PathOps.Parent(path.Value)),
                "parents" => PathOps.Parents(path.Value),
                "parts" => PathOps.Parts(path.Value),
                "is_absolute" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_absolute() expects no arguments.", span);
                    }

                    return PathOps.IsAbsolute(path.Value.AsString());
                }),
                "joinpath" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length == 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects at least one string argument.", span);
                    }

                    var current = path.Value;
                    foreach (var argument in arguments)
                    {
                        if (!PyStringOps.TryAsString(argument, out var part))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects string arguments.", span);
                        }

                        current = PathOps.Join(current, part);
                    }

                    return new PyPath(current);
                }),
                "match" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.match(pattern) expects one string argument.", span);
                    }

                    return PathOps.Match(path.Value.AsString(), pattern.AsString());
                }, "Path.match", ["pattern"]),
                "as_posix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.as_posix() expects no arguments.", span);
                    }

                    return path.Value;
                }),
                "resolve" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.resolve() expects no arguments.", span);
                    }

                    return new PyPath(PathOps.Normalize(path.Value, PyString.FromString(context.Host.Cwd)));
                }),
                "relative_to" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.relative_to(other) expects one argument.", span);
                    }

                    var other = RequirePath(arguments[0], "Path.relative_to(other)", span);
                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString())));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.relative_to", ["other"]),
                "with_suffix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.with_suffix(suffix) expects one string argument.", span);
                    }

                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.WithSuffix(path.Value.AsString(), suffix.AsString())));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.with_suffix", ["suffix"]),
                "with_name" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var name))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.with_name(name) expects one string argument.", span);
                    }

                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.WithName(path.Value.AsString(), name.AsString())));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.with_name", ["name"]),
                "exists" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostExists(path.Value.AsString(), span);
                }),
                "is_file" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span).IsFile;
                }),
                "is_dir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span).IsDir;
                }),
                "unlink" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.unlink() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    context.HostRemove(path.Value.AsString(), span);
                    return PyNone.Instance;
                }),
                "rename" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.rename(target)", span);
                    context.RegisterHostCall(span);
                    context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                    return target;
                }, "Path.rename", ["target"]),
                "mkdir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.mkdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    context.HostMkDir(path.Value.AsString(), span);
                    return PyNone.Instance;
                }),
                "open" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.open([mode][, encoding]) expects zero to two arguments.", span);
                    }

                    var mode = arguments.Length >= 1
                        ? arguments[0] switch
                        {
                            null => PyString.FromString("r"),
                            PyNone => PyString.FromString("r"),
                            PyString text => text,
                            _ => throw new LythonRuntimeException("TypeError", "Path.open(mode) expects mode to be a string.", span)
                        }
                        : PyString.FromString("r");

                    if (arguments.Length == 2 &&
                        (!PyStringOps.TryAsString(arguments[1], out var encoding) || !encoding.Equals(PyString.FromString("utf-8"))))
                    {
                        throw new LythonRuntimeException("ValueError", "Path.open() only supports encoding='utf-8'.", span);
                    }

                    var modeText = mode.AsString();
                    if (modeText.Contains('b'))
                    {
                        throw new LythonRuntimeException("ValueError", "Path.open() only supports UTF-8 text modes; binary modes like 'rb' and 'wb' are unsupported.", span);
                    }

                    return modeText switch
                    {
                        "r" => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(path.Value.AsString(), context),
                        "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path.Value.AsString(), context),
                        "a" => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path.Value.AsString(), context),
                        _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                    };
                }, "Path.open", ["mode", "encoding"], 0),
                "glob" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.glob(pattern) expects one string argument.", span);
                    }

                    var results = new PyList([], context.MemoryGovernor, span);
                    context.RegisterHostCall(span);
                    foreach (var name in context.HostListDir(path.Value.AsString(), span))
                    {
                        context.CheckExecutionBudget(span);
                        if (LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern))
                        {
                            results.Add(new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                            context.ObserveCollectionCount(results.Count, span);
                        }
                    }

                    return results;
                }, "Path.glob", ["pattern"]),
                "read_text" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.read_text([encoding]) expects zero or one argument.", span);
                    }

                    if (arguments.Length == 1 && (!PyStringOps.TryAsString(arguments[0], out var encoding) || !encoding.Equals(PyString.FromString("utf-8"))))
                    {
                        throw new LythonRuntimeException("ValueError", "Path.read_text() only supports encoding='utf-8'.", span);
                    }

                    return ReadGovernedHostText(path.Value.AsString(), context, span);
                }, "Path.read_text", ["encoding"], 0),
                "write_text" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.write_text(text[, encoding][, newline]) expects a string plus optional keyword-compatible arguments.", span);
                    }

                    if (arguments.Length >= 2 && (!PyStringOps.TryAsString(arguments[1], out var encoding) || !encoding.Equals(PyString.FromString("utf-8"))))
                    {
                        throw new LythonRuntimeException("ValueError", "Path.write_text() only supports encoding='utf-8'.", span);
                    }

                    if (arguments.Length == 3 && (!PyStringOps.TryAsString(arguments[2], out var newline) || !newline.Equals(PyString.Empty)))
                    {
                        throw new LythonRuntimeException("ValueError", "Path.write_text() only supports newline=''.", span);
                    }

                    text = PyStringOps.NormalizeNewlines(text);
                    context.ObserveString(text, span);
                    context.RegisterHostCall(span);
                    context.WriteTextUtf8(path.Value.AsString(), PyStringOps.EncodeUtf8(text), span);
                    return new BigInteger(text.Length);
                }, "Path.write_text", ["text", "encoding", "newline"], 1),
                "rglob" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rglob(pattern) expects one string argument.", span);
                    }

                    var results = new PyList([], context.MemoryGovernor, span);
                    foreach (var item in EnumerateRecursive(path.Value, pattern, context, span))
                    {
                        results.Add(item);
                        context.ObserveCollectionCount(results.Count, span);
                    }

                    return results;
                }, "Path.rglob", ["pattern"]),
                _ => null!,
            };

            return value is not null;
        }

        public static bool TryGetMember(PyPath path, string name, ExecutionContext context, LythonSourceSpan span, out object value)
        {
            value = name switch
            {
                "parents" => PathOps.Parents(path.Value, context.MemoryGovernor, span),
                "parts" => PathOps.Parts(path.Value, context.MemoryGovernor, span),
                _ => null!
            };

            return value is not null;
        }

        private static PyPath RequirePath(object value, string signature, LythonSourceSpan span)
        {
            return value switch
            {
                PyPath path => path,
                _ when PyStringOps.TryAsString(value, out var text) => new PyPath(PathOps.Normalize(text)),
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects a Path or string argument.", span)
            };
        }

        private static IEnumerable<object> EnumerateRecursive(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span)
        {
            context.RegisterHostCall(span);
            foreach (var name in context.HostListDir(root.AsString(), span))
            {
                context.CheckExecutionBudget(span);
                var child = new PyPath(PathOps.Join(root, PyString.FromString(name)));
                context.RegisterHostCall(span);
                var stat = context.HostStat(child.Value.AsString(), span);
                if (stat.IsDir)
                {
                    foreach (var nested in EnumerateRecursive(child.Value, pattern, context, span))
                    {
                        yield return nested;
                    }

                    continue;
                }

                if (stat.IsFile && MatchRglobPattern(name, pattern))
                {
                    yield return child;
                }
            }
        }

        private static bool MatchRglobPattern(string name, PyString pattern)
        {
            return LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern);
        }
    }

    internal static class TextFileHandleMembers
    {
        public static bool TryGetMember(ExecutionContext.TextFileHandle handle, string name, out object value)
        {
            value = name switch
            {
                "__enter__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "__enter__() expects no arguments.", span);
                    }

                    return handle.Enter();
                }),
                "__exit__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "__exit__(exc_type, exc, tb) expects three arguments.", span);
                    }

                    return handle.Exit();
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "__exit__(exc_type, exc, tb) expects three arguments.", span);
                    }

                    return await handle.ExitAsync().ConfigureAwait(false);
                }),
                "read" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.read() expects no arguments.", span);
                    }

                    return handle.Read();
                }),
                "readline" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readline() expects no arguments.", span);
                    }

                    return handle.ReadLine();
                }),
                "readlines" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readlines() expects no arguments.", span);
                    }

                    return handle.ReadLines();
                }),
                "write" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "file.write(text) expects one string argument.", span);
                    }

                    return handle.Write(text);
                }, "file.write", ["text"]),
                "writelines" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects one argument.", span);
                    }

                    return handle.WriteLines(arguments[0], span);
                }, "file.writelines", ["lines"]),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class HostTextInputMembers
    {
        public static bool TryGetMember(HostTextInputHandle handle, string name, out object value)
        {
            value = name switch
            {
                "read" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.read() expects no arguments.", span);
                    }

                    return handle.ReadAll(span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.read() expects no arguments.", span);
                    }

                    return await handle.ReadAllAsync(span).ConfigureAwait(false);
                }),
                "readline" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.readline() expects no arguments.", span);
                    }

                    return handle.ReadLine(span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.readline() expects no arguments.", span);
                    }

                    return await handle.ReadLineAsync(span).ConfigureAwait(false);
                }),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class HostTextOutputMembers
    {
        public static bool TryGetMember(HostTextOutputHandle handle, string name, out object value)
        {
            value = name switch
            {
                "write" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "stream.write(text) expects one string argument.", span);
                    }

                    return handle.Write(text, span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "stream.write(text) expects one string argument.", span);
                    }

                    return await handle.WriteAsync(text, span).ConfigureAwait(false);
                }, "stream.write", ["text"]),
                "flush" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.flush() expects no arguments.", span);
                    }

                    return handle.Flush(span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.flush() expects no arguments.", span);
                    }

                    return await handle.FlushAsync(span).ConfigureAwait(false);
                }),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class CompletedProcessMembers
    {
        public static bool TryGetMember(PyCompletedProcess process, string name, out object value)
        {
            value = name switch
            {
                "returncode" => process.ReturnCode,
                "stdout" => process.Stdout,
                "stderr" => process.Stderr,
                _ => null!
            };

            return value is not null;
        }
    }
}
