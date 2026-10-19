using System.Collections.Concurrent;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class MissingMemberValue
{
    public static object Instance { get; } = new();
}

internal abstract class PyModule : IPyRenderableValue
{
    private readonly ConcurrentDictionary<string, object> _memberCache = new(StringComparer.Ordinal);
    private readonly PyString _nameValue;

    protected PyModule(string name)
    {
        Name = name;
        _nameValue = PyString.FromString(name);
    }

    public string Name { get; }

    /// <summary>Gets names exported by <c>from module import *</c>.</summary>
    public virtual IReadOnlyList<string> ExportedNames
        => StaticContracts.GetModuleExportedMemberNames(Name);

    /// <summary>Gets names exposed through module introspection.</summary>
    public virtual IReadOnlyList<string> MemberNames
        => StaticContracts.GetModuleMemberNames(Name);

    /// <summary>Resolves a module member and returns <see langword="false"/> without throwing when absent.</summary>
    public abstract bool TryGetMember(string name, [MaybeNullWhen(false)] out object value);

    /// <summary>Resolves a stable module attribute, preserving the identity of values already exposed.</summary>
    public bool TryGetCachedMember(string name, [MaybeNullWhen(false)] out object value)
    {
        // Script attributes share mutable globals; read their current binding.
        // Builtin definition factories still use the stable identity cache.
        if (this is ScriptPyModule) return TryGetMember(name, out value);
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

        value = _memberCache.GetOrAdd(name, value);
        return true;
    }

    /// <summary>Assigns a writable module member, returning <see langword="false"/> when assignment is unsupported.</summary>
    public virtual bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"<module '{Name}'>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<module '{Name}'>";
}

internal sealed class ScriptPyModule : PyModule
{
    // Conservatively fund namespace entries for the run. The module and its
    // functions share one existing context rather than copied export values.
    private const long ExportedEntryBytes = 32;

    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public ScriptPyModule(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        : base(name)
    {
        _context = context;
        _span = span;
        var charge = checked(ExportedEntryBytes * context.Variables.Count);
        context.MemoryGovernor.Reserve(charge, span);
        context.MemoryGovernor.Commit(charge);
    }

    public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        => _context.Variables.TryGetValue(name, out value);

    public override IReadOnlyList<string> ExportedNames
        => _context.Variables.Keys.Where(static name => !name.StartsWith("_", StringComparison.Ordinal)).ToArray();

    public override IReadOnlyList<string> MemberNames => _context.Variables.Keys.ToArray();

    public override bool TrySetMember(string name, object value)
    {
        var bytes = _context.Variables.ContainsKey(name) ? 0 : ExportedEntryBytes;
        var governor = _context.MemoryGovernor;
        governor.Reserve(bytes, _span);
        try
        {
            // Keep globals() and active cells coherent too.
            LythonRuntime.StoreName(name, value, _context, _span);
        }
        catch
        {
            governor.ReleaseReserved(bytes);
            throw;
        }
        governor.Commit(bytes);
        return true;
    }
}
