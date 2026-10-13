namespace Lokad.Lython.Runtime;

internal class ExecutionFrame : IMemoryStorageEdges
{
    private Dictionary<string, object>? _variables;
    internal IMemoryStorageEdges? ActiveCaller { get; set; }

    public virtual void VisitStorageChildren(MemoryStorageWalk visitor)
    {
        if (!visitor.Add(Parent) || !visitor.Add(ActiveCaller)) return;
        // Variables allocates a namespace on first access. The stored-field
        // walk must neither create it nor call name-resolution machinery.
        if (_variables is not null)
            foreach (var pair in _variables) if (!visitor.Add(pair.Value)) return;
    }

    protected ExecutionFrame() { }

    // Module contexts construct their namespace after preparing shared services.
    // They publish the context only after this eager namespace is initialized.
    protected void InitializeFrameVariables(Dictionary<string, object> variables)
        => _variables = variables;

    public ExecutionFrame(ExecutionFrame? parent, Dictionary<string, object> variables)
    {
        Parent = parent;
        _variables = variables;
    }

    // Executable function locals usually live in slots. Keep their empty
    // namespace absent until a dictionary consumer needs it; captured and
    // mirrored frames still get the same stable dictionary on first access.
    public ExecutionFrame(ExecutionFrame parent) => Parent = parent;

    public ExecutionFrame? Parent { get; }

    public Dictionary<string, object> Variables => _variables ?? InitializeVariables();

    internal bool ContainsVariable(string name) => _variables?.ContainsKey(name) == true;

    internal bool TryGetVariable(string name, [MaybeNullWhen(false)] out object value)
    {
        if (_variables is not null) return _variables.TryGetValue(name, out value);
        value = null;
        return false;
    }

    // A lower bound from existing strong fields, not a reachability collector.
    // MAX avoids counting aliases twice. The fixed inspection budget bounds
    // this cold search; missed values simply leave normal relief in place.
    internal long KnownLiveStringBytes(MemoryGovernor governor, ref int remaining)
    {
        long largest = 0;
        if (_variables is null) return largest;
        foreach (var pair in _variables)
        {
            if (remaining == 0) break;
            largest = Math.Max(largest, KnownLiveStringBytes(pair.Value, governor, ref remaining));
        }
        return largest;
    }

    internal static long KnownLiveStringBytes(object value, MemoryGovernor governor, ref int remaining)
    {
        if (remaining == 0) return 0;
        remaining--;
        if (value is Text.PyString text) return PayloadBytes(text, governor);
        if (value is not PyList list) return 0;
        long largest = 0;
        for (var i = 0; i < list.Count && remaining > 0; i++)
        {
            remaining--;
            if (list[i] is Text.PyString item)
                largest = Math.Max(largest, PayloadBytes(item, governor));
        }
        return largest;

        static long PayloadBytes(Text.PyString text, MemoryGovernor governor)
            => ReferenceEquals(text.OwnerMemoryGovernor, governor) &&
               text.ReclamationEntry is { } entry && entry.ValueCharge >= text.Utf8Bytes.Length
                ? text.Utf8Bytes.Length : 0;
    }

    private Dictionary<string, object> InitializeVariables()
    {
        var variables = new Dictionary<string, object>(StringComparer.Ordinal);
        return Interlocked.CompareExchange(ref _variables, variables, null) ?? variables;
    }
}

