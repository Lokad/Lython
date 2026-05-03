namespace Lokad.Lython.Runtime;

internal sealed class ExecutionFrame
{
    public ExecutionFrame(ExecutionFrame? parent, Dictionary<string, object> variables)
    {
        Parent = parent;
        Variables = variables;
    }

    public ExecutionFrame? Parent { get; }

    public Dictionary<string, object> Variables { get; }
}

