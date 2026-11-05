using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyDict ExecuteExecutableMakeDict(ref ExecutableValueStorage stack, int start, int pairCount, LythonSourceSpan span, ExecutionContext context)
    {
        var valueCount = checked(pairCount * 2);
        var dict = new PyDict(context.MemoryGovernor, span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        for (var i = 0; i < valueCount; i += 2)
        {
            var key = ValidateDictionaryKey(stack[start + i], span);
            dict.SetItem(key, stack[start + i + 1]);
        }

        stack.RetireTo(start);

        context.CheckExecution(span);
        return dict;
    }

    private static PySet ExecuteExecutableMakeSet(ref ExecutableValueStorage stack, int start, int count, LythonSourceSpan span, ExecutionContext context)
    {
        var set = new PySet(context.MemoryGovernor, span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        try
        {
            for (var i = start; i < start + count; i++)
            {
                set.Add(ValidateSetItem(stack[i], span));
            }

            stack.RetireTo(start);

            context.CheckExecution(span);
        }
        catch
        {
            set.ReleaseUnpublishedStorage();
            throw;
        }
        return set;
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

    private static bool CanCacheRuntimeMemberTarget(object target)
        => target is not IPyContextualDynamicAttributes &&
           target is (PyString
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
               or PyDirEntryObject);

    // Only immediate calls may reuse a bound callable: an ordinary attribute
    // read must preserve the identity of the freshly resolved method object.
    // Instances, types, super objects and functions keep mutable lookup rules:
    // user-level rebinding, deletion and descriptors resolve anew on every access.
    private static bool CanCacheRuntimeMemberValue(object target, object value, bool isCallTarget = false)
        => (isCallTarget || value is not ICallable) &&
           (CanCacheRuntimeMemberTarget(target) ||
           (value is ICallable &&
            target is not PyInstance &&
            target is not PyType &&
            target is not PySuper &&
            target is not PyFunctionBase &&
            target is not IPyContextualDynamicAttributes));

    private static PyList CreateListFromStack(ref ExecutableValueStorage stack, int start, int count, LythonSourceSpan span, ExecutionContext context)
    {
        var bytes = PyList.EstimateCapacityBytes(count);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var items = count == 0 ? Array.Empty<object>() : new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = stack[start + i];
        }

        stack.RetireTo(start);
        storage.Commit(bytes);
        return PyList.FromTransferredArray(items, context.MemoryGovernor, span);
    }

    private static PyTuple CreateTupleFromStack(ref ExecutableValueStorage stack, int start, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count == 0) return PyTuple.Empty;

        using var preflight7249 = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = stack[start + i];
        }

        stack.RetireTo(start);
        var tuple = new PyTuple(items, context.MemoryGovernor, span);
        return tuple;
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
