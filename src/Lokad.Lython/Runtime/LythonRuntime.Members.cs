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
                "st_size" => stat.Size,
                "st_mtime" => PathModifiedAtSeconds(stat.ModifiedAt, null),
                "st_ctime" => PathModifiedAtSeconds(stat.ModifiedAt, null),
                "st_atime" => PathModifiedAtSeconds(stat.ModifiedAt, null),
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
        private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

        public static bool TryGetMember(PyPath path, string name, out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(PathOps.BaseName(path.Value.AsString())),
                "suffix" => PyString.FromString(PathOps.Suffix(path.Value.AsString())),
                "suffixes" => PathSuffixes(path.Value),
                "stem" => PyString.FromString(PathOps.Stem(path.Value.AsString())),
                "parent" => new PyPath(PathOps.Parent(path.Value)),
                "parents" => PathOps.Parents(path.Value),
                "parts" => PathOps.Parts(path.Value),
                "drive" => PyString.Empty,
                "root" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                "anchor" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
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
                "is_relative_to" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_relative_to(other) expects one argument.", span);
                    }

                    var other = RequirePath(arguments[0], "Path.is_relative_to(other)", span);
                    try
                    {
                        PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString());
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                }, "Path.is_relative_to", ["other"]),
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
                "absolute" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.absolute() expects no arguments.", span);
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
                "with_stem" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var stem))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.with_stem(stem) expects one string argument.", span);
                    }

                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.WithName(path.Value.AsString(), stem.AsString() + PathOps.Suffix(path.Value.AsString()))));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.with_stem", ["stem"]),
                "stat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }),
                "exists" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostExists(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostExistsAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }),
                "is_file" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span).IsFile;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsFile;
                }),
                "is_dir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span).IsDir;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsDir;
                }),
                "is_symlink" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_symlink() expects no arguments.", span);
                    }

                    return false;
                }),
                "unlink" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                    }

                    var missingOk = ParseOptionalBool(arguments, 0, false, "Path.unlink([missing_ok])", "missing_ok", span);
                    if (missingOk)
                    {
                        context.RegisterHostCall(span);
                        if (!context.HostStat(path.Value.AsString(), span).Exists)
                        {
                            return PyNone.Instance;
                        }
                    }

                    context.RegisterHostCall(span);
                    context.HostRemove(path.Value.AsString(), span);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                    }

                    var missingOk = ParseOptionalBool(arguments, 0, false, "Path.unlink([missing_ok])", "missing_ok", span);
                    if (missingOk)
                    {
                        context.RegisterHostCall(span);
                        if (!(await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).Exists)
                        {
                            return PyNone.Instance;
                        }
                    }

                    context.RegisterHostCall(span);
                    await context.HostRemoveAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    return PyNone.Instance;
                }, "Path.unlink", ["missing_ok"], 0),
                "rmdir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    context.HostRemove(path.Value.AsString(), span);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    await context.HostRemoveAsync(path.Value.AsString(), span).ConfigureAwait(false);
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
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.rename(target)", span);
                    context.RegisterHostCall(span);
                    await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                    return target;
                }, "Path.rename", ["target"]),
                "replace" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.replace(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.replace(target)", span);
                    context.RegisterHostCall(span);
                    if (context.HostStat(target.Value.AsString(), span).Exists)
                    {
                        context.RegisterHostCall(span);
                        context.HostRemove(target.Value.AsString(), span);
                    }

                    context.RegisterHostCall(span);
                    context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                    return target;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.replace(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.replace(target)", span);
                    context.RegisterHostCall(span);
                    if ((await context.HostStatAsync(target.Value.AsString(), span).ConfigureAwait(false)).Exists)
                    {
                        context.RegisterHostCall(span);
                        await context.HostRemoveAsync(target.Value.AsString(), span).ConfigureAwait(false);
                    }

                    context.RegisterHostCall(span);
                    await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                    return target;
                }, "Path.replace", ["target"]),
                "mkdir" => new BoundCallable((arguments, span, context) =>
                {
                    PathMkDir(path.Value.AsString(), arguments, span, context);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    await PathMkDirAsync(path.Value.AsString(), arguments, span, context).ConfigureAwait(false);
                    return PyNone.Instance;
                }, "Path.mkdir", ["mode", "parents", "exist_ok"], 0),
                "touch" => new BoundCallable((arguments, span, context) =>
                {
                    PathTouch(path.Value.AsString(), arguments, span, context);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    await PathTouchAsync(path.Value.AsString(), arguments, span, context).ConfigureAwait(false);
                    return PyNone.Instance;
                }, "Path.touch", ["mode", "exist_ok"], 0),
                "open" => new BoundCallable((arguments, span, context) =>
                {
                    var (mode, encodingMode) = ParsePathOpenArguments(arguments, span);
                    var modeText = mode.AsString();
                    return modeText switch
                    {
                        "r" => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(path.Value.AsString(), context, encodingMode),
                        "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path.Value.AsString(), context, encodingMode),
                        "a" => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path.Value.AsString(), context, encodingMode),
                        _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                    };
                },
                async (arguments, span, context) =>
                {
                    var (mode, encodingMode) = ParsePathOpenArguments(arguments, span);
                    var modeText = mode.AsString();
                    return modeText switch
                    {
                        "r" => await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync(path.Value.AsString(), context, encodingMode).ConfigureAwait(false),
                        "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path.Value.AsString(), context, encodingMode),
                        "a" => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path.Value.AsString(), context, encodingMode),
                        _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                    };
                }, "Path.open", ["mode", "encoding", "errors", "newline"], 0),
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
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.glob(pattern) expects one string argument.", span);
                    }

                    var results = new PyList([], context.MemoryGovernor, span);
                    context.RegisterHostCall(span);
                    var names = await context.HostListDirAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    foreach (var name in names)
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
                "iterdir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.iterdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    var entries = context.HostListDir(path.Value.AsString(), span)
                        .Select<string, object>(name => new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                    return new PyList(entries, context.MemoryGovernor, span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.iterdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    var names = await context.HostListDirAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    var entries = names.Select<string, object>(name => new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                    return new PyList(entries, context.MemoryGovernor, span);
                }),
                "read_text" => new BoundCallable((arguments, span, context) =>
                {
                    var encodingMode = ParsePathReadTextArguments(arguments, span);
                    return ReadPathText(path.Value.AsString(), encodingMode, context, span);
                },
                async (arguments, span, context) =>
                {
                    var encodingMode = ParsePathReadTextArguments(arguments, span);
                    return await ReadPathTextAsync(path.Value.AsString(), encodingMode, context, span).ConfigureAwait(false);
                }, "Path.read_text", ["encoding", "errors"], 0),
                "write_text" => new BoundCallable((arguments, span, context) =>
                {
                    var (text, encodingMode) = ParsePathWriteTextArguments(arguments, span);
                    context.ObserveString(text, span);
                    context.RegisterHostCall(span);
                    context.WriteTextUtf8(path.Value.AsString(), EncodePathText(text, encodingMode), span);
                    return new BigInteger(text.Length);
                },
                async (arguments, span, context) =>
                {
                    var (text, encodingMode) = ParsePathWriteTextArguments(arguments, span);
                    context.ObserveString(text, span);
                    context.RegisterHostCall(span);
                    await context.WriteTextUtf8Async(path.Value.AsString(), EncodePathText(text, encodingMode), span).ConfigureAwait(false);
                    return new BigInteger(text.Length);
                }, "Path.write_text", ["text", "encoding", "errors", "newline"], 1),
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
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rglob(pattern) expects one string argument.", span);
                    }

                    var results = new PyList([], context.MemoryGovernor, span);
                    await EnumerateRecursiveAsync(path.Value, pattern, context, span, results).ConfigureAwait(false);
                    return results;
                }, "Path.rglob", ["pattern"]),
                "samefile" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.samefile(other_path) expects one argument.", span);
                    }

                    var other = RequirePath(arguments[0], "Path.samefile(other_path)", span);
                    var left = PathOps.Normalize(path.Value.AsString(), context.Host.Cwd);
                    var right = PathOps.Normalize(other.Value.AsString(), context.Host.Cwd);
                    context.RegisterHostCall(span);
                    var leftStat = context.HostStat(left, span);
                    context.RegisterHostCall(span);
                    var rightStat = context.HostStat(right, span);
                    if (!leftStat.Exists || !rightStat.Exists)
                    {
                        throw new LythonRuntimeException("RuntimeError", "Path.samefile() expects both paths to exist.", span);
                    }

                    return string.Equals(left, right, StringComparison.Ordinal);
                }, "Path.samefile", ["other_path"]),
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

        private static PyList PathSuffixes(PyString path)
        {
            var name = PathOps.BaseName(path.AsString());
            var suffixes = new List<object>();
            var dot = name.IndexOf('.', name.StartsWith(".", StringComparison.Ordinal) ? 1 : 0);
            while (dot >= 0 && dot < name.Length - 1)
            {
                var next = name.IndexOf('.', dot + 1);
                suffixes.Add(PyString.FromString(next < 0 ? name[dot..] : name[dot..next]));
                dot = next;
            }

            return new PyList(suffixes);
        }

        private static bool ParseOptionalBool(object[] arguments, int index, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is null or PyNone)
            {
                return defaultValue;
            }

            return arguments[index] is bool value
                ? value
                : throw new LythonRuntimeException("TypeError", $"{owner} expects {parameterName} to be a bool.", span);
        }

        private static void ValidateIgnoredPathMode(object[] arguments, int index, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is null or PyNone)
            {
                return;
            }

            if (!Numbers.PyNumberOps.TryAsInteger(arguments[index], out _))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects mode to be an integer.", span);
            }
        }

        private static void PathMkDir(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.mkdir([mode][, parents][, exist_ok])", span);
            var parents = ParseOptionalBool(arguments, 1, false, "Path.mkdir([mode][, parents][, exist_ok])", "parents", span);
            var existOk = ParseOptionalBool(arguments, 2, false, "Path.mkdir([mode][, parents][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);

            if (parents)
            {
                PathMkDirs(normalized, existOk, span, context);
                return;
            }

            if (existOk)
            {
                context.RegisterHostCall(span);
                var stat = context.HostStat(normalized, span);
                if (stat.Exists && stat.IsDir)
                {
                    return;
                }

                if (stat.Exists)
                {
                    throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
                }
            }

            context.RegisterHostCall(span);
            context.HostMkDir(normalized, span);
        }

        private static async ValueTask PathMkDirAsync(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.mkdir([mode][, parents][, exist_ok])", span);
            var parents = ParseOptionalBool(arguments, 1, false, "Path.mkdir([mode][, parents][, exist_ok])", "parents", span);
            var existOk = ParseOptionalBool(arguments, 2, false, "Path.mkdir([mode][, parents][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);

            if (parents)
            {
                await PathMkDirsAsync(normalized, existOk, span, context).ConfigureAwait(false);
                return;
            }

            if (existOk)
            {
                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
                if (stat.Exists && stat.IsDir)
                {
                    return;
                }

                if (stat.Exists)
                {
                    throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
                }
            }

            context.RegisterHostCall(span);
            await context.HostMkDirAsync(normalized, span).ConfigureAwait(false);
        }

        private static void PathMkDirs(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.RegisterHostCall(span);
            var stat = context.HostStat(normalized, span);
            if (stat.Exists)
            {
                if (stat.IsDir && existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
            }

            foreach (var current in EnumerateMissingDirectories(normalized))
            {
                context.RegisterHostCall(span);
                var currentStat = context.HostStat(current, span);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                context.HostMkDir(current, span);
            }
        }

        private static async ValueTask PathMkDirsAsync(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
            if (stat.Exists)
            {
                if (stat.IsDir && existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
            }

            foreach (var current in EnumerateMissingDirectories(normalized))
            {
                context.RegisterHostCall(span);
                var currentStat = await context.HostStatAsync(current, span).ConfigureAwait(false);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                await context.HostMkDirAsync(current, span).ConfigureAwait(false);
            }
        }

        private static void PathTouch(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.touch([mode][, exist_ok])", span);
            var existOk = ParseOptionalBool(arguments, 1, true, "Path.touch([mode][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);
            context.RegisterHostCall(span);
            var stat = context.HostStat(normalized, span);
            if (stat.Exists)
            {
                if (existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.touch() target already exists: {normalized}", span);
            }

            context.RegisterHostCall(span);
            context.WriteTextUtf8(normalized, Array.Empty<byte>(), span);
        }

        private static async ValueTask PathTouchAsync(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.touch([mode][, exist_ok])", span);
            var existOk = ParseOptionalBool(arguments, 1, true, "Path.touch([mode][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
            if (stat.Exists)
            {
                if (existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.touch() target already exists: {normalized}", span);
            }

            context.RegisterHostCall(span);
            await context.WriteTextUtf8Async(normalized, Array.Empty<byte>(), span).ConfigureAwait(false);
        }

        private static (PyString Mode, TextEncodingMode EncodingMode) ParsePathOpenArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length > 4)
            {
                throw new LythonRuntimeException("TypeError", "Path.open([mode][, encoding][, errors][, newline]) expects supported text-mode options.", span);
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

            var encodingMode = arguments.Length >= 2
                ? ParseTextEncoding(arguments[1], "Path.open()", span)
                : TextEncodingMode.Utf8;

            if (arguments.Length >= 3)
            {
                ValidateStrictTextErrors(arguments[2], "Path.open()", span);
            }

            if (arguments.Length == 4)
            {
                ValidatePathNewline(arguments[3], "Path.open()", span);
            }

            var modeText = mode.AsString();
            if (modeText.Contains('b'))
            {
                throw new LythonRuntimeException("ValueError", "Path.open() only supports UTF-8 text modes; binary modes like 'rb' and 'wb' are unsupported.", span);
            }

            return (mode, encodingMode);
        }

        private static TextEncodingMode ParsePathReadTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.read_text([encoding][, errors]) expects zero to two arguments.", span);
            }

            var encodingMode = arguments.Length >= 1
                ? ParseTextEncoding(arguments[0], "Path.read_text()", span)
                : TextEncodingMode.Utf8;

            if (arguments.Length == 2)
            {
                ValidateStrictTextErrors(arguments[1], "Path.read_text()", span);
            }

            return encodingMode;
        }

        private static (PyString Text, TextEncodingMode EncodingMode) ParsePathWriteTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Path.write_text(text[, encoding][, errors][, newline]) expects a string plus optional keyword-compatible arguments.", span);
            }

            var encodingMode = arguments.Length >= 2
                ? ParseTextEncoding(arguments[1], "Path.write_text()", span)
                : TextEncodingMode.Utf8;

            if (arguments.Length >= 3)
            {
                ValidateStrictTextErrors(arguments[2], "Path.write_text()", span);
            }

            if (arguments.Length == 4)
            {
                ValidatePathNewline(arguments[3], "Path.write_text()", span);
            }

            return (PyStringOps.NormalizeNewlines(text), encodingMode);
        }

        private static void ValidateStrictTextErrors(object value, string owner, LythonSourceSpan span)
        {
            if (value is null or PyNone)
            {
                return;
            }

            if (!PyStringOps.TryAsString(value, out var errors) ||
                !errors.AsString().Equals("strict", StringComparison.OrdinalIgnoreCase))
            {
                throw new LythonRuntimeException("ValueError", $"{owner} only supports errors='strict'.", span);
            }
        }

        private static void ValidatePathNewline(object value, string owner, LythonSourceSpan span)
        {
            if (value is null or PyNone)
            {
                return;
            }

            if (!PyStringOps.TryAsString(value, out var newline) || newline.Length != 0)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} only supports newline=''.", span);
            }
        }

        private static PyString ReadPathText(string path, TextEncodingMode encodingMode, ExecutionContext context, LythonSourceSpan span)
        {
            var text = StripUtf8Bom(ReadGovernedHostText(path, context, span), encodingMode);
            context.ObserveString(text, span);
            return text;
        }

        private static async ValueTask<PyString> ReadPathTextAsync(string path, TextEncodingMode encodingMode, ExecutionContext context, LythonSourceSpan span)
        {
            var text = StripUtf8Bom(await ReadGovernedHostTextAsync(path, context, span).ConfigureAwait(false), encodingMode);
            context.ObserveString(text, span);
            return text;
        }

        private static PyString StripUtf8Bom(PyString text, TextEncodingMode encodingMode)
        {
            if (encodingMode != TextEncodingMode.Utf8Bom)
            {
                return text;
            }

            var decoded = text.AsString();
            return decoded.Length > 0 && decoded[0] == '\uFEFF'
                ? PyString.FromString(decoded[1..])
                : text;
        }

        private static byte[] EncodePathText(PyString text, TextEncodingMode encodingMode)
        {
            var utf8 = PyStringOps.EncodeUtf8(text);
            return encodingMode == TextEncodingMode.Utf8Bom
                ? [.. Utf8Bom, .. utf8]
                : utf8;
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

        private static async ValueTask EnumerateRecursiveAsync(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span, PyList results)
        {
            context.RegisterHostCall(span);
            var names = await context.HostListDirAsync(root.AsString(), span).ConfigureAwait(false);
            foreach (var name in names)
            {
                context.CheckExecutionBudget(span);
                var child = new PyPath(PathOps.Join(root, PyString.FromString(name)));
                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(child.Value.AsString(), span).ConfigureAwait(false);
                if (stat.IsDir)
                {
                    await EnumerateRecursiveAsync(child.Value, pattern, context, span, results).ConfigureAwait(false);
                    continue;
                }

                if (stat.IsFile && MatchRglobPattern(name, pattern))
                {
                    results.Add(child);
                    context.ObserveCollectionCount(results.Count, span);
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
                "close" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.close() expects no arguments.", span);
                    }

                    handle.Exit();
                    return PyNone.Instance;
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.close() expects no arguments.", span);
                    }

                    await handle.ExitAsync().ConfigureAwait(false);
                    return PyNone.Instance;
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
                "args" => process.Args,
                "returncode" => process.ReturnCode,
                "stdout" => process.Stdout,
                "stderr" => process.Stderr,
                "check_returncode" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "CompletedProcess.check_returncode() expects no arguments.", span);
                    }

                    if (process.ReturnCode != BigInteger.Zero)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"subprocess.CompletedProcess failed with return code {process.ReturnCode}.", span, payload: process);
                    }

                    return PyNone.Instance;
                }),
                _ => null!
            };

            return value is not null;
        }
    }
}
