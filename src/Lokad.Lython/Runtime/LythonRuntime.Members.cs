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
                "update" => new RawBoundCallable((arguments, span, context) => UpdateDictionary(dict, arguments, span, context)),
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
                "keys" => BoundCallable.CreateNoArguments(
                    dict,
                    "defaultdict.keys",
                    static (receiver, span, context) => new PyList(receiver.Keys, context.MemoryGovernor, span)),
                "values" => BoundCallable.CreateNoArguments(
                    dict,
                    "defaultdict.values",
                    static (receiver, span, context) => new PyList(receiver.Values, context.MemoryGovernor, span)),
                "items" => BoundCallable.CreateNoArguments(
                    dict,
                    "defaultdict.items",
                    static (receiver, span, context) => BuildItemsList(receiver, context, span)),
                "update" => new RawBoundCallable((arguments, span, context) => dict.UpdateFrom(arguments, context, span)),
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
                "keys" => BoundCallable.CreateNoArguments(
                    counter,
                    "Counter.keys",
                    static (receiver, span, context) => new PyList(receiver.Keys, context.MemoryGovernor, span)),
                "values" => BoundCallable.CreateNoArguments(
                    counter,
                    "Counter.values",
                    static (receiver, span, context) => new PyList(receiver.Values, context.MemoryGovernor, span)),
                "items" => BoundCallable.CreateNoArguments(
                    counter,
                    "Counter.items",
                    static (receiver, span, context) => BuildItemsList(receiver, context, span)),
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
                items[index++] = PyTuple.FromOwnedArray([pair.Key, pair.Value], context.MemoryGovernor, span);
            }

            return new PyList(items, context.MemoryGovernor, span);
        }

        var list = new List<object>();
        foreach (var pair in pairs)
        {
            list.Add(PyTuple.FromOwnedArray([pair.Key, pair.Value], context.MemoryGovernor, span));
        }

        return new PyList(list, context.MemoryGovernor, span);
    }

}
