using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyDict ExecuteExecutableMakeDict(ref ExecutableValueStorage stack, ExecutableCodeObject code,
        in ExecutableInstruction instruction, ExecutionContext context)
    {
        var start = instruction.InputStart;
        var inputs = stack.Values.AsSpan(start, checked(instruction.PairCount * 2));
        var cacheIndex = instruction.LookupCacheIndex;
        var span = instruction.Span;
        var dictionary = cacheIndex >= 0 && inputs[0].Reference is PyString
            ? CreateSlotDictionary(inputs, ref stack.GetLookupCaches(code.LookupCacheCount, span)[cacheIndex], context, span)
            : CreateSlotDictionary(inputs, context, span);
        try { stack.RetireTo(start); return dictionary; }
        catch { dictionary.DiscardUnpublishedStorage(); throw; }
    }

    private static PySet ExecuteExecutableMakeSet(ref ExecutableValueStorage stack, int start, int count, LythonSourceSpan span, ExecutionContext context)
    {
        var set = CreateSlotSet(stack.Values.AsSpan(start, count), context, span);
        try { stack.RetireTo(start); return set; }
        catch { set.ReleaseUnpublishedStorage(); throw; }
    }

    private static PyDict ExecuteExecutableMakeRecord(ref ExecutableValueStorage stack, ExecutableCodeObject code,
        in ExecutableInstruction instruction, ExecutionContext context)
    {
        var keys = (PyDict.RecordKeys)code.Constants[instruction.ConstantIndex]!;
        var dictionary = keys.CreateDisplay(stack.Values.AsSpan(instruction.InputStart, keys.Count), keyAndValue: false,
            context, instruction.Span);
        try { stack.RetireTo(instruction.InputStart); return dictionary; }
        catch { dictionary.DiscardUnpublishedStorage(); throw; }
    }

    private static IPyContextManager RequireContextManager(object value, LythonSourceSpan span)
        => value is IPyContextManager manager ? manager
            : throw RuntimeErrors.Type("'" + RuntimeErrors.OperandTypeName(value) + "' object does not support the context manager protocol", span);

    private static object ExecuteExecutableSlice(in ExecutableValueStorage storage, int first,
        ExecutableSliceParts parts, LythonSourceSpan span, ExecutionContext context)
    {
        var (start, end, step) = ReadExecutableSliceBounds(in storage, parts, first + 1);
        return ReadSliceValue(storage[first], start, end, step, span, context);
    }

    private static (object? Start, object? End, object? Step) ReadExecutableSliceBounds(
        in ExecutableValueStorage storage, ExecutableSliceParts parts, int first)
    {
        var start = (parts & ExecutableSliceParts.Start) != 0 ? storage[first++] : null;
        var end = (parts & ExecutableSliceParts.End) != 0 ? storage[first++] : null;
        var step = (parts & ExecutableSliceParts.Step) != 0 ? storage[first] : null;
        return (start, end, step);
    }

    private static bool CanCacheRuntimeMemberReceiver(object target)
        => target is not (PyInstance or PyType or PySuper or PyFunctionBase or IPyContextualDynamicAttributes);

    // Only immediate calls may reuse a bound callable: ordinary attribute reads
    // preserve fresh method identity. Mutable/protocol receivers never enter the
    // cache, so they need neither a lookup nor otherwise-unused table capacity.
    private static bool CanCacheRuntimeMemberValue(object target, object value, bool isCallTarget = false)
        => CanCacheRuntimeMemberReceiver(target) && (isCallTarget || value is not ICallable) &&
           (value is ICallable || target is (PyString
               or PyBytes
               or PyList
               or PyTuple
               or PyDict
               or PySet
               or PyDecimal
               or PyDate
               or PyTime
               or PyDateTime
               or PyTimedelta
               or PyTimezone
               or RePatternObject
               or ReMatchObject
               or PyPath
               or PyModule
               or HashlibHashObject
               or StatisticsModule.PyNormalDist
               or PyDeque
               or PkgutilLoaderObject
               or PyDirEntryObject));

    private static PyList CreateListFromStack(ref ExecutableValueStorage stack, int start, int count, LythonSourceSpan span, ExecutionContext context)
    {
        var list = CreateSlotList(stack.Values.AsSpan(start, count), context, span);
        try { stack.RetireTo(start); return list; }
        catch { list.DiscardUnpublishedStorage(); throw; }
    }

    private static PyTuple CreateTupleFromStack(ref ExecutableValueStorage stack, int start, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count == 0) return PyTuple.Empty;

        // The existing operand owner survives admission/reentry and copying.
        // Retire its inputs only after the independent final tuple is funded.
        var tuple = PyTuple.FromSlots(stack.Values.AsSpan(start, count), context, span);
        stack.RetireTo(start);
        return tuple;
    }

    private static bool IsUnpackedTupleReturn(ExecutableCodeObject code, int index,
        in ExecutableInstruction tuple, int count)
    {
        if (count == 0 || tuple.ItemCount != count || tuple.HasResultConsumer || index + 1 >= code.Instructions.Length)
            return false;
        ref readonly var next = ref code.Instructions[index + 1];
        return next.OpCode == ExecutableOpCode.Return && next.InputStart == tuple.ResultSlot;
    }

    private static void AdmitUnpackedReturnScalars(ReadOnlySpan<ExecutableSlot> items, ExecutionContext scope, LythonSourceSpan span)
    {
        // Only scalar publication needs new funding. The caller owns the
        // operand window, and a fixed unpack constructs no tuple.
        var bytes = PyTuple.EstimateScalarSlots(items, scope, span);
        if (bytes == 0) return;
        var governor = scope.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            scope.CheckExecution(span);
            governor.Commit(bytes);
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    private static void WriteUnpackOperands(ReadOnlySpan<ExecutableSlot> items, Span<ExecutableSlot> destination)
    {
        // Caller and callee own separate slots. The first target consumes
        // the top slot, so write each element directly in its final position.
        for (var i = 0; i < items.Length; i++) destination[^(i + 1)] = items[i];
    }

    private static object EvaluateExecutableUnary(ExecutableUnaryOperator op, object operand, ExecutionContext context, LythonSourceSpan span)
        => EvaluateUnaryOperator(MapExecutableUnary(op), operand, context, span);

    private static UnaryOperatorSyntax MapExecutableUnary(ExecutableUnaryOperator op)
        => op switch
        {
            ExecutableUnaryOperator.Not => UnaryOperatorSyntax.Not,
            ExecutableUnaryOperator.Plus => UnaryOperatorSyntax.Plus,
            ExecutableUnaryOperator.Minus => UnaryOperatorSyntax.Minus,
            ExecutableUnaryOperator.BitwiseNot => UnaryOperatorSyntax.BitwiseNot,
            _ => throw new InvalidOperationException($"Executable IR contains unknown unary operator {op}."),
        };



}
