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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
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

            using var sorted = SortItems(list, keyCallable as ICallable, reverse, span, context);
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

            using var sorted = await SortItemsAsync(list, keyCallable as ICallable, reverse, span, context).ConfigureAwait(false);
            list.ReplaceAll(sorted);
            return PyNone.Instance;
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
        public static bool TryGetMember(PyDict dict, string name, [MaybeNullWhen(false)] out object value)
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class DefaultDictMembers
    {
        public static bool TryGetMember(PyDefaultDict dict, string name, [MaybeNullWhen(false)] out object value)
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

}
