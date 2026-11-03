namespace Lokad.Lython.Runtime;

internal interface IPyDictStorage : IEnumerable<KeyValuePair<object, object>>
{
    int Count { get; }

    IEnumerable<object> Keys { get; }

    IEnumerable<object> Values { get; }

    object GetRequired(object key);

    bool TryGetValue(object key, [MaybeNullWhen(false)] out object value);

    bool ContainsKey(object key);

    bool SetItem(object key, object value);

    bool TrySetExisting(object key, object value);

    void AddNew(object key, object value);

    bool Remove(object key);

    void Clear();

    // Raw storage traversal is separate from Python mutation-checked iteration.
    IEnumerable<KeyValuePair<object, object>> Entries { get; }

    void EnsureCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span);

    long CommittedBytes { get; }

    long ReleaseCommittedBytes();

}
