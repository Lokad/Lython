from pathlib import Path
p=Path('src/Lokad.Lython/Runtime/LythonRuntime.Executable.NativeReturn.cs');t=p.read_text(encoding='utf-8')
old='''                var value = EvaluateNativeReturn(body.Root + code.FirstTemporarySlot, code, body.Values, arguments, context,
                    entryException, ref result, consumedByIteration);
                result.Value = value;'''
new='''                var root = body.Root + code.FirstTemporarySlot;
                if (consumedByIteration || body.Values[body.Root].Operation is
                    NativeReturnOperation.StoreClosure or NativeReturnOperation.Conditional)
                    EvaluateNativeRoot(root, code, body.Values, arguments, context, entryException, ref result, consumedByIteration);
                else result.Value = EvaluateNativeValue(root, code, body.Values, arguments, context, entryException);'''
assert t.count(old)==1;t=t.replace(old,new)
start=t.index('    private static object EvaluateNativeReturn(')
middle=t.index('        object? first = null, second = null, third = null, fourth = null;',start)
header='''    // Preparation emits stores and branches only as return continuations, never
    // as expression operands. Keep their private result handoff at that boundary;
    // ordinary recursive values need neither iteration metadata nor a byref result.
    private static void EvaluateNativeRoot(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException,
        ref ExecutableResult returned, bool consumedByIteration)
    {
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        var instruction = value.Instruction;
        if (value.Operation == NativeReturnOperation.StoreClosure)
        {
            var stored = EvaluateNativeValue(value.First, code, values, arguments, context, entryException);
            context.State.CheckExecution(instruction.Span);
            ((ExecutableCell)arguments[arguments.Length - code.NativeBody!.ClosureInputCount + instruction.ClosureSlot]!).StoreThroughOwner(stored);
            EvaluateNativeRoot(value.Second, code, values, arguments, context, entryException, ref returned, consumedByIteration);
            CheckNativeReturnExit(value.Second - code.FirstTemporarySlot, values, context);
        }
        else if (value.Operation == NativeReturnOperation.Conditional)
        {
            var condition = EvaluateNativeValue(value.First, code, values, arguments, context, entryException);
            context.State.CheckExecution(instruction.Span);
            var selected = PyTruthiness.IsTruthy(condition) ? value.Second : value.Third;
            EvaluateNativeRoot(selected, code, values, arguments, context, entryException, ref returned, consumedByIteration);
            CheckNativeReturnExit(selected - code.FirstTemporarySlot, values, context);
        }
        else if (consumedByIteration && value.Operation == NativeReturnOperation.CasedSplit)
        {
            var receiver = (PyString)EvaluateNativeValue(value.First, code, values, arguments, context, entryException);
            try { ExecuteCasedSplit(code.CallSites[instruction.CallSiteIndex].CasedSplit!, receiver, context, out returned, true); }
            catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
        }
        else returned.Value = EvaluateNativeValue(index, code, values, arguments, context, entryException);
    }

    // Keep the materialized split's result carrier out of every expression frame.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object EvaluateMaterializedCasedSplit(ExecutableCasedSplit recipe, PyString receiver, ExecutionContext context)
    {
        ExecuteCasedSplit(recipe, receiver, context, out var result);
        return result.Value.RequireNotNull();
    }

    private static object EvaluateNativeValue(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException)
    {
        // Bound direct inputs are pure. Real operations retain their checks.
        if (index < code.FirstTemporarySlot)
            return RuntimeValue(index < 0 ? code.Constants[~index] : arguments[index]);
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        if (value.Operation is NativeReturnOperation.StoreClosure or NativeReturnOperation.Conditional)
            throw new InvalidOperationException("A native return continuation cannot be used as an expression operand.");
        var instruction = value.Instruction;
'''
t=t[:start]+header+t[middle:]
old='''                try
                {
                    ExecuteCasedSplit(code.CallSites[instruction.CallSiteIndex].CasedSplit!, (PyString)first,
                        context, out var split, consumedByIteration);
                    if (consumedByIteration) returned = split;
                    return split.Value.RequireNotNull();
                }'''
new='''                try { return EvaluateMaterializedCasedSplit(code.CallSites[instruction.CallSiteIndex].CasedSplit!, (PyString)first, context); }'''
assert t.count(old)==1;t=t.replace(old,new)
t=t.replace('EvaluateNativeReturn(', 'EvaluateNativeValue(').replace('context, entryException, ref returned);','context, entryException);')
p.write_text(t,encoding='utf-8')
print('Private handoff remains at native return roots; ordinary expression evaluation no longer carries result metadata. Control handling moved, not duplicated; original checks/exit order retained.')
