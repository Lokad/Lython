namespace Lokad.Lython.Runtime;

internal abstract class PyModule
{
    protected PyModule(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>Gets names exported by <c>from module import *</c>.</summary>
    public virtual IReadOnlyList<string> ExportedNames => [];

    /// <summary>Gets names exposed through module introspection.</summary>
    public virtual IReadOnlyList<string> MemberNames => ExportedNames;

    /// <summary>Resolves a module member and returns <see langword="false"/> without throwing when absent.</summary>
    public abstract bool TryGetMember(string name, out object value);

    /// <summary>Assigns a writable module member, returning <see langword="false"/> when assignment is unsupported.</summary>
    public virtual bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }
}

internal sealed class ScriptPyModule : PyModule
{
    private readonly Dictionary<string, object> _members;

    public ScriptPyModule(string name, Dictionary<string, object> members)
        : base(name)
    {
        _members = members;
    }

    public override bool TryGetMember(string name, out object value) => _members.TryGetValue(name, out value!);

    public override IReadOnlyList<string> ExportedNames
        => _members.Keys.Where(static name => !name.StartsWith("_", StringComparison.Ordinal)).ToArray();

    public override IReadOnlyList<string> MemberNames => _members.Keys.ToArray();

    public override bool TrySetMember(string name, object value)
    {
        _members[name] = value;
        return true;
    }
}
