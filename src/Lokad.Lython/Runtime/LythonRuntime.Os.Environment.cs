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

    private static PyEnvironmentMapping GetEnvironmentMapping(ExecutionContext context, LythonSourceSpan span)
        => context.State.OsEnvironMapping ??= new PyEnvironmentMapping(context.State.Environment, context.MemoryGovernor, span);

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
        IEnumerable<object>, IPyDictStorage, IPySizedValue
    {
        // Initial strings are borrowed; guest mutations and yielded copies are
        // execution-owned. Retained table capacity survives deletion/clear.

        private readonly Dictionary<string, string> _items;
        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _allocationSpan;
        private PyDict? _views;

        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature EnvironGetSignature = LythonCallableSignature.Create("os.environ.get", ["key", "default"], 1);
        private static readonly LythonCallableSignature EnvironCopySignature = LythonCallableSignature.Create("os.environ.copy", []);
        private static readonly LythonCallableSignature EnvironClearSignature = LythonCallableSignature.Create("os.environ.clear", []);
        private static readonly LythonCallableSignature EnvironUpdateSignature = LythonCallableSignature.Create("os.environ.update", ["mapping"]);

        public PyEnvironmentMapping(Dictionary<string, string> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        {
            _items = items;
            _governor = governor;
            _allocationSpan = allocationSpan ?? new LythonSourceSpan(0, 0, 1, 1);
        }

        // Each yielded key/value owns its converted Python storage.
        private PyString EnvString(string text) => PyString.FromString(text, _governor, _allocationSpan);

        public bool TryGetString(string key, [MaybeNullWhen(false)] out string value) => _items.TryGetValue(key, out value);

        private MemoryGovernor.TemporaryMemoryReservation KeyScratch(object key, LythonSourceSpan? span)
            => _governor.ReserveTemporary(key is PyString text ? 32L + 2L * text.Utf8Bytes.Length : 0, span);

        public object GetDefault(object key, object fallback, LythonSourceSpan span)
            => TryRead(key, span, out var value) ? value : fallback;

        private bool TryRead(object index, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            using var decoding = KeyScratch(index, span);
            var key = GetEnvironmentKey(index, "os.environ", span);
            if (_items.TryGetValue(key, out var found)) { value = EnvString(found); return true; }
            value = null; return false;
        }

        public object GetSubscript(object index, LythonSourceSpan span)
            => TryRead(index, span, out var value) ? value : throw RuntimeErrors.MissingKey(index, span);

        public void SetSubscript(object index, object value, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(index, out var key) || !PyStringOps.TryAsString(value, out var text))
                throw RuntimeErrors.Type("environment keys and values must be strings", span);
            if (key.Utf8Bytes.IsEmpty) throw new LythonRuntimeException("OSError", "Invalid environment variable name", span);
            if (key.Utf8Bytes.Span.Contains((byte)'=') || key.Utf8Bytes.Span.Contains((byte)0) || text.Utf8Bytes.Span.Contains((byte)0))
                throw new LythonRuntimeException("ValueError", "environment names cannot contain '=' or NUL and values cannot contain NUL", span);
            var strings = checked(128L + 2L * (key.Utf8Bytes.Length + (long)text.Utf8Bytes.Length));
            // Per-entry headroom funds retained table capacity and replacement
            // growth. Old decoded values may remain aliased by retained requests.
            using var construction = _governor.ReserveTemporary(checked(strings + 256), span);
            var decodedKey = key.AsString();
            var decodedValue = text.AsString();
            var added = !_items.ContainsKey(decodedKey);
            _items[decodedKey] = decodedValue;
            construction.Commit(strings + (added ? 256 : 0));
            if (added) _views?.NotifyNamespaceMutation();
        }

        public bool RemoveKey(object index, LythonSourceSpan span)
        {
            using var decoding = KeyScratch(index, span);
            var removed = _items.Remove(GetEnvironmentKey(index, "os.environ", span));
            if (removed) _views?.NotifyNamespaceMutation();
            return removed;
        }
        public void DeleteSubscript(object index, LythonSourceSpan span)
        { if (!RemoveKey(index, span)) throw RuntimeErrors.MissingKey(index, span); }

        private void Clear()
        {
            if (_items.Count != 0) { _items.Clear(); _views?.NotifyNamespaceMutation(); }
        }
        public int Count => _items.Count;
        public int Length => Count;

        public bool IsTruthy() => _items.Count != 0;

        public IEnumerable<object> Iterate() => _items.Keys.Select<string, object>(EnvString);

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "keys" or "values" or "items")
            {
                if (_views is null)
                {
                    using var storage = _governor.ReserveTemporary(128, _allocationSpan);
                    _views = new PyDict(this, _governor, _allocationSpan);
                    storage.Commit(128);
                }
                return DictMembers.TryGetMember(_views, name, out value);
            }
            value = name switch
            {
                "get" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.get(key[, default]) expects one key and an optional default.", span);
                    }

                    return GetDefault(arguments[0], arguments.Length == 2 ? arguments[1] : PyNone.Instance, span);
                }, EnvironGetSignature),
                "copy" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.copy() expects no arguments.", span);
                    }

                    var result = ToPyDict(context, span);
                    context.CheckExecution(span);
                    return result;
                }, EnvironCopySignature),
                "clear" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.clear() expects no arguments.", span);
                    }

                    Clear();
                    return PyNone.Instance;
                }, EnvironClearSignature),
                "update" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || arguments[0] is not PyDict source)
                    {
                        throw new LythonRuntimeException("TypeError", "os.environ.update(mapping) expects one dictionary argument.", span);
                    }

                    foreach (var pair in source)
                    {
                        context.CheckExecution(span);
                        SetSubscript(pair.Key, pair.Value, span);
                    }

                    return PyNone.Instance;
                }, EnvironUpdateSignature),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        IEnumerable<object> IPyDictStorage.Keys => Iterate();
        IEnumerable<object> IPyDictStorage.Values => _items.Values.Select<string, object>(EnvString);
        object IPyDictStorage.GetRequired(object key) => GetSubscript(key, _allocationSpan);
        bool IPyDictStorage.TryGetValue(object key, out object value) => TryRead(key, _allocationSpan, out value!);
        bool IPyDictStorage.ContainsKey(object key)
        {
            using var decoding = KeyScratch(key, _allocationSpan);
            return _items.ContainsKey(GetEnvironmentKey(key, "os.environ", _allocationSpan));
        }
        bool IPyDictStorage.SetItem(object key, object value)
        {
            var added = !((IPyDictStorage)this).ContainsKey(key);
            SetSubscript(key, value, _allocationSpan);
            return added;
        }
        bool IPyDictStorage.TrySetExisting(object key, object value)
        {
            if (!((IPyDictStorage)this).ContainsKey(key)) return false;
            SetSubscript(key, value, _allocationSpan); return true;
        }
        void IPyDictStorage.AddNew(object key, object value) => SetSubscript(key, value, _allocationSpan);
        bool IPyDictStorage.Remove(object key) => RemoveKey(key, _allocationSpan);
        void IPyDictStorage.Clear() => Clear();
        IPyDictStorage IPyDictStorage.Clone()
        {
            using var working = _governor.ReserveTemporary(64L + 16L * Count, _allocationSpan);
            return PyDictStorage.Create((IEnumerable<KeyValuePair<object, object>>)this, _governor, _allocationSpan);
        }
        long IPyDictStorage.CommittedBytes => 0; // The shared execution owner retains native backing.
        long IPyDictStorage.ReleaseCommittedBytes() => throw new InvalidOperationException("A live environment retains its backing.");
        IEnumerator<KeyValuePair<object, object>> IEnumerable<KeyValuePair<object, object>>.GetEnumerator()
            => _items.Select(pair => new KeyValuePair<object, object>(EnvString(pair.Key), EnvString(pair.Value))).GetEnumerator();

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
