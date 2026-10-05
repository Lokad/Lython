namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static async ValueTask<object> EvaluateMatrixMultiplyAsync(object left, object right,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var reflectedFirst = ShouldTryReflectedFirst(left, right, "__rmatmul__");
        if (reflectedFirst)
        {
            var reflected = await Invoke(right, "__rmatmul__", left).ConfigureAwait(false);
            if (reflected.Kind == SpecialMethodInvocationKind.Invoked && reflected.Value is not PyNotImplemented) return reflected.Value;
        }
        var direct = await Invoke(left, "__matmul__", right).ConfigureAwait(false);
        if (direct.Kind == SpecialMethodInvocationKind.Invoked && direct.Value is not PyNotImplemented) return direct.Value;
        if (!reflectedFirst && !ReferenceEquals(MatrixOperandType(left), MatrixOperandType(right)))
        {
            var reflected = await Invoke(right, "__rmatmul__", left).ConfigureAwait(false);
            if (reflected.Kind == SpecialMethodInvocationKind.Invoked && reflected.Value is not PyNotImplemented) return reflected.Value;
        }
        throw RuntimeErrors.UnsupportedOperands("@", left, right, span);

        ValueTask<SpecialMethodInvocation> Invoke(object receiver, string name, object argument)
            => asynchronous ? InvokeBinarySpecialMethodAsync(receiver, name, argument, context, span)
                : InvokeBinarySpecialMethod(receiver, name, argument, context, span);
    }

    private static object MatrixOperandType(object value) => value is PyInstance instance ? instance.Type : value.GetType();

    private static async ValueTask<object> EvaluateMatrixInPlaceAsync(object left, object right,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var result = asynchronous ? await InvokeBinarySpecialMethodAsync(left, "__imatmul__", right, context, span).ConfigureAwait(false)
            : await InvokeBinarySpecialMethod(left, "__imatmul__", right, context, span).ConfigureAwait(false);
        return result.Kind == SpecialMethodInvocationKind.Invoked && result.Value is not PyNotImplemented ? result.Value
            : await EvaluateMatrixMultiplyAsync(left, right, context, span, asynchronous).ConfigureAwait(false);
    }
}
