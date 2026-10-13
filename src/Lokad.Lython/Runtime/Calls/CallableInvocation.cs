namespace Lokad.Lython.Runtime;

internal static class CallableInvocation
{
    [ThreadStatic]
    private static CallArgumentValue[]? _unaryArguments;

    [ThreadStatic]
    private static CallArgumentValue[]? _binaryArguments;

    [ThreadStatic]
    private static CallArgumentValue[]? _ternaryArguments;

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

    public static object InvokeTernary(
        LythonRuntime.ICallable callable,
        object first,
        object second,
        object third,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var arguments = RentTernaryArguments(first, second, third);
        try
        {
            return callable.Invoke(arguments, span, context);
        }
        finally
        {
            ReturnTernaryArguments(arguments);
        }
    }

    public static async ValueTask<object> InvokeTernaryAsync(
        LythonRuntime.ICallable callable,
        object first,
        object second,
        object third,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        var arguments = RentTernaryArguments(first, second, third);
        try
        {
            return await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false);
        }
        finally
        {
            ReturnTernaryArguments(arguments);
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

    private static CallArgumentValue[] RentTernaryArguments(object first, object second, object third)
    {
        var arguments = _ternaryArguments ?? new CallArgumentValue[3];
        _ternaryArguments = null;
        arguments[0] = CallArgumentValue.Positional(first);
        arguments[1] = CallArgumentValue.Positional(second);
        arguments[2] = CallArgumentValue.Positional(third);
        return arguments;
    }

    private static void ReturnTernaryArguments(CallArgumentValue[] arguments)
    {
        arguments[0] = CallArgumentValue.Positional(PyNone.Instance);
        arguments[1] = CallArgumentValue.Positional(PyNone.Instance);
        arguments[2] = CallArgumentValue.Positional(PyNone.Instance);
        _ternaryArguments ??= arguments;
    }
}
