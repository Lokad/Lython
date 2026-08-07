using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class MissingMemberValue
{
    public static object Instance { get; } = new();
}

internal abstract class PyModule
{
    private readonly Dictionary<string, object> _memberCache = new(StringComparer.Ordinal);
    private readonly PyString _nameValue;

    protected PyModule(string name)
    {
        Name = name;
        _nameValue = PyString.FromString(name);
    }

    public string Name { get; }

    /// <summary>Gets names exported by <c>from module import *</c>.</summary>
    public virtual IReadOnlyList<string> ExportedNames => [];

    /// <summary>Gets names exposed through module introspection.</summary>
    public virtual IReadOnlyList<string> MemberNames => ExportedNames;

    /// <summary>Resolves a module member and returns <see langword="false"/> without throwing when absent.</summary>
    public abstract bool TryGetMember(string name, [MaybeNullWhen(false)] out object value);

    /// <summary>Resolves a stable module attribute, preserving the identity of values already exposed.</summary>
    public bool TryGetCachedMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "__name__")
        {
            value = _nameValue;
            return true;
        }

        if (_memberCache.TryGetValue(name, out value))
        {
            return true;
        }

        if (!TryGetMember(name, out value))
        {
            return false;
        }

        _memberCache[name] = value;
        return true;
    }

    /// <summary>Updates the cached identity of an attribute changed by a writable module.</summary>
    protected void UpdateCachedMember(string name, object value) => _memberCache[name] = value;

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

    public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => _members.TryGetValue(name, out value);

    public override IReadOnlyList<string> ExportedNames
        => _members.Keys.Where(static name => !name.StartsWith("_", StringComparison.Ordinal)).ToArray();

    public override IReadOnlyList<string> MemberNames => _members.Keys.ToArray();

    public override bool TrySetMember(string name, object value)
    {
        _members[name] = value;
        UpdateCachedMember(name, value);
        return true;
    }
}
