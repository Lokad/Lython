using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
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
