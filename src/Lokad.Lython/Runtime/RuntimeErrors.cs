namespace Lokad.Lython.Runtime;

internal static class RuntimeErrors
{
    public static LythonRuntimeException CannotImportMember(string moduleName, string memberName, LythonSourceSpan? span)
        => new("ImportError", $"Cannot import name '{memberName}' from '{moduleName}'.", span);

    public static LythonRuntimeException NoModuleNamed(string moduleName, LythonSourceSpan? span)
        => new("ModuleNotFoundError", $"No module named '{moduleName}'.", span);

    public static LythonRuntimeException CircularImport(string moduleName, LythonSourceSpan? span)
        => new("ImportError", $"Circular import of '{moduleName}'.", span);

    public static LythonRuntimeException TopLevelLoopControl(LythonSourceSpan? span)
        => Runtime("Loop control cannot appear outside a loop.", span);

    public static LythonRuntimeException Type(string message, LythonSourceSpan? span)
        => new("TypeError", message, span);

    public static LythonRuntimeException UnsupportedOperands(
        string operation,
        object left,
        object right,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        var lhs = OperandTypeName(left, context);
        var rhs = OperandTypeName(right, context);
        return Type($"unsupported operand type(s) for {operation}: '{lhs}' and '{rhs}'", span);
    }

    public static LythonRuntimeException BadUnaryOperand(
        string operation,
        object operand,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        var name = OperandTypeName(operand, context);
        return Type($"bad operand type for unary {operation}: '{name}'", span);
    }

    private static string OperandTypeName(object? value, LythonRuntime.ExecutionContext context) => value switch
    {
        LythonRuntime.DictKeysView => "dict_keys",
        LythonRuntime.DictValuesView => "dict_values",
        LythonRuntime.DictItemsView => "dict_items",
        ChainMapKeysView => "KeysView",
        ChainMapValuesView => "ValuesView",
        ChainMapItemsView => "ItemsView",
        _ => LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context),
    };

    public static LythonRuntimeException Value(string message, LythonSourceSpan? span)
        => new("ValueError", message, span);

    public static LythonRuntimeException Runtime(string message, LythonSourceSpan? span)
        => new("RuntimeError", message, span);

    public static LythonRuntimeException Recursion(string message, LythonSourceSpan? span)
        => new("RecursionError", message, span);

    public static LythonRuntimeException Memory(string message, LythonSourceSpan? span)
        => new("MemoryError", message, span);

    public static LythonRuntimeException Host(string operation, Exception exception, LythonSourceSpan? span)
        => new("RuntimeError", $"Host {operation} failed.", span, exception);

    // Mapping misses carry their key as the payload so str/args render like
    // CPython; the fixed text stays for the message member and failure paths.
    public static LythonRuntimeException MissingKey(object key, LythonSourceSpan? span)
        => new("KeyError", "Key was not found.", span, null, key);

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
