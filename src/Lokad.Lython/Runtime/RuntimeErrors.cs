namespace Lokad.Lython.Runtime;

internal static class RuntimeErrors
{
    public static LythonRuntimeException CannotImportMember(string moduleName, string memberName, LythonSourceSpan? span)
        => new("ImportError", $"Cannot import name '{memberName}' from '{moduleName}'.", span);

    public static LythonRuntimeException NoModuleNamed(string moduleName, LythonSourceSpan? span)
        => new("ImportError", $"No module named '{moduleName}'.", span);

    public static LythonRuntimeException CircularImport(string moduleName, LythonSourceSpan? span)
        => new("ImportError", $"Circular import of '{moduleName}'.", span);

    public static LythonRuntimeException TopLevelLoopControl(LythonSourceSpan? span)
        => Runtime("Loop control cannot appear outside a loop.", span);

    public static LythonRuntimeException Type(string message, LythonSourceSpan? span)
        => new("TypeError", message, span);

    public static LythonRuntimeException Value(string message, LythonSourceSpan? span)
        => new("ValueError", message, span);

    public static LythonRuntimeException Runtime(string message, LythonSourceSpan? span)
        => new("RuntimeError", message, span);

    public static LythonRuntimeException Host(string operation, Exception exception, LythonSourceSpan? span)
        => new("RuntimeError", $"Host {operation} failed: {exception.Message}", span, exception);

    public static LythonRuntimeException Key(string message, LythonSourceSpan? span)
        => new("KeyError", message, span);

    public static LythonRuntimeException NotCallable(LythonSourceSpan span)
        => Type("Object is not callable.", span);

    public static LythonRuntimeException NotSubscriptable(LythonSourceSpan span)
        => Type("Object is not subscriptable.", span);

    public static LythonRuntimeException NotSliceable(LythonSourceSpan span)
        => Type("Object does not support slicing.", span);

    public static LythonRuntimeException NameNotDefined(string name, LythonSourceSpan? span)
        => new("NameError", $"Name '{name}' is not defined.", span);

    public static LythonRuntimeException RaiseExpectsException(LythonSourceSpan? span)
        => Type("raise expects an exception instance.", span);

    public static LythonRuntimeException ImportedModuleReturned(string moduleName, LythonSourceSpan? span)
        => Runtime($"Imported module '{moduleName}' returned from top level.", span);

    public static LythonRuntimeException CannotImportModule(string moduleName, string message, LythonSourceSpan? span)
        => new("SyntaxError", $"Cannot import module '{moduleName}': {message}", span);

    public static LythonRuntimeException SetElementsMustBeHashable(LythonSourceSpan? span)
        => Type("set elements must be hashable.", span);
}
