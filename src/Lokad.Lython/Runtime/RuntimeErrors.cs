using System.Numerics;
using Lokad.Lython.Runtime.Text;

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
        LythonSourceSpan? span)
    {
        return Type($"unsupported operand type(s) for {operation}: '{OperandTypeName(left)}' and '{OperandTypeName(right)}'", span);
    }

    public static LythonRuntimeException BadUnaryOperand(
        string operation,
        object operand,
        LythonSourceSpan? span)
    {
        return Type($"bad operand type for unary {operation}: '{OperandTypeName(operand)}'", span);
    }

    public static LythonRuntimeException UnsupportedComparison(
        string operation,
        object left,
        object right,
        LythonSourceSpan? span)
    {
        return Type($"'{operation}' not supported between instances of '{OperandTypeName(left)}' and '{OperandTypeName(right)}'", span);
    }

    public static LythonRuntimeException ConcatError(string left, object right, LythonSourceSpan? span)
        => Type($"can only concatenate {left} (not \"{OperandTypeName(right)}\") to {left}", span);

    public static LythonRuntimeException CantConcatToBytes(object right, LythonSourceSpan? span)
        => Type($"can't concat {OperandTypeName(right)} to bytes", span);

    public static LythonRuntimeException MultiplySequenceError(object other, LythonSourceSpan? span)
        => Type($"can't multiply sequence by non-int of type '{OperandTypeName(other)}'", span);

    public static string OperandTypeName(object? value) => value switch
    {
        null => "NoneType",
        PyNone => "NoneType",
        PyString => "str",
        double => "float",
        bool => "bool",
        BigInteger or int => "int",
        PyList => "list",
        PyDict or PyDefaultDict or PyCounter => "dict",
        PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject => "tuple",
        PySet => "set",
        PyBytes => "bytes",
        PyRange => "range",
        PyDeque => "deque",
        PyChainMap => "ChainMap",
        LythonRuntime.DictKeysView => "dict_keys",
        LythonRuntime.DictValuesView => "dict_values",
        LythonRuntime.DictItemsView => "dict_items",
        ChainMapKeysView => "KeysView",
        ChainMapValuesView => "ValuesView",
        ChainMapItemsView => "ItemsView",
        PyDecimal => "Decimal",
        PyDate => "date",
        PyTime => "time",
        PyDateTime => "datetime",
        PyTimedelta => "timedelta",
        PyTimezone => "timezone",
        LythonRuntime.StatisticsModule.PyNormalDist => "NormalDist",
        PyInstance instance => instance.Type.Name,
        _ => "object",
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
