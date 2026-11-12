using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Conditional branches and unary not need the same closed truth contract.
    // Other values may run Python protocols and require their invocation frame.
    private static bool CanUseNativeTruth(object value)
        => value is PyNone or bool or BigInteger or double or PyString or PyBytes
            or PyList or PyTuple or PyDict or PySet or PyRange;

    // Compiler preparation and ordinary receiver entry select the same closed
    // operations. Other arities/keywords/protocol values keep ordinary binding.
    internal static NativeReturnOperation? SelectNativeStringOperation(object? target, int argumentCount)
        => ReferenceEquals(target, ReceiverMethods.StringSplit) && argumentCount is 1 or 2 ? NativeReturnOperation.Split
            : ReferenceEquals(target, ReceiverMethods.StringReplace) && argumentCount is 2 or 3 ? NativeReturnOperation.Replace
            : ReferenceEquals(target, ReceiverMethods.StringStartsWith) && argumentCount == 1 ? NativeReturnOperation.StartsWith
            : ReferenceEquals(target, ReceiverMethods.StringEndsWith) && argumentCount == 1 ? NativeReturnOperation.EndsWith
            : null;

    private static bool TryGetNativeStringCallShape(NativeReturnOperation operation, NativeReturnShape receiver,
        NativeReturnShape first, NativeReturnShape second, NativeReturnShape third, out NativeReturnShape result)
    {
        result = NativeReturnShape.Other;
        if (receiver != NativeReturnShape.String) return false;
        switch (operation)
        {
            case NativeReturnOperation.Split:
                result = NativeReturnShape.Sized;
                return first is NativeReturnShape.None or NativeReturnShape.String
                    && second is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Boolean;
            case NativeReturnOperation.Replace:
                result = NativeReturnShape.String;
                return first == NativeReturnShape.String && second == NativeReturnShape.String
                    && third is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Boolean;
            case NativeReturnOperation.StartsWith or NativeReturnOperation.EndsWith:
                result = NativeReturnShape.Boolean;
                return first == NativeReturnShape.String;
            default:
                throw new InvalidOperationException("Unknown prepared string operation.");
        }
    }

    private static object InvokeNativeStringOperation(NativeReturnOperation operation, PyString receiver,
        object? first, object? second, object? third, LythonSourceSpan span, ExecutionContext context)
        => operation switch
        {
            NativeReturnOperation.Split => InvokeStringSplit(receiver, first, second, span, context),
            NativeReturnOperation.Replace => InvokeStringReplace(receiver, first!, second!, third, span, context),
            NativeReturnOperation.StartsWith or NativeReturnOperation.EndsWith =>
                InvokeStringAffix(receiver, first!, span, context, operation == NativeReturnOperation.StartsWith),
            _ => throw new InvalidOperationException("Unknown prepared string operation."),
        };

    // Select the existing operation without doing native work. A caller supplies
    // either an actual operand, or the proved shape of a not-yet-evaluated one.
    // Other means no closed return proof; ordinary calls can still use an
    // operation such as len for its existing broader error/protocol behavior.
    private static Func<object, LythonSourceSpan, ExecutionContext, object>? SelectNativeUnaryOperation(
        object? target, object? value, NativeReturnShape inputShape, out NativeReturnShape resultShape)
    {
        resultShape = NativeReturnShape.Other;
        if (target is not BuiltinCallable builtin) return null;
        switch (builtin.Name)
        {
            case "int" when value is PyString or PyBytes || inputShape is NativeReturnShape.String or NativeReturnShape.Bytes:
                resultShape = NativeReturnShape.Integer;
                return IntegerTextOperation;
            case "float" when value is PyString or PyBytes || inputShape is NativeReturnShape.String or NativeReturnShape.Bytes:
                resultShape = NativeReturnShape.Float;
                return FloatTextOperation;
            case "str" when value is BigInteger or PyString || inputShape is NativeReturnShape.Integer or NativeReturnShape.String:
                resultShape = NativeReturnShape.String;
                return NativeStrOperation;
            case "abs" when ExecutionContext.IsAbsoluteBuiltin(target) &&
                (value is BigInteger or double or bool || inputShape is NativeReturnShape.Integer or NativeReturnShape.Float or NativeReturnShape.Boolean):
                resultShape = value is double || inputShape == NativeReturnShape.Float ? NativeReturnShape.Float : NativeReturnShape.Integer;
                return AbsoluteOperation;
            case "sum" when ExecutionContext.IsSumBuiltin(target) && (value is PyBytes || inputShape == NativeReturnShape.Bytes):
                resultShape = NativeReturnShape.Integer;
                return SumBytesOperation;
            case "len" when ExecutionContext.IsLengthBuiltin(target):
                if (inputShape is NativeReturnShape.String or NativeReturnShape.Bytes or NativeReturnShape.Dictionary or NativeReturnShape.Sized ||
                    value is PyString or PyBytes or PyList or PyTuple or PyDict or PySet or PyRange or PyDeque)
                {
                    resultShape = NativeReturnShape.Integer;
                    return LengthOperation;
                }
                // Keep the ordinary call's original wider selection, but never
                // claim that an unknown value cannot invoke a guest protocol.
                return value is not null and not PyInstance ? LengthOperation : null;
            default:
                return null;
        }
    }
}
