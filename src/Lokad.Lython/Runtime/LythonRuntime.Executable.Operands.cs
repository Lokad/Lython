using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ReadExecutableOperand(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int source, int index, int input, ExecutionContext scope, ExecutableCaptures captures)
    {
        if (source < 0)
        {
            var immediate = ~source;
            if (immediate < code.Constants.Length) return RuntimeValue(code.Constants[immediate]);
            var capture = immediate - code.CaptureOperandBase;
            if (capture >= 0)
                return captures.TryRead(capture, out var captured) && captured is not null
                    ? captured : throw RuntimeErrors.FreeVariableNotAssociated(code.ClosureNames[capture], code.OperandReadSpan(index, input));
            var name = code.Names[immediate - code.Constants.Length];
            return TryResolveExecutableGlobal(code, immediate - code.Constants.Length, scope, out var global)
                ? global : throw RuntimeErrors.NameNotDefined(name, code.OperandReadSpan(index, input));
        }
        var value = values[source].Object;
        if (source < code.FirstTemporarySlot)
        {
            if (value is ExecutableCell cell) value = cell.Value;
            if (value is null) return LoadMissingLocal(code, source, code.OperandReadSpan(index, input), scope);
        }
        return value.RequireNotNull();
    }

    private static bool TryReadMachineOperand(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int source, ExecutionContext scope, out long value, ExecutableCaptures captures = default)
    {
        if (source >= 0) return values[source].TryGetInteger(out value);
        var index = ~source;
        if (index < code.Constants.Length) return TryMachineInteger(code.Constants[index], out value);
        var capture = index - code.CaptureOperandBase;
        if (capture >= 0) return captures.TryReadInteger(capture, out value);
        // Missing-name diagnostics belong to the ordered ordinary reads. This
        // probe never raises a later operand's failure ahead of an earlier one.
        TryResolveExecutableGlobal(code, index - code.Constants.Length, scope, out var item);
        return TryMachineInteger(item, out value);
    }

    private static bool TryReadDoubleOperand(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int source, ExecutionContext scope, out double value, ExecutableCaptures captures)
    {
        if (source >= 0) return values[source].TryGetDouble(out value);
        var index = ~source;
        object? item;
        if (index < code.Constants.Length) item = code.Constants[index];
        else
        {
            var capture = index - code.CaptureOperandBase;
            if (capture >= 0) captures.TryRead(capture, out item);
            else TryResolveExecutableGlobal(code, index - code.Constants.Length, scope, out item);
        }
        if (item is double floating)
        { value = floating; return true; }
        value = 0;
        return false;
    }
}
