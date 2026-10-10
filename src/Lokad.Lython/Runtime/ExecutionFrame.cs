namespace Lokad.Lython.Runtime;

internal class ExecutionFrame
{
    private Dictionary<string, object>? _variables;

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

    private Dictionary<string, object> InitializeVariables()
    {
        var variables = new Dictionary<string, object>(StringComparer.Ordinal);
        return Interlocked.CompareExchange(ref _variables, variables, null) ?? variables;
    }
}

