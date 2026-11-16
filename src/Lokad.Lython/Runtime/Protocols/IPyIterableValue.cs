namespace Lokad.Lython.Runtime;

// Like an executable operand, an unpublished scalar moves privately until its
// object identity is observed. Exhaustion uses the existing unassigned slot;
// guest None remains the ordinary non-null PyNone singleton.
internal struct PyIterationResult
{
    private LythonRuntime.ExecutableSlot _value;

    private PyIterationResult(LythonRuntime.ExecutableSlot value) => _value = value;

    public static PyIterationResult End => default;

    public static PyIterationResult Yield(object value)
    {
        return new(LythonRuntime.ExecutableSlot.FromObject(LythonRuntime.RuntimeValue(value)));
    }

    internal static PyIterationResult Yield(LythonRuntime.ExecutableSlot value)
        => new(value);

    public readonly bool HasValue => !_value.IsUnassigned;

    internal readonly bool TryGetInteger(out long value) => _value.TryGetInteger(out value);

    internal LythonRuntime.ExecutableSlot TakeSlotValue()
    {
        var value = _value;
        _value = default;
        return value;
    }

    // Observe through the owning local before passing an object to Python or
    // making an observable alias. The slot then preserves its canonical box.
    public object Value => HasValue ? _value.Object.RequireNotNull()
        : throw new InvalidOperationException("An exhausted iterator has no value.");

    public void Deconstruct(out bool hasValue, out object value)
    {
        hasValue = HasValue;
        value = HasValue ? Value : PyNone.Instance;
    }
}

internal interface IPyIterableValue
{
    IEnumerable<object> Iterate();
}

internal interface IPyAsyncIterableValue : IPyIterableValue
{
    IAsyncEnumerable<object> IterateAsync();
}

internal interface IPyIteratorValue : IPyIterableValue
{
    PyIterationResult TryMoveNext();
}

internal interface IPyAsyncIteratorValue : IPyIteratorValue, IPyAsyncIterableValue
{
    ValueTask<PyIterationResult> TryMoveNextAsync();
}
