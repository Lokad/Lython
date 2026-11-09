using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal static class CallableInvocation
{
    // Binders consume this managed stack storage into invocation-owned values
    // before returning a pending operation. Other callable implementations may
    // retain their arguments and therefore still receive an owned array.
    [InlineArray(3)]
    internal struct Arguments
    {
        private CallArgumentValue _first;
    }

    public static object InvokeUnary(object callable, object value, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        Arguments arguments = default;
        arguments[0] = CallArgumentValue.Positional(value);
        return Invoke(callable, arguments[..1], span, context);
    }

    public static ValueTask<object> InvokeUnaryAsync(object callable, object value, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        Arguments arguments = default;
        arguments[0] = CallArgumentValue.Positional(value);
        return InvokeAsync(callable, arguments[..1], span, context);
    }

    public static object InvokeBinary(object callable, object first, object second, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        Arguments arguments = default;
        arguments[0] = CallArgumentValue.Positional(first);
        arguments[1] = CallArgumentValue.Positional(second);
        return Invoke(callable, arguments[..2], span, context);
    }

    public static ValueTask<object> InvokeBinaryAsync(object callable, object first, object second, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        Arguments arguments = default;
        arguments[0] = CallArgumentValue.Positional(first);
        arguments[1] = CallArgumentValue.Positional(second);
        return InvokeAsync(callable, arguments[..2], span, context);
    }

    public static object InvokeTernary(LythonRuntime.ICallable callable, object first, object second, object third,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        Arguments arguments = default;
        arguments[0] = CallArgumentValue.Positional(first);
        arguments[1] = CallArgumentValue.Positional(second);
        arguments[2] = CallArgumentValue.Positional(third);
        return Invoke(callable, arguments, span, context);
    }

    public static ValueTask<object> InvokeTernaryAsync(LythonRuntime.ICallable callable, object first, object second, object third,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        Arguments arguments = default;
        arguments[0] = CallArgumentValue.Positional(first);
        arguments[1] = CallArgumentValue.Positional(second);
        arguments[2] = CallArgumentValue.Positional(third);
        return InvokeAsync(callable, arguments, span, context);
    }

    internal static object Invoke(object callable, ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
        => callable switch
        {
            PyFunctionBase function => function.Invoke(arguments, span, context),
            PyBoundMethod { Function: PyFunctionBase function } method => function.Invoke(arguments, span, context, method.Receiver),
            LythonRuntime.BoundArgumentsCallable builtin => builtin.Invoke(arguments, span, context),
            LythonRuntime.ICallable other => other.Invoke(arguments.ToArray(), span, context),
            _ => throw LythonRuntime.NotCallableError(callable, context, span)
        };

    internal static ValueTask<object> InvokeAsync(object callable, ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        try
        {
            return callable switch
            {
                PyFunctionBase function => function.InvokeAsync(arguments, span, context),
                PyBoundMethod { Function: PyFunctionBase function } method => function.InvokeAsync(arguments, span, context, method.Receiver),
                LythonRuntime.BoundArgumentsCallable builtin => builtin.InvokeAsync(arguments, span, context),
                LythonRuntime.ICallable other => other.InvokeAsync(arguments.ToArray(), span, context),
                _ => throw LythonRuntime.NotCallableError(callable, context, span)
            };
        }
        catch (Exception error) { return ValueTask.FromException<object>(error); }
    }
}
