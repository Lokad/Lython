using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool CanBorrowForwardedInteger(object target, Span<ExecutableSlot> values, int source)
        => source >= 0 && values[source].IsUnpublishedInteger && target is PyExecutableFunction
        {
            CodeObject: { HasDirectOperandReturn: true } or { DirectDefinitionReturn.InputCount: > 0 } or
                { HasDirectDefinitionPair: true },
        };

    private static object ReadExecutableOperand(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int source, int index, int input, ExecutionContext scope, ExecutableCaptures captures)
    {
        if (source < 0)
        {
            var immediate = ~source;
            var name = immediate - code.Constants.Length;
            if (name < 0) return RuntimeValue(code.Constants[immediate]);
            if (name >= code.Names.Length)
            {
                var capture = name - code.Names.Length;
                return captures.TryRead(capture, out var captured) && captured is not null
                    ? captured : throw RuntimeErrors.FreeVariableNotAssociated(code.ClosureNames[capture], code.OperandReadSpan(index, input));
            }
            return TryResolveExecutableGlobal(code, name, scope, out var global)
                ? global : throw RuntimeErrors.NameNotDefined(code.Names[name], code.OperandReadSpan(index, input));
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
        var name = index - code.Constants.Length;
        if (name < 0) return code.TryReadMachineConstant(index, out value);
        if (name >= code.Names.Length) return captures.TryReadInteger(name - code.Names.Length, out value);
        // Missing-name diagnostics belong to the ordered ordinary reads. This
        // probe never raises a later operand's failure ahead of an earlier one.
        TryResolveExecutableGlobal(code, name, scope, out var item);
        return TryMachineInteger(item, out value);
    }

    private static bool TryReadDoubleOperand(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int source, ExecutionContext scope, out double value, ExecutableCaptures captures)
    {
        if (source >= 0) return values[source].TryGetDouble(out value);
        var index = ~source;
        var name = index - code.Constants.Length;
        object? item;
        if (name < 0) item = code.Constants[index];
        else if (name >= code.Names.Length) captures.TryRead(name - code.Names.Length, out item);
        else TryResolveExecutableGlobal(code, name, scope, out item);
        if (item is double floating)
        { value = floating; return true; }
        value = 0;
        return false;
    }
}
