using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class PyInstance : IPyRenderableValue, IPyHashableValue, LythonRuntime.ICallable, IPyGovernedValue, IPyOwnershipSnapshot
{
    private readonly Dictionary<string, object> _attributes;

    // Attribute tables grow one CLR entry per guest attribute name; charge
    // each new key so retained attributes accumulate. The key strings
    // themselves are caller-owned (often shared parser constants); only the
    // table slot is charged here. Even an empty instance owns its shell and
    // attribute table, so it can register before initialization/copy callbacks.
    internal const long InstanceShellBytes = 160;
    private const long AttributeSlotBytes = 64;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedAttributeBytes;

    public PyInstance(PyType type)
    {
        _attributes = new(StringComparer.Ordinal);
        Type = type;
    }

    public PyInstance(PyType type, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        governor.Reserve(InstanceShellBytes, allocationSpan);
        try
        {
            _attributes = new(StringComparer.Ordinal);
        }
        catch
        {
            governor.ReleaseReserved(InstanceShellBytes);
            throw;
        }
        governor.Commit(InstanceShellBytes);
        Type = type;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public PyType Type { get; }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    // Attribute-slot funding for unpublished rollback and explicit replacement.
    // Attachment funds existing slots before publication; instances remain batch-owned.
    internal long CommittedAttributeBytes => _committedAttributeBytes;

    internal long CommittedStorageBytes => (_memoryGovernor is null ? 0 : InstanceShellBytes) + _committedAttributeBytes;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is not null)
        {
            _allocationSpan ??= allocationSpan;
            return;
        }
        var attributeBytes = checked((long)_attributes.Count * AttributeSlotBytes);
        var storageBytes = checked(InstanceShellBytes + attributeBytes);
        governor.Reserve(storageBytes, allocationSpan);
        governor.Commit(storageBytes);
        _committedAttributeBytes = attributeBytes;
        _allocationSpan = allocationSpan;
        _memoryGovernor = governor;
    }

    public bool TryGetOwnAttribute(string name, [MaybeNullWhen(false)] out object value) => _attributes.TryGetValue(name, out value);

    public IEnumerable<KeyValuePair<string, object>> EnumerateOwnAttributes() => _attributes;

    internal int OwnAttributeCount => _attributes.Count;

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

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Release(AttributeSlotBytes);
            _committedAttributeBytes -= AttributeSlotBytes;
        }

        return true;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (!Type.TryLookupInMro("__call__", 0, out var rawMember, out _) ||
            PyAttributeLookup.BindForInstance(this, rawMember, context, span) is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{Type.Name}' object is not callable", span);
        }

        return callable.Invoke(arguments, span, context);
    }

    public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (!Type.TryLookupInMro("__call__", 0, out var rawMember, out _) ||
            await PyAttributeLookup.BindForInstanceAsync(this, rawMember, context, span).ConfigureAwait(false) is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{Type.Name}' object is not callable", span);
        }

        return await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false);
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
        => RenderSpecialAsync("__repr__", context, false).GetAwaiter().GetResult();

    public PyString RenderInterpolated(PyRenderingContext context)
        => RenderSpecialAsync("__str__", context, false).GetAwaiter().GetResult();

    internal ValueTask<PyString> RenderPythonAsync(PyRenderingContext context)
        => RenderSpecialAsync("__repr__", context, true);

    internal ValueTask<PyString> RenderInterpolatedAsync(PyRenderingContext context)
        => RenderSpecialAsync("__str__", context, true);

    private async ValueTask<PyString> RenderSpecialAsync(string name, PyRenderingContext context, bool asynchronous)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // Implicit protocols bypass instance shadows and __getattribute__;
        // descriptor binding still applies to the member found on the type.
        if (Type.TryLookupInMro(name, 0, out var raw, out _))
        {
            object member;
            try
            {
                member = asynchronous
                    ? await PyAttributeLookup.BindForInstanceAsync(this, raw, context.Context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForInstance(this, raw, context.Context, span);
            }
            catch (LythonRuntimeException error) when (name == "__repr__" && error.Attribution is null)
            {
                // CPython's repr slot clears ordinary descriptor-binding errors,
                // but containment refusal/cancellation must still stop this run.
                context.Context.CheckExecution(span);
                return RenderObjectRepresentation();
            }
            if (member is not LythonRuntime.ICallable callable)
                throw LythonRuntime.NotCallableError(member, context.Context, span);
            var rendered = asynchronous
                ? await callable.InvokeAsync([], span, context.Context).ConfigureAwait(false)
                : callable.Invoke([], span, context.Context);
            return RequireRenderedString(rendered, name);
        }

        return name == "__str__"
            ? await RenderSpecialAsync("__repr__", context, asynchronous).ConfigureAwait(false)
            : RenderDefault(context);
    }

    private PyString RenderDefault(PyRenderingContext context)
    {
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

        return RenderObjectRepresentation();
    }

    internal PyString RenderObjectRepresentation() => PyString.FromString($"<{Type.Name} object>");

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
        using var guard = PyStructuralGuard.EnterSingle(this, PyStructuralGuard.AmbientSpan, PyStructuralGuard.AmbientContext);
        var hash = new HashCode();
        foreach (var field in fields)
        {
            PyStructuralGuard.NoteWork();
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
