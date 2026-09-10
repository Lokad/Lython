namespace Lokad.Lython.Runtime;

internal interface IPyDescriptor
{
    object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span);
}

internal interface IClassOwnedMember
{
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
