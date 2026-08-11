namespace Lokad.Lython.Runtime;

internal static class CallableInvocation
{
    [ThreadStatic]
    private static CallArgumentValue[]? _unaryArguments;

    [ThreadStatic]
    private static CallArgumentValue[]? _binaryArguments;

    public static object InvokeUnary(
        LythonRuntime.ICallable callable,
        object value,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var arguments = RentUnaryArguments(value);
        try
        {
            return callable.Invoke(arguments, span, context);
        }
        finally
        {
            ReturnUnaryArguments(arguments);
        }
    }

    public static async ValueTask<object> InvokeUnaryAsync(
        LythonRuntime.ICallable callable,
        object value,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var arguments = RentUnaryArguments(value);
        try
        {
            return await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false);
        }
        finally
        {
            ReturnUnaryArguments(arguments);
        }
    }

    public static object InvokeBinary(
        LythonRuntime.ICallable callable,
        object first,
        object second,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var arguments = RentBinaryArguments(first, second);
        try
        {
            return callable.Invoke(arguments, span, context);
        }
        finally
        {
            ReturnBinaryArguments(arguments);
        }
    }

    public static async ValueTask<object> InvokeBinaryAsync(
        LythonRuntime.ICallable callable,
        object first,
        object second,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var arguments = RentBinaryArguments(first, second);
        try
        {
            return await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false);
        }
        finally
        {
            ReturnBinaryArguments(arguments);
        }
    }

    private static CallArgumentValue[] RentUnaryArguments(object value)
    {
        var arguments = _unaryArguments ?? new CallArgumentValue[1];
        _unaryArguments = null;
        arguments[0] = CallArgumentValue.Positional(value);
        return arguments;
    }

    private static void ReturnUnaryArguments(CallArgumentValue[] arguments)
    {
        arguments[0] = CallArgumentValue.Positional(PyNone.Instance);
        _unaryArguments ??= arguments;
    }

    private static CallArgumentValue[] RentBinaryArguments(object first, object second)
    {
        var arguments = _binaryArguments ?? new CallArgumentValue[2];
        _binaryArguments = null;
        arguments[0] = CallArgumentValue.Positional(first);
        arguments[1] = CallArgumentValue.Positional(second);
        return arguments;
    }

    private static void ReturnBinaryArguments(CallArgumentValue[] arguments)
    {
        arguments[0] = CallArgumentValue.Positional(PyNone.Instance);
        arguments[1] = CallArgumentValue.Positional(PyNone.Instance);
        _binaryArguments ??= arguments;
    }
}
