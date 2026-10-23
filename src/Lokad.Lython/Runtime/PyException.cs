using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyException : IPyHashableValue, IPyGovernedValue
{
    public PyException(PythonExceptionIdentity identity, string message, object? value, PyTuple? args)
    {
        Identity = identity;
        Message = message;
        _payload = value;
        Args = args;
        Code = value ?? PyNone.Instance;
    }

    public PythonExceptionIdentity Identity { get; }
    public string Message { get; }
    private readonly object? _payload;
    public object Value => _payload ?? PyNone.Instance;
    public PyTuple? Args { get; set; }
    public object Code { get; set; }

    // Native control errors stay allocation-free until their arguments are read.
    // Once materialized, the actual tuple is authoritative and identity-stable.
    internal PyTuple GetArgs(MemoryGovernor? governor = null, LythonSourceSpan? span = null)
    {
        if (Args is not null) return Args;
        governor ??= OwnerMemoryGovernor;
        object[] items;
        if (TypeName == "CalledProcessError" && _payload is PyDict process &&
            process.TryGetValue(PyString.FromString("returncode"), out var code) &&
            process.TryGetValue(PyString.FromString("cmd"), out var command))
            items = [code, command];
        else if (TypeName == "TimeoutExpired" && _payload is PyDict timeout &&
            timeout.TryGetValue(PyString.FromString("cmd"), out var commandValue) &&
            timeout.TryGetValue(PyString.FromString("timeout"), out var seconds))
            items = [commandValue, seconds];
        else if (_payload is null || Identity == PythonExceptionIdentity.Module("json", "JSONDecodeError"))
            items = string.IsNullOrEmpty(Message) ? [] : [governor is null
                ? PyString.FromString(Message) : PyString.FromString(Message, governor, span)];
        else
            items = [_payload];
        return Args = items.Length == 0 ? PyTuple.Empty : governor is null
            ? PyTuple.FromOwnedArray(items) : new PyTuple(items, governor, span);
    }

    // Fixed instance storage, including inline provenance and its governor link.
    // Referenced args, payloads, attributes and exception chains own their own
    // storage; this shell owns those references, never their aliased payloads.
    internal const long ShellBytes = 160;
    public MemoryGovernor? OwnerMemoryGovernor { get; private set; }
    private bool _chainOwned;
    public LythonSourceSpan? AllocationSpan => null;

    internal static PyException CreateOwned(
        PythonExceptionIdentity identity, string message, object? value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span,
        PyTuple? args = null)
    {
        var governor = context.MemoryGovernor;
        governor.Reserve(ShellBytes, span);
        PyException result;
        try
        {
            result = new(identity, message, value, args) { OwnerMemoryGovernor = governor, _chainOwned = true };
            governor.Commit(ShellBytes);
        }
        catch
        {
            governor.ReleaseReserved(ShellBytes);
            throw;
        }
        return result;
    }

    // Engine-control exceptions are unowned until they become guest values.
    // Constructor results and published aliases already have an owner.
    // Fund the root before descending; refusal remains terminal.
    internal void Adopt(LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        if (_chainOwned) return;
        if (OwnerMemoryGovernor is null)
        {
            context.MemoryGovernor.Reserve(ShellBytes, span);
            context.MemoryGovernor.Commit(ShellBytes);
            OwnerMemoryGovernor = context.MemoryGovernor;
        }
        if (Cause is not null || Context is not null)
        {
            ExecutionGuards.EnsureInterpreterStack(span);
            Cause?.Adopt(context, span);
            Context?.Adopt(context, span);
        }
        // Guest-created cyclic chains already contain fully owned instances;
        // transient control chains are constructed only from earlier errors.
        _chainOwned = true;
    }
    public PyException(PythonExceptionIdentity identity, string message, object value)
        : this(identity, message, value, null)
    {
    }

    public PyException(string typeName, string message, object? value)
        : this(PythonExceptionIdentity.FromRuntimeTypeName(typeName), message, value, null)
    {
    }

    public string TypeName => Identity.TypeName;

    // Internal provenance follows this actual exception across catches and
    // reraises. Python attributes/args cannot manufacture or replace it.
    internal LythonFailureAttribution? Attribution { get; set; }

    // Equality and hashing use identity, independent of mutable exception chains.
    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    // Explicit raise causes ride alongside the value; handlers rewrap
    // through the thrown CLR exception, which carries the same slot.
    // Direct __cause__ assignment writes this slot like CPython.
    public PyException? Cause { get; set; }

    // Implicit chaining follows the active handler like CPython: the context
    // is captured at raise time while suppression comes from `raise ... from`.
    // Direct __context__ assignment writes this slot like CPython.
    public PyException? Context { get; set; }

    // Direct __suppress_context__ assignment writes this slot like CPython.
    public bool SuppressContext { get; set; }

    // Custom attributes live in a governed dict like CPython's instance
    // __dict__; reads check it before fixed members so assignments shadow,
    // while the args slot stays separate like CPython.
    public PyDict? CustomDict { get; set; }

}
