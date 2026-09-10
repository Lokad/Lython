using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class UnsupportedOsCallableObject : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        private readonly string _message;

        public UnsupportedOsCallableObject(string name, string message)
        {
            Name = name;
            _message = message;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", _message, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static string GetEnvironmentKey(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var key))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(...) expects environment keys to be strings.", span);
        }

        return key.AsString();
    }

    private static string GetEnvironmentValue(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(...) expects environment values to be strings.", span);
        }

        return text.AsString();
    }

    private static string ExpandVars(string path, IReadOnlyDictionary<string, string> environment)
    {
        if (path.Length == 0)
        {
            return path;
        }

        var builder = new StringBuilder(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            var ch = path[i];
            if (ch == '$')
            {
                if (i + 1 < path.Length && path[i + 1] == '{')
                {
                    var end = path.IndexOf('}', i + 2);
                    if (end >= 0)
                    {
                        var name = path[(i + 2)..end];
                        builder.Append(environment.TryGetValue(name, out var value) ? value : path[i..(end + 1)]);
                        i = end;
                        continue;
                    }

                    // No later braced variable can close once the remaining suffix
                    // contains no '}', so preserve it without rescanning that suffix.
                    builder.Append(path.AsSpan(i));
                    break;
                }
                else
                {
                    var start = i + 1;
                    var end = start;
                    while (end < path.Length && (char.IsAsciiLetterOrDigit(path[end]) || path[end] == '_'))
                    {
                        end++;
                    }

                    if (end > start)
                    {
                        var name = path[start..end];
                        builder.Append(environment.TryGetValue(name, out var value) ? value : path[i..end]);
                        i = end - 1;
                        continue;
                    }
                }
            }
            else if (ch == '%')
            {
                var end = path.IndexOf('%', i + 1);
                if (end > i + 1)
                {
                    var name = path[(i + 1)..end];
                    builder.Append(environment.TryGetValue(name, out var value) ? value : path[i..(end + 1)]);
                    i = end;
                    continue;
                }
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }


    internal sealed class PyEnvironmentMapping :
        IMutablePySubscriptableValue,
        IDeletablePySubscriptableValue,
        IPyTruthyValue,
        IPyIterableValue,
        IPyRenderableValue,
        IPyDynamicAttributes,
        IEnumerable<object>
    {
        private readonly Dictionary<string, string> _items;
        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan? _allocationSpan;

        public PyEnvironmentMapping(Dictionary<string, string> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        {
            _items = items;
            _governor = governor;
            _allocationSpan = allocationSpan;
        }

        // Host-owned table strings convert on read through the stored governor,
        // so retained copies accumulate while the table itself stays host-owned.
        private PyString EnvString(string text) => PyString.FromString(text, _governor, _allocationSpan);

        public bool TryGetString(string key, [MaybeNullWhen(false)] out string value) => _items.TryGetValue(key, out value);

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var key = GetEnvironmentKey(index, "os.environ.__getitem__", span);
            if (!_items.TryGetValue(key, out var value))
            {
                throw new LythonRuntimeException("KeyError", $"Key '{key}' was not found.", span);
            }

            return EnvString(value);
        }

        public void SetSubscript(object index, object value, LythonSourceSpan span)
        {
            var key = GetEnvironmentKey(index, "os.environ.__setitem__", span);
            _items[key] = GetEnvironmentValue(value, "os.environ.__setitem__", span);
        }

        public void DeleteSubscript(object index, LythonSourceSpan span)
        {
            var key = GetEnvironmentKey(index, "os.environ.__delitem__", span);
            if (!_items.Remove(key))
            {
                throw new LythonRuntimeException("KeyError", $"Key '{key}' was not found.", span);
            }
        }

        public bool IsTruthy() => _items.Count != 0;

        public IEnumerable<object> Iterate() => _items.Keys.Select<string, object>(EnvString);

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "get" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.get(key[, default]) expects one key and an optional default.", span);
                    }

                    var key = GetEnvironmentKey(arguments[0], "os.environ.get", span);
                    return _items.TryGetValue(key, out var found)
                        ? EnvString(found)
                        : arguments.Length == 2 ? arguments[1] : PyNone.Instance;
                }, "os.environ.get", ["key", "default"], 1),
                "keys" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.keys() expects no arguments.", span);
                    }

                    var result = new PyList(_items.Keys.Select<string, object>(EnvString), context.MemoryGovernor, span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.keys", []),
                "values" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.values() expects no arguments.", span);
                    }

                    var result = new PyList(_items.Values.Select<string, object>(EnvString), context.MemoryGovernor, span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.values", []),
                "items" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.items() expects no arguments.", span);
                    }

                    var result = new PyList(
                        _items.Select(pair => (object)new PyTuple(
                            [EnvString(pair.Key), EnvString(pair.Value)],
                            context.MemoryGovernor,
                            span)),
                        context.MemoryGovernor,
                        span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.items", []),
                "copy" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.copy() expects no arguments.", span);
                    }

                    var result = ToPyDict(context, span);
                    context.ObserveCollectionCount(result.Count, span);
                    return result;
                }, "os.environ.copy", []),
                "clear" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.clear() expects no arguments.", span);
                    }

                    _items.Clear();
                    return PyNone.Instance;
                }, "os.environ.clear", []),
                "update" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || arguments[0] is not PyDict source)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.update(mapping) expects one dictionary argument.", span);
                    }

                    foreach (var pair in source)
                    {
                        var key = GetEnvironmentKey(pair.Key, "os.environ.update", span);
                        _items[key] = GetEnvironmentValue(pair.Value, "os.environ.update", span);
                    }

                    return PyNone.Instance;
                }, "os.environ.update", ["mapping"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context) => ToPyDict(context.Context, null).RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private PyDict ToPyDict(ExecutionContext context, LythonSourceSpan? span)
        {
            var result = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in _items)
            {
                result.SetItem(EnvString(pair.Key), EnvString(pair.Value));
            }

            return result;
        }
    }

}
