using System.Collections;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Values stay in the actual attribute table. Only exposed key labels are cached.
internal sealed class ObjectNamespaceStorage : IPyDictStorage
{
    private readonly Dictionary<string, object> _values;
    private readonly PyInstance? _instance;
    private readonly MemoryGovernor _governor;
    private readonly LythonSourceSpan _span;
    private readonly Dictionary<string, PyString> _labels = new(StringComparer.Ordinal);
    private int _labelCapacity;
    private long _decodedNameBytes;
    private IPyDictStorage? _extra;

    internal ObjectNamespaceStorage(Dictionary<string, object> values, PyInstance? instance, MemoryGovernor governor, LythonSourceSpan span)
    {
        governor.Reserve(384, span);
        governor.Commit(384);
        _values = values; _instance = instance; _governor = governor; _span = span;
    }
    public int Count => _values.Count + (_extra?.Count ?? 0);
    public IEnumerable<object> Keys => this.Select(x => x.Key);
    public IEnumerable<object> Values => this.Select(x => x.Value);
    public long CommittedBytes => 384L + 32L * _labelCapacity + _decodedNameBytes + (_extra?.CommittedBytes ?? 0);
    public object GetRequired(object key) => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
    public bool ContainsKey(object key) => TryGetValue(key, out _);
    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
    {
        if (key is PyString name)
        {
            using var decoding = _governor.ReserveTemporary(24L + 2L * name.GetUtf16CodeUnitCount(), _span);
            return _values.TryGetValue(name.AsString(), out value);
        }
        if (_extra is not null) return _extra.TryGetValue(key, out value);
        value = null; return false;
    }
    public bool SetItem(object key, object value)
    {
        RequireWritable();
        if (key is PyString name)
        {
            var bytes = 24L + 2L * name.GetUtf16CodeUnitCount();
            using var decoding = _governor.ReserveTemporary(bytes, _span);
            var decoded = name.AsString();
            var newName = !_values.ContainsKey(decoded);
            _instance!.SetAttribute(decoded, value);
            if (newName) { decoding.Commit(bytes); _decodedNameBytes += bytes; }
            return newName;
        }
        var added = !ContainsKey(key);
        _extra ??= PyDictStorage.Create(0, _governor, _span);
        _extra = PyDictStorage.EnsureCapacity(_extra, _extra.Count + (added ? 1 : 0), _governor, _span);
        _extra.SetItem(key, value);
        return added;
    }
    public void AddNew(object key, object value) => SetItem(key, value);
    public bool TrySetExisting(object key, object value) { if (!ContainsKey(key)) return false; SetItem(key, value); return true; }
    public bool Remove(object key)
    {
        RequireWritable();
        if (key is PyString name)
        {
            using var decoding = _governor.ReserveTemporary(24L + 2L * name.GetUtf16CodeUnitCount(), _span);
            return _instance!.RemoveAttribute(name.AsString());
        }
        return _extra?.Remove(key) == true;
    }
    public void Clear()
    {
        RequireWritable();
        using var scratch = _governor.ReserveTemporary(32L + 8L * _values.Count, _span);
        foreach (var name in _values.Keys.ToArray()) _instance!.RemoveAttribute(name);
        _extra?.Clear();
    }
    public IPyDictStorage Clone() => PyDictStorage.Create(this);
    public long ReleaseCommittedBytes() => throw new InvalidOperationException("A live attribute namespace retains its backing.");
    private void RequireWritable()
    {
        if (_instance is null) throw RuntimeErrors.Type("namespace is read-only", _span);
    }
    private PyString Label(string name)
    {
        if (_labels.TryGetValue(name, out var label)) return label;
        if (_labels.Count == _labelCapacity)
        {
            using var growth = _governor.ReserveTemporary(32L * Math.Max(3L, 2L * (_labels.Count + 1)), _span);
            var capacity = _labels.EnsureCapacity(_labels.Count + 1);
            growth.Commit(32L * (capacity - _labelCapacity));
            _labelCapacity = capacity;
        }
        label = PyString.FromString(name, _governor, _span);
        _labels.Add(name, label);
        return label;
    }
    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
        foreach (var pair in _values) yield return new(Label(pair.Key), pair.Value);
        if (_extra is not null) foreach (var pair in _extra) yield return pair;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class PyMappingProxy : IPySubscriptableValue, IPyIterableValue, IPyContainsValue, IPyHashableValue,
    IPyTruthyValue, IPySizedValue, IPyRenderableValue, IPyGovernedValue, IPyDynamicAttributes, IChainMapSource
{
    internal PyDict Dictionary { get; }
    internal PyMappingProxy(PyDict dictionary, MemoryGovernor governor, LythonSourceSpan span)
    {
        governor.Reserve(64, span); governor.Commit(64); Dictionary = dictionary;
    }
    public MemoryGovernor? OwnerMemoryGovernor => Dictionary.OwnerMemoryGovernor;
    public LythonSourceSpan? AllocationSpan => Dictionary.AllocationSpan;
    public int Length => Dictionary.Count;
    public int Count => Dictionary.Count;
    public int GetPyHashCode() => throw new PyUnhashableException();
    public bool IsTruthy() => Count != 0;
    public object Underlying => this;
    public IEnumerable<object> Keys => Dictionary.Keys;
    public IEnumerable<object> Values => Dictionary.Values;
    public IEnumerable<KeyValuePair<object, object>> Items => Dictionary.Items;
    public IEnumerable<object> Iterate() => Dictionary.Keys;
    public bool Contains(object value, LythonSourceSpan span) => Dictionary.ContainsKey(value);
    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => Dictionary.TryGetValue(key, out value);
    public object GetSubscript(object index, LythonSourceSpan span)
        => Dictionary.TryGetValue(index, out var value) ? value : throw RuntimeErrors.MissingKey(index, span);
    public void SetItem(object key, object value) => throw RuntimeErrors.Type("mappingproxy does not support item assignment", AllocationSpan);
    public bool Remove(object key) => throw RuntimeErrors.Type("mappingproxy does not support item deletion", AllocationSpan);
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = null;
        return name is "keys" or "values" or "items" or "get" or "copy" && LythonRuntime.DictMembers.TryGetMember(Dictionary, name, out value);
    }
    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, AllocationSpan);
        try { builder.AppendAscii("mappingproxy("); builder.Append(PyRendering.ToReprPyString(Dictionary, context)); builder.Append((byte)')'); return builder.ToPyStringAndRelease(); }
        finally { builder.Release(); }
    }
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    internal static void UnwrapComparison(ref object left, ref object right)
    {
        if (left is not (PyDict or PyMappingProxy or PyDefaultDict or PyCounter or PyChainMap) ||
            right is not (PyDict or PyMappingProxy or PyDefaultDict or PyCounter or PyChainMap)) return;
        if (left is PyMappingProxy l) left = l.Dictionary;
        if (right is PyMappingProxy r) right = r.Dictionary;
    }
}
