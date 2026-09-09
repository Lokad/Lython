using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class PyInstance : IPyRenderableValue, IPyHashableValue, LythonRuntime.ICallable, IPyGovernedValue
{
    private readonly Dictionary<string, object> _attributes = new(StringComparer.Ordinal);

    // Attribute tables grow one CLR entry per guest attribute name; charge
    // each new key so retained attributes accumulate. The key strings
    // themselves are caller-owned (often shared parser constants); only the
    // table slot is charged here. Empty instances stay free.
    private const long AttributeSlotBytes = 64;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedAttributeBytes;

    public PyInstance(PyType type)
    {
        Type = type;
    }

    public PyInstance(PyType type, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        Type = type;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public PyType Type { get; }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public bool TryGetOwnAttribute(string name, [MaybeNullWhen(false)] out object value) => _attributes.TryGetValue(name, out value);

    public IEnumerable<KeyValuePair<string, object>> EnumerateOwnAttributes() => _attributes;

    public bool TryGetAttribute(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        => PyAttributeLookup.TryResolveInstanceMember(this, name, context, span, out value);

    public void SetAttribute(string name, object value)
    {
        if (_memoryGovernor is not null && !_attributes.ContainsKey(name))
        {
            _memoryGovernor.Reserve(AttributeSlotBytes, _allocationSpan);
            _memoryGovernor.Commit(AttributeSlotBytes);
            _committedAttributeBytes += AttributeSlotBytes;
        }

        _attributes[name] = value;
    }

    public bool RemoveAttribute(string name)
    {
        if (!_attributes.Remove(name))
        {
            return false;
        }

        // Only release a matching reservation: attributes adopted from an
        // ungoverned table (MG03 gap) hold no charge to return.
        if (_memoryGovernor is not null && _committedAttributeBytes >= AttributeSlotBytes)
        {
            _memoryGovernor.Release(AttributeSlotBytes);
            _committedAttributeBytes -= AttributeSlotBytes;
        }

        return true;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (!TryGetAttribute("__call__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{Type.Name}' object is not callable", span);
        }

        return callable.Invoke(arguments, span, context);
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (!TryGetAttribute("__call__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{Type.Name}' object is not callable", span);
        }

        return callable.InvokeAsync(arguments, span, context);
    }

    public int GetPyHashCode()
    {
        if (Type.DataclassFields is { } fields)
        {
            return Type.DataclassHashMode switch
            {
                DataclassHashMode.Generated => GetGeneratedDataclassHashCode(fields),
                DataclassHashMode.Unhashable => throw new PyUnhashableException(),
                _ => RuntimeHelpers.GetHashCode(this)
            };
        }

        return RuntimeHelpers.GetHashCode(this);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        if (TryGetAttribute("__repr__", context.Context, span, out var reprMember) &&
            reprMember is LythonRuntime.ICallable reprCallable)
        {
            return RequireRenderedString(reprCallable.Invoke([], span, context.Context), "__repr__");
        }

        if (Type.DataclassReprEnabled && Type.DataclassReprFields is { } renderedFields)
        {
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
            builder.AppendString(Type.Name);
            builder.AppendAscii("(");
            for (var i = 0; i < renderedFields.Length; i++)
            {
                if (i > 0)
                {
                    builder.AppendAscii(", ");
                }

                var field = renderedFields[i];
                builder.AppendString(field.Name);
                builder.AppendAscii("=");
                _ = TryGetOwnAttribute(field.Name, out var value);
                builder.Append(PyRendering.ToReprPyString(value ?? PyNone.Instance, context));
            }

            builder.AppendAscii(")");
            return builder.ToPyStringAndRelease();
        }

        return PyString.FromString($"<{Type.Name} object>");
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        if (TryGetAttribute("__str__", context.Context, span, out var strMember) &&
            strMember is LythonRuntime.ICallable strCallable)
        {
            return RequireRenderedString(strCallable.Invoke([], span, context.Context), "__str__");
        }

        return RenderPython(context);
    }

    private static PyString RequireRenderedString(object value, string methodName)
    {
        if (PyStringOps.TryAsString(value, out var text))
        {
            return text;
        }

        throw new LythonRuntimeException("TypeError", methodName + " returned non-string", null);
    }

    public override string ToString() => $"<{Type.Name} object>";

    private int GetGeneratedDataclassHashCode(IReadOnlyList<DataclassFieldSpec> fields)
    {
        var hash = new HashCode();
        foreach (var field in fields)
        {
            if (!PyDataclass.ShouldIncludeInGeneratedHash(field))
            {
                continue;
            }

            _ = TryGetOwnAttribute(field.Name, out var value);
            hash.Add(PyValueComparer.Instance.GetHashCode(value ?? PyNone.Instance));
        }

        return hash.ToHashCode();
    }
}
