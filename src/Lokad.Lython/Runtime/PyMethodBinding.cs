namespace Lokad.Lython.Runtime;

internal interface IPyDescriptor
{
    object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span);
    ValueTask<object> GetAsync(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
        => new(Get(instance, owner, context, span));
}

internal interface IClassOwnedMember
{
    // Establish the defining owner once; assigning an alias to another class
    // must not rewrite the original descriptor or function's identity.
    void BindOwner(PyType owner);
}

internal interface IClassNamedMember
{
    void BindName(string name);
}

internal interface IPySettableDescriptor
{
    void Set(PyInstance instance, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span);
}

internal interface IPyBindableCallable : IPyDescriptor, LythonRuntime.ICallable
{
    object Bind(object self);
}

// Marks object slot wrappers (and their bound method-wrapper forms) so class
// resolution reports the wrapper_descriptor/method-wrapper runtime types.
internal interface IPySlotWrapper
{
}
