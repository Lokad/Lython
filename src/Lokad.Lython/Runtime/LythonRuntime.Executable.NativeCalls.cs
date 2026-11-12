using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal enum NativeStringOperation { Split, Replace, StartsWith, EndsWith }

    // Conditional branches and unary not need the same closed truth contract.
    // Other values may run Python protocols and require their invocation frame.
    private static bool CanUseNativeTruth(object value)
        => value is PyNone or bool or BigInteger or double or PyString or PyBytes
            or PyList or PyTuple or PyDict or PySet or PyRange;

    // Both execution forms select operations from actual evaluated operands.
    // Other arities/keywords/protocol values keep ordinary binding.
    internal static NativeStringOperation? SelectNativeStringOperation(object? target, int argumentCount)
        => ReferenceEquals(target, ReceiverMethods.StringSplit) && argumentCount is 1 or 2 ? NativeStringOperation.Split
            : ReferenceEquals(target, ReceiverMethods.StringReplace) && argumentCount is 2 or 3 ? NativeStringOperation.Replace
            : ReferenceEquals(target, ReceiverMethods.StringStartsWith) && argumentCount == 1 ? NativeStringOperation.StartsWith
            : ReferenceEquals(target, ReceiverMethods.StringEndsWith) && argumentCount == 1 ? NativeStringOperation.EndsWith
            : null;

    private static bool CanUseNativeStringCall(NativeStringOperation operation, object? first, object? second, object? third)
        => operation switch
        {
            NativeStringOperation.Split => first is null or PyNone or PyString && second is null or PyNone or BigInteger or bool,
            NativeStringOperation.Replace => first is PyString && second is PyString && third is null or PyNone or BigInteger or bool,
            NativeStringOperation.StartsWith or NativeStringOperation.EndsWith => first is PyString,
            _ => throw new InvalidOperationException("Unknown native string operation."),
        };

    private static bool CanUseNativeBinary(BinaryOperatorSyntax operation, object first, object second)
    {
        if (first is PyString && second is PyString) return operation == BinaryOperatorSyntax.Add;
        if (first is not (BigInteger or double or bool) || second is not (BigInteger or double or bool)) return false;
        return operation switch
        {
            BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or BinaryOperatorSyntax.Multiply
                or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo or BinaryOperatorSyntax.Divide
                or BinaryOperatorSyntax.Equal or BinaryOperatorSyntax.NotEqual or BinaryOperatorSyntax.Less
                or BinaryOperatorSyntax.LessEqual or BinaryOperatorSyntax.Greater or BinaryOperatorSyntax.GreaterEqual => true,
            BinaryOperatorSyntax.BitwiseOr or BinaryOperatorSyntax.BitwiseXor or BinaryOperatorSyntax.BitwiseAnd
                or BinaryOperatorSyntax.LeftShift or BinaryOperatorSyntax.RightShift => first is not double && second is not double,
            _ => false,
        };
    }

    private static object InvokeNativeStringOperation(NativeStringOperation operation, PyString receiver,
        object? first, object? second, object? third, LythonSourceSpan span, ExecutionContext context)
        => operation switch
        {
            NativeStringOperation.Split => InvokeStringSplit(receiver, first, second, span, context),
            NativeStringOperation.Replace => InvokeStringReplace(receiver, first!, second!, third, span, context),
            NativeStringOperation.StartsWith or NativeStringOperation.EndsWith =>
                InvokeStringAffix(receiver, first!, span, context, operation == NativeStringOperation.StartsWith),
            _ => throw new InvalidOperationException("Unknown prepared string operation."),
        };

    // A closed operation cannot invoke guest protocols. Ordinary framed calls
    // retain len's broader selection and its original error/protocol behavior.
    private static Func<object, LythonSourceSpan, ExecutionContext, object>? SelectNativeUnaryOperation(
        object? target, object? value, out bool closed)
    {
        closed = true;
        if (target is not BuiltinCallable builtin) return null;
        switch (builtin.Name)
        {
            case "int" when value is PyString or PyBytes:
                return IntegerTextOperation;
            case "float" when value is PyString or PyBytes:
                return FloatTextOperation;
            case "str" when value is BigInteger or PyString:
                return NativeStrOperation;
            case "abs" when ExecutionContext.IsAbsoluteBuiltin(target) &&
                value is BigInteger or double or bool:
                return AbsoluteOperation;
            case "sum" when ExecutionContext.IsSumBuiltin(target) && value is PyBytes:
                return SumBytesOperation;
            case "len" when ExecutionContext.IsLengthBuiltin(target):
                if (value is PyString or PyBytes or PyList or PyTuple or PyDict or PySet or PyRange or PyDeque)
                {
                    return LengthOperation;
                }
                // Keep the ordinary call's original wider selection, but never
                // claim that an unknown value cannot invoke a guest protocol.
                closed = false;
                return value is not null and not PyInstance ? LengthOperation : null;
            default:
                return null;
        }
    }
}
