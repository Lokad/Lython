using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
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
                    list.AddRange(ToSequence(arguments[0], span, context).ToArray());
                    context.ObserveCollectionCount(list.Count, span);
                    return PyNone.Instance;
                }, "list.extend", ["iterable"]),
                "index" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "list.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = NormalizeListSearchBound(arguments.Length >= 2 ? arguments[1] : null, list.Count, 0, span);
                    var stop = NormalizeListSearchBound(arguments.Length >= 3 ? arguments[2] : null, list.Count, list.Count, span);
                    for (var i = start; i < stop; i++)
                    {
                        if (AreEqual(list[i], arguments[0]))
                        {
                            return new BigInteger(i);
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "list.index(value): value is not in list", span);
                }, "list.index", ["value", "start", "stop"], 1),
                "count" => new BoundCallable((arguments, span, _) =>
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
                "insert" => new BoundCallable((arguments, span, context) =>
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
                "remove" => new BoundCallable((arguments, span, _) =>
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
                "pop" => new BoundCallable((arguments, span, _) =>
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
                "reverse" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "list.reverse() expects no arguments.", span);
                    }

                    list.Reverse();
                    return PyNone.Instance;
                }),
                "sort" => new BoundCallable(
                    (arguments, span, context) => SortList(list, arguments, span, context),
                    new LythonCallableSignature("list.sort", ["key", "reverse"], RequiredCount: 0, MaxPositionalCount: 0),
                    (arguments, span, context) => SortListAsync(list, arguments, span, context)),
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

        private static object SortList(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var keyCallable = arguments.Length >= 1 ? arguments[0] : null;
            if (keyCallable is not null &&
                !ReferenceEquals(keyCallable, PyNone.Instance) &&
                keyCallable is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", "list.sort(..., key=...) expects a callable or None.", span);
            }

            var reverse = false;
            if (arguments.Length >= 2)
            {
                reverse = IsTruthy(arguments[1]);
            }

            var sorted = SortListItems([.. list], keyCallable, reverse, span, context);
            list.ReplaceAll(sorted);
            return PyNone.Instance;
        }

        private static async ValueTask<object> SortListAsync(PyList list, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var keyCallable = arguments.Length >= 1 ? arguments[0] : null;
            if (keyCallable is not null &&
                !ReferenceEquals(keyCallable, PyNone.Instance) &&
                keyCallable is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", "list.sort(..., key=...) expects a callable or None.", span);
            }

            var reverse = false;
            if (arguments.Length >= 2)
            {
                reverse = IsTruthy(arguments[1]);
            }

            var sorted = await SortListItemsAsync([.. list], keyCallable, reverse, span, context).ConfigureAwait(false);
            list.ReplaceAll(sorted);
            return PyNone.Instance;
        }

        private static object[] SortListItems(
            object[] values,
            object? keyCallable,
            bool reverse,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var keyed = new List<SortKeyValue>(values.Length);
            foreach (var item in values)
            {
                keyed.Add(new SortKeyValue(
                    item,
                    keyCallable is ICallable callable
                        ? callable.Invoke([new CallArgumentValue(null, item)], span, context)
                        : item));
            }

            SortKeyedItems(keyed, reverse, span, context);

            return keyed.Select(item => item.Value).ToArray();
        }

        private static async ValueTask<object[]> SortListItemsAsync(
            object[] values,
            object? keyCallable,
            bool reverse,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var keyed = new List<SortKeyValue>(values.Length);
            foreach (var item in values)
            {
                keyed.Add(new SortKeyValue(
                    item,
                    keyCallable is ICallable callable
                        ? await callable.InvokeAsync([new CallArgumentValue(null, item)], span, context).ConfigureAwait(false)
                        : item));
            }

            await SortKeyedItemsAsync(keyed, reverse, span, context).ConfigureAwait(false);

            return keyed.Select(item => item.Value).ToArray();
        }

        private static void SortKeyedItems(List<SortKeyValue> keyed, bool reverse, LythonSourceSpan span, ExecutionContext context)
        {
            for (var i = 1; i < keyed.Count; i++)
            {
                var current = keyed[i];
                var j = i - 1;
                while (j >= 0 && (reverse
                    ? CompareSortKeys(keyed[j].Key, current.Key, span, context) < 0
                    : CompareSortKeys(keyed[j].Key, current.Key, span, context) > 0))
                {
                    keyed[j + 1] = keyed[j];
                    j--;
                }

                keyed[j + 1] = current;
            }
        }

        private static async ValueTask SortKeyedItemsAsync(List<SortKeyValue> keyed, bool reverse, LythonSourceSpan span, ExecutionContext context)
        {
            for (var i = 1; i < keyed.Count; i++)
            {
                var current = keyed[i];
                var j = i - 1;
                while (j >= 0 && (reverse
                    ? await CompareSortKeysAsync(keyed[j].Key, current.Key, span, context).ConfigureAwait(false) < 0
                    : await CompareSortKeysAsync(keyed[j].Key, current.Key, span, context).ConfigureAwait(false) > 0))
                {
                    keyed[j + 1] = keyed[j];
                    j--;
                }

                keyed[j + 1] = current;
            }
        }

        private static int NormalizeListSearchBound(object? value, int length, int defaultValue, LythonSourceSpan span)
        {
            if (value is null)
            {
                return defaultValue;
            }

            var integer = ExpectInteger(value, "list.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
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
                "update" => new RawBoundCallable((arguments, span, context) => UpdateDictionary(dict, arguments, span, context)),
                "pop" => new BoundCallable((arguments, span, context) =>
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
                }, "dict.pop", ["key", "default"], 1),
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
                "update" => new CounterUpdateCallable(counter, subtract: false),
                "subtract" => new CounterUpdateCallable(counter, subtract: true),
                "total" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Counter.total() expects no arguments.", span);
                    }

                    object total = BigInteger.Zero;
                    foreach (var pair in counter.Items)
                    {
                        total = AddCounterCounts(total, ExpectCounterCount(pair.Value, span), span);
                    }

                    return total;
                }, "Counter.total", []),
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
                    sortedItems.Sort((left, right) => CompareCounterCounts(right.Value, left.Value, span));

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
                    if (argument.Name is null)
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

                    if (argument.Name is "iterable" or "mapping")
                    {
                        if (hasSource)
                        {
                            throw new LythonRuntimeException("TypeError", $"Counter.{Name}(...) got multiple values for iterable.", span);
                        }

                        source = argument.Value;
                        hasSource = true;
                        continue;
                    }

                    keywordItems.Add(new(argument.Name, argument.Value));
                }

                if (hasSource)
                {
                    try
                    {
                        PopulateCounter(_counter, source!, span, context, _subtract);
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
                _ => null!,
            };

            return value is not null;
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
        public static bool TryGetMember(PySet set, string name, out object value)
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
                _ => null!,
            };

            return value is not null;
        }

        private static LythonCallableSignature NoArguments(string name) => new(name, []);

        private static LythonCallableSignature OnePositional(string name, string parameterName)
            => new(name, [parameterName], PositionalOnlyCount: 1);

        private static LythonCallableSignature VariadicPositional(string name)
            => new(name, RequiredCount: 0, AllowsExtraPositional: true);

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
                "st_mode" or "st_ino" or "st_dev" or "st_nlink" or "st_uid" or "st_gid"
                    => throw new LythonRuntimeException("NotImplementedError", "Rich stat_result metadata is not supported by Lython because the host path model only exposes existence, kind, size, and modified time.", null),
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
                "type" => PyString.FromString(exception.TypeName),
                "message" => PyString.FromString(exception.Message),
                "args" => CreateExceptionArgs(exception),
                "code" when string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal) => exception.Value,
                _ => null!,
            };

            if (value is null &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString(name), out var payloadValue))
            {
                value = payloadValue;
            }

            return value is not null;
        }

        private static PyTuple CreateExceptionArgs(PyException exception)
        {
            if (exception.ExplicitArgs is not null)
            {
                return exception.ExplicitArgs;
            }

            if (string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal))
            {
                return ReferenceEquals(exception.Value, PyNone.Instance)
                    ? PyTuple.Empty
                    : new PyTuple([exception.Value]);
            }

            if (string.Equals(exception.TypeName, "JSONDecodeError", StringComparison.Ordinal) &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString("msg"), out var msg) &&
                payload.TryGetValue(PyString.FromString("doc"), out var doc) &&
                payload.TryGetValue(PyString.FromString("pos"), out var pos))
            {
                return new PyTuple([msg, doc, pos]);
            }

            if (string.Equals(exception.TypeName, "CalledProcessError", StringComparison.Ordinal) &&
                exception.Value is PyDict subprocessPayload &&
                subprocessPayload.TryGetValue(PyString.FromString("returncode"), out var returnCode) &&
                subprocessPayload.TryGetValue(PyString.FromString("cmd"), out var command))
            {
                return new PyTuple([returnCode, command]);
            }

            return exception.Value switch
            {
                PyNone => PyTuple.Empty,
                _ => new PyTuple([exception.Value])
            };
        }
    }

    internal static class DecimalMembers
    {
        public static bool TryGetMember(PyDecimal decimalValue, string name, out object value)
        {
            value = name switch
            {
                "quantize" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3 || arguments[0] is not PyDecimal exponent)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.quantize(exp[, rounding][, context]) expects a Decimal exponent plus optional rounding/context.", span);
                    }

                    var rounding = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
                    var decimalContext = arguments.Length >= 3 && arguments[2] is not PyNone
                        ? arguments[2] as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal.quantize(..., context=...) expects a Context or None.", span)
                        : context.DecimalContext;
                    return PyDecimalOps.Quantize(decimalValue, exponent, rounding, decimalContext, span);
                }, new LythonCallableSignature("Decimal.quantize", ["exp", "rounding", "context"], RequiredCount: 1)),
                "normalize" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("normalize", decimalValue, arguments, span), new LythonCallableSignature("Decimal.normalize", ["context"], RequiredCount: 0)),
                "sqrt" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("sqrt", decimalValue, arguments, span), new LythonCallableSignature("Decimal.sqrt", ["context"], RequiredCount: 0)),
                "exp" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("exp", decimalValue, arguments, span), new LythonCallableSignature("Decimal.exp", ["context"], RequiredCount: 0)),
                "ln" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("ln", decimalValue, arguments, span), new LythonCallableSignature("Decimal.ln", ["context"], RequiredCount: 0)),
                "log10" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("log10", decimalValue, arguments, span), new LythonCallableSignature("Decimal.log10", ["context"], RequiredCount: 0)),
                "copy_abs" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_abs", decimalValue, arguments, span)),
                "copy_negate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_negate", decimalValue, arguments, span)),
                "copy_sign" => new BoundCallable((arguments, span, _) => PyDecimalOps.CopySign(decimalValue, arguments, span), "Decimal.copy_sign", ["other"]),
                "to_integral_value" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral_value", ["rounding", "context"], RequiredCount: 0)),
                "to_integral_exact" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral_exact", ["rounding", "context"], RequiredCount: 0)),
                "to_integral" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral", ["rounding", "context"], RequiredCount: 0)),
                "as_tuple" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.as_tuple() expects no arguments.", span);
                    }

                    return PyDecimalOps.AsTuple(decimalValue);
                }, "Decimal.as_tuple", []),
                "adjusted" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.adjusted() expects no arguments.", span);
                    }

                    return PyDecimalOps.Adjusted(decimalValue);
                }, "Decimal.adjusted", []),
                "compare" => new BoundCallable((arguments, span, _) => PyDecimalOps.CompareValue(decimalValue, arguments, span), new LythonCallableSignature("Decimal.compare", ["other", "context"], RequiredCount: 1)),
                "compare_total" => new BoundCallable((arguments, span, _) => PyDecimalOps.CompareTotal(decimalValue, arguments, span), "Decimal.compare_total", ["other"]),
                "is_nan" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_nan", arguments, span, false), "Decimal.is_nan", []),
                "is_infinite" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_infinite", arguments, span, false), "Decimal.is_infinite", []),
                "is_finite" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_finite", arguments, span, true), "Decimal.is_finite", []),
                "is_zero" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_zero", arguments, span, decimalValue.Value == 0m), "Decimal.is_zero", []),
                "is_signed" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_signed", arguments, span, decimalValue.IsSigned), "Decimal.is_signed", []),
                "to_eng_string" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.to_eng_string() expects no arguments.", span);
                    }

                    return PyDecimalOps.ToEngineeringString(decimalValue);
                }, "Decimal.to_eng_string", []),
                "scaleb" => new BoundCallable((arguments, span, _) => PyDecimalOps.ScaleB(decimalValue, arguments, span), new LythonCallableSignature("Decimal.scaleb", ["other", "context"], RequiredCount: 1)),
                "shift" => new BoundCallable((arguments, span, _) => PyDecimalOps.Shift(decimalValue, arguments, span), "Decimal.shift", ["other"]),
                "rotate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Rotate(decimalValue, arguments, span), "Decimal.rotate", ["other"]),
                "same_quantum" => new BoundCallable((arguments, span, _) => PyDecimalOps.SameQuantum(decimalValue, arguments, span), "Decimal.same_quantum", ["other"]),
                "remainder_near" => new BoundCallable((arguments, span, _) => PyDecimalOps.RemainderNear(decimalValue, arguments, span), new LythonCallableSignature("Decimal.remainder_near", ["other", "context"], RequiredCount: 1)),
                "min" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "min", span), new LythonCallableSignature("Decimal.min", ["other", "context"], RequiredCount: 1)),
                "max" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "max", span), new LythonCallableSignature("Decimal.max", ["other", "context"], RequiredCount: 1)),
                "min_mag" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "min_mag", span), new LythonCallableSignature("Decimal.min_mag", ["other", "context"], RequiredCount: 1)),
                "max_mag" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "max_mag", span), new LythonCallableSignature("Decimal.max_mag", ["other", "context"], RequiredCount: 1)),
                _ => null!,
            };

            return value is not null;
        }

        private static bool ExpectDecimalNoArguments(string name, object[] arguments, LythonSourceSpan span, bool result)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"Decimal.{name}() expects no arguments.", span);
            }

            return result;
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
                "suffixes" => PathSuffixes(path.Value),
                "stem" => PyString.FromString(PathOps.Stem(path.Value.AsString())),
                "parent" => new PyPath(PathOps.Parent(path.Value)),
                "parents" => PathOps.Parents(path.Value),
                "parts" => PathOps.Parts(path.Value),
                "drive" => PyString.Empty,
                "root" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                "anchor" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                "__fspath__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.__fspath__() expects no arguments.", span);
                    }

                    return path.Value;
                }, "Path.__fspath__", []),
                "is_absolute" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_absolute() expects no arguments.", span);
                    }

                    return PathOps.IsAbsolute(path.Value.AsString());
                }),
                "is_mount" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_mount() expects no arguments.", span);
                    }

                    return string.Equals(PathOps.Normalize(path.Value.AsString()), "/", StringComparison.Ordinal);
                }, "Path.is_mount", []),
                "is_reserved" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_reserved() expects no arguments.", span);
                    }

                    return false;
                }, "Path.is_reserved", []),
                "joinpath" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length == 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects at least one string argument.", span);
                    }

                    var current = path.Value;
                    foreach (var argument in arguments)
                    {
                        var part = argument switch
                        {
                            PyPath pathArgument => pathArgument.Value,
                            _ when PyStringOps.TryAsString(argument, out var text) => text,
                            _ => throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects Path or string arguments.", span)
                        };

                        if (part.Length == 0)
                        {
                            continue;
                        }

                        current = PathOps.Join(current, part);
                    }

                    return new PyPath(current);
                }),
                "expanduser" => UnsupportedPathMember("Path.expanduser", "Path.expanduser() is not supported by Lython; the host does not expose an ambient user home directory."),
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
                "lstat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }, "Path.lstat", []),
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
                "read_bytes" => UnsupportedPathMember("Path.read_bytes", "Path.read_bytes() is not supported by Lython under the text-only host boundary."),
                "write_bytes" => UnsupportedPathMember("Path.write_bytes", "Path.write_bytes(data) is not supported by Lython under the text-only host boundary."),
                "readlink" => UnsupportedPathMember("Path.readlink", "Path.readlink() is not supported by Lython because symlink targets are not exposed by the host path model."),
                "symlink_to" => UnsupportedPathMember("Path.symlink_to", "Path.symlink_to(target, target_is_directory=False) is not supported by Lython because symlink mutation is outside the host path model."),
                "hardlink_to" => UnsupportedPathMember("Path.hardlink_to", "Path.hardlink_to(target) is not supported by Lython because hardlink mutation is outside the host path model."),
                "chmod" => UnsupportedPathMember("Path.chmod", "Path.chmod(mode) is not supported by Lython because permissions are not exposed by the host path model."),
                "owner" => UnsupportedPathMember("Path.owner", "Path.owner() is not supported by Lython because user ownership is not exposed by the host path model."),
                "group" => UnsupportedPathMember("Path.group", "Path.group() is not supported by Lython because group ownership is not exposed by the host path model."),
                "open" => new PathOpenCallable(path.Value.AsString()),
                "glob" => new BoundCallable((arguments, span, context) =>
                {
                    var pattern = ParsePathGlobArguments(arguments, "Path.glob", span);

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
                    var pattern = ParsePathGlobArguments(arguments, "Path.glob", span);

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
                }, "Path.glob", ["pattern", "case_sensitive", "recurse_symlinks"], 1),
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
                    var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                    return ReadPathText(path.Value.AsString(), encodingMode, errors, newline, context, span);
                },
                async (arguments, span, context) =>
                {
                    var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                    return await ReadPathTextAsync(path.Value.AsString(), encodingMode, errors, newline, context, span).ConfigureAwait(false);
                }, "Path.read_text", ["encoding", "errors", "newline"], 0),
                "write_text" => new BoundCallable((arguments, span, context) =>
                {
                    var (text, encodingMode, _, newline) = ParsePathWriteTextArguments(arguments, span);
                    context.ObserveString(text, span);
                    context.RegisterHostCall(span);
                    context.WriteTextUtf8(path.Value.AsString(), EncodePathText(text, encodingMode, newline), span);
                    return new BigInteger(text.Length);
                },
                async (arguments, span, context) =>
                {
                    var (text, encodingMode, _, newline) = ParsePathWriteTextArguments(arguments, span);
                    context.ObserveString(text, span);
                    context.RegisterHostCall(span);
                    await context.WriteTextUtf8Async(path.Value.AsString(), EncodePathText(text, encodingMode, newline), span).ConfigureAwait(false);
                    return new BigInteger(text.Length);
                }, "Path.write_text", ["text", "encoding", "errors", "newline"], 1),
                "rglob" => new BoundCallable((arguments, span, context) =>
                {
                    var pattern = ParsePathGlobArguments(arguments, "Path.rglob", span);

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
                    var pattern = ParsePathGlobArguments(arguments, "Path.rglob", span);

                    var results = new PyList([], context.MemoryGovernor, span);
                    await EnumerateRecursiveAsync(path.Value, pattern, context, span, results).ConfigureAwait(false);
                    return results;
                }, "Path.rglob", ["pattern", "case_sensitive", "recurse_symlinks"], 1),
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

        private static BoundCallable UnsupportedPathMember(string name, string message)
            => new((object[] arguments, LythonSourceSpan span, ExecutionContext context) =>
            {
                _ = arguments;
                _ = context;
                throw new LythonRuntimeException("NotImplementedError", message, span);
            }, name: name);

        private static PyString ParsePathGlobArguments(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(pattern[, case_sensitive][, recurse_symlinks]) expects a string pattern.", span);
            }

            if (arguments.Length >= 2 &&
                arguments[1] is not PyNone &&
                arguments[1] is not true)
            {
                throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., case_sensitive=False) is not supported by Lython's normalized path matcher.", span);
            }

            if (arguments.Length >= 3 &&
                arguments[2] is not PyNone and not false)
            {
                throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., recurse_symlinks=True) is not supported by Lython because symlink traversal is outside the host path model.", span);
            }

            return pattern;
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

        private sealed class PathOpenCallable(string path) : ICallable
        {
            private static readonly string[] ParameterNames = ["mode", "buffering", "encoding", "errors", "newline"];

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var (mode, encodingMode, errors, newline) = ParsePathOpenArguments(BindArguments(arguments, span), span);
                return OpenTextFile(path, mode, encodingMode, errors, newline, span, context);
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var (mode, encodingMode, errors, newline) = ParsePathOpenArguments(BindArguments(arguments, span), span);
                return mode switch
                {
                    "r" => await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    "a" => await LythonRuntime.ExecutionContext.TextFileHandle.ForAppendAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                };
            }

            private static BoundOpenArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
            {
                var bound = new object[ParameterNames.Length];
                Array.Fill(bound, PyNone.Instance);
                var assigned = new bool[ParameterNames.Length];
                var positionalIndex = 0;

                foreach (var argument in arguments)
                {
                    if (argument.Name is null)
                    {
                        if (positionalIndex >= bound.Length)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.open([mode][, buffering][, encoding][, errors][, newline]) received too many positional arguments.", span);
                        }

                        bound[positionalIndex] = argument.Value;
                        assigned[positionalIndex] = true;
                        positionalIndex++;
                        continue;
                    }

                    var index = argument.Name switch
                    {
                        "mode" => 0,
                        "buffering" => 1,
                        "encoding" => 2,
                        "errors" => 3,
                        "newline" => 4,
                        _ => -1
                    };

                    if (index < 0)
                    {
                        throw new LythonRuntimeException("TypeError", $"Path.open([mode][, buffering][, encoding][, errors][, newline]) got an unexpected keyword argument '{argument.Name}'.", span);
                    }

                    if (assigned[index])
                    {
                        throw new LythonRuntimeException("TypeError", $"Path.open([mode][, buffering][, encoding][, errors][, newline]) got multiple values for argument '{ParameterNames[index]}'.", span);
                    }

                    bound[index] = argument.Value;
                    assigned[index] = true;
                }

                var count = bound.Length;
                while (count > 0 && !assigned[count - 1])
                {
                    count--;
                }

                return new BoundOpenArguments(bound, assigned, count);
            }

            private static object OpenTextFile(
                string path,
                string mode,
                TextEncodingMode encodingMode,
                TextErrorMode errors,
                TextNewlineMode newline,
                LythonSourceSpan span,
                ExecutionContext context)
            {
                return mode switch
                {
                    "r" => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(path, context, encodingMode, errors, newline),
                    "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    "a" => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path, context, encodingMode, errors, newline),
                    _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                };
            }
        }

        private static (string Mode, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathOpenArguments(BoundOpenArguments boundArguments, LythonSourceSpan span)
        {
            var arguments = boundArguments.Values;
            if (boundArguments.Count > 5)
            {
                throw new LythonRuntimeException("TypeError", "Path.open([mode][, buffering][, encoding][, errors][, newline]) expects supported text-mode options.", span);
            }

            var mode = boundArguments.Assigned[0]
                ? arguments[0] switch
                {
                    PyString text => text,
                    _ => throw new LythonRuntimeException("TypeError", "Path.open(mode) expects mode to be a string.", span)
                }
                : PyString.FromString("r");

            if (boundArguments.Count >= 2)
            {
                ValidateTextBuffering(arguments[1], "Path.open()", span);
            }

            var encodingMode = boundArguments.Count >= 3
                ? ParseTextEncoding(arguments[2], "Path.open()", span)
                : TextEncodingMode.Utf8;
            var errors = boundArguments.Count >= 4
                ? ParseTextErrors(arguments[3], "Path.open()", span)
                : TextErrorMode.Strict;
            var newline = boundArguments.Count >= 5
                ? ParseTextNewline(arguments[4], "Path.open()", span)
                : TextNewlineMode.TranslateUniversal;

            return (ParseTextOpenMode(mode, "Path.open()", span), encodingMode, errors, newline);
        }

        private static (TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathReadTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.read_text([encoding][, errors][, newline]) expects zero to three arguments.", span);
            }

            var encodingMode = arguments.Length >= 1
                ? ParseTextEncoding(arguments[0], "Path.read_text()", span)
                : TextEncodingMode.Utf8;
            var errors = arguments.Length >= 2
                ? ParseTextErrors(arguments[1], "Path.read_text()", span)
                : TextErrorMode.Strict;
            var newline = arguments.Length >= 3
                ? ParseTextNewline(arguments[2], "Path.read_text()", span)
                : TextNewlineMode.TranslateUniversal;

            return (encodingMode, errors, newline);
        }

        private static (PyString Text, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathWriteTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Path.write_text(text[, encoding][, errors][, newline]) expects a string plus optional keyword-compatible arguments.", span);
            }

            var encodingMode = arguments.Length >= 2
                ? ParseTextEncoding(arguments[1], "Path.write_text()", span)
                : TextEncodingMode.Utf8;
            var errors = arguments.Length >= 3
                ? ParseTextErrors(arguments[2], "Path.write_text()", span)
                : TextErrorMode.Strict;
            var newline = arguments.Length == 4
                ? ParseTextNewline(arguments[3], "Path.write_text()", span)
                : TextNewlineMode.TranslateUniversal;

            return (text, encodingMode, errors, newline);
        }

        private static PyString ReadPathText(
            string path,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var text = StripUtf8Bom(ReadGovernedHostText(path, context, span, errors, newline), encodingMode);
            context.ObserveString(text, span);
            return text;
        }

        private static async ValueTask<PyString> ReadPathTextAsync(
            string path,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var text = StripUtf8Bom(await ReadGovernedHostTextAsync(path, context, span, errors, newline).ConfigureAwait(false), encodingMode);
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

        private static byte[] EncodePathText(PyString text, TextEncodingMode encodingMode, TextNewlineMode newline)
            => EncodeUtf8Text(text, encodingMode, newline);

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
                "closed" => handle.IsClosed,
                "name" => PyString.FromString(handle.Path),
                "mode" => PyString.FromString(handle.Mode),
                "encoding" => PyString.FromString(handle.EncodingName),
                "errors" => PyString.FromString(handle.ErrorsName),
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
                "readable" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readable() expects no arguments.", span);
                    }

                    return handle.IsReadable();
                }, "file.readable", []),
                "writable" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.writable() expects no arguments.", span);
                    }

                    return handle.IsWritable();
                }, "file.writable", []),
                "seekable" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.seekable() expects no arguments.", span);
                    }

                    return handle.IsSeekable();
                }, "file.seekable", []),
                "tell" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.tell() expects no arguments.", span);
                    }

                    return handle.Tell();
                }, "file.tell", []),
                "seek" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "file.seek(offset[, whence]) expects one or two arguments.", span);
                    }

                    return handle.Seek(span);
                }, "file.seek", ["offset", "whence"], 1),
                "read" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.read([size]) expects zero or one integer argument.", span);
                    }

                    return handle.Read(ParseOptionalSize(arguments, "file.read([size])", span));
                }, "file.read", ["size"], 0),
                "readline" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readline([size]) expects zero or one integer argument.", span);
                    }

                    return handle.ReadLine(ParseOptionalSize(arguments, "file.readline([size])", span));
                }, "file.readline", ["size"], 0),
                "readlines" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readlines([hint]) expects zero or one integer argument.", span);
                    }

                    return handle.ReadLines(ParseOptionalSize(arguments, "file.readlines([hint])", span));
                }, "file.readlines", ["hint"], 0),
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
                "flush" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.flush() expects no arguments.", span);
                    }

                    return handle.Flush();
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.flush() expects no arguments.", span);
                    }

                    return await handle.FlushAsync().ConfigureAwait(false);
                }, "file.flush", []),
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
                "check_returncode" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "CompletedProcess.check_returncode() expects no arguments.", span);
                    }

                    if (process.ReturnCode != BigInteger.Zero)
                    {
                        throw CreateCalledProcessError(
                            process.ReturnCode,
                            process.Args,
                            process.Stdout,
                            process.Stderr,
                            $"subprocess.CompletedProcess failed with return code {process.ReturnCode}.",
                            context,
                            span);
                    }

                    return PyNone.Instance;
                }),
                _ => null!
            };

            return value is not null;
        }
    }
}
