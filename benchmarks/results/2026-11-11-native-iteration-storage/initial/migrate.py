from pathlib import Path
def edit(name, changes):
 p=Path('src/Lokad.Lython')/name;t=p.read_text(encoding='utf-8')
 for old,new,count in changes:
  assert t.count(old)==count,(name,old,t.count(old),count)
  t=t.replace(old,new)
 p.write_text(t,encoding='utf-8')
edit('Runtime/LythonRuntime.Executable.cs',[
 ('internal object? ReturnValue;','internal ExecutableResult ReturnValue;',1),
 ('ReturnValue = null;','ReturnValue = default;',1),
 ('private sealed record PendingReturn(object Value) : PendingAbruptSignal;','private sealed record PendingReturn(ExecutableResult Result) : PendingAbruptSignal;',1),
 ('internal struct ExecutableValueStorage','internal partial struct ExecutableValueStorage',1),
 ('        private int[]? _nativeIterationPositions;\n','',1),
 ('_nativeIterationPositions = null;','_nativeIterations = null;',2),
 ('set => _items[index] = value;','set { ClearNativeIteration(index); _items[index] = value; }',1),
 ('            _items[resultSlot] = result;','            ClearNativeIteration(resultSlot);\n            _items[resultSlot] = result;',1),
 ('            _items[inputSlot] = null!;','            ClearNativeIteration(inputSlot);\n            _items[inputSlot] = null!;',1),
 ('            _items[resultSlot] = null!;','            ClearNativeIteration(resultSlot);\n            _items[resultSlot] = null!;',1),
 ('internal bool TryPrepareNativeIteration(object source, int slot, int liveExtent, LythonSourceSpan span)','internal bool TryPrepareNativeIteration(object source, int sourceSlot, int slot, int liveExtent, LythonSourceSpan span)',1),
 ('''            if (source is PyStringOps.CasedWhitespaceIteration)
            {
                PublishValue(slot, liveExtent, source);
                return true;
            }
            if (source is not (PyList or PyTuple)) return false;
            if (_nativeIterationPositions is null)
            {
                // The existing private operand keeps the source alive. One
                // admitted position array replaces repeated native cursor and
                // enumerator objects; it shares this frame's storage lifetime.
                var bytes = checked(32L + 4L * (_items.Length - _firstTemporarySlot));
                var frameBytes = checked(_ownedStorageBytes + bytes);
                using var storage = _state.MemoryGovernor.ReserveTemporary(bytes, span);
                var positions = new int[_items.Length - _firstTemporarySlot];
                storage.Commit(bytes);
                _nativeIterationPositions = positions;
                _ownedStorageBytes = frameBytes;
            }
            _nativeIterationPositions[slot - _firstTemporarySlot] = 0;
            PublishValue(slot, liveExtent, source);''', '''            var prepared = ReadResult(sourceSlot, source);
            if (prepared.CasedSplit is not null)
            {
                PublishIterationResult(slot, liveExtent, prepared);
                return true;
            }
            if (source is not (PyList or PyTuple)) return false;
            EnsureNativeIterationStorage(span);
            PublishValue(slot, liveExtent, source);''',1),
 ('''                case PyStringOps.CasedWhitespaceIteration words:
                    next = words.MoveNext();
                    return true;''','''                case PyString source when _nativeIterations is not null &&
                    _nativeIterations[slot - _firstTemporarySlot].CasedSplit is { } recipe:
                    ref var words = ref _nativeIterations[slot - _firstTemporarySlot];
                    next = PyStringOps.AdvanceCasedWhitespace(source, recipe.Upper,
                        ref words.Range, words.Context!, recipe.SplitCallSpan);
                    return true;''',1),
 ('_nativeIterationPositions![slot - _firstTemporarySlot]','_nativeIterations![slot - _firstTemporarySlot].Range.Position',2),
 ('_nativeIterationPositions[slot - _firstTemporarySlot]','_nativeIterations[slot - _firstTemporarySlot].Range.Position',2),
 ('            Array.Clear(_items, newCount, LiveExtent - newCount);','''            if (_nativeIterations is not null)
            {
                var start = Math.Max(newCount, _firstTemporarySlot);
                if (start < LiveExtent) Array.Clear(_nativeIterations, start - _firstTemporarySlot, LiveExtent - start);
            }
            Array.Clear(_items, newCount, LiveExtent - newCount);''',1),
 ])
edit('Runtime/Text/PyStringOps.Whitespace.cs',[
 ('        var mapping = upper ? CaseMapping.Upper : CaseMapping.Lower;\n','',1)])
edit('Runtime/LythonRuntime.Executable.CasedSplits.cs',[
 ('private static object ExecuteCasedSplit(ExecutableCasedSplit recipe, PyString receiver, ExecutionContext context,\n        bool consumedByIteration = false)','private static void ExecuteCasedSplit(ExecutableCasedSplit recipe, PyString receiver, ExecutionContext context,\n        out ExecutableResult result, bool consumedByIteration = false)',1),
 ('''        return consumedByIteration
            ? PyStringOps.PrepareCasedWhitespaceIteration(receiver, recipe.Upper, context, recipe.SplitCallSpan)
            : PyStringOps.SplitCasedWhitespace(receiver, recipe.Upper,
                context.MemoryGovernor, recipe.SplitCallSpan, context);''','''        if (consumedByIteration)
        {
            PyStringOps.PrepareCasedWhitespaceIteration(receiver, recipe.Upper, context, recipe.SplitCallSpan);
            result = new(receiver) { IterationContext = context, CasedSplit = recipe };
        }
        else result = new(PyStringOps.SplitCasedWhitespace(receiver, recipe.Upper,
            context.MemoryGovernor, recipe.SplitCallSpan, context));''',1),
 ('                object result;','                ExecutableResult result;',1),
 ('try { result = ExecuteCasedSplit(recipe, receiver, context, CallResultConsumedByIteration(in execution, resultCall)); }','try { ExecuteCasedSplit(recipe, receiver, context, out result, CallResultConsumedByIteration(in execution, resultCall)); }',1)])
edit('Runtime/LythonRuntime.Executable.Results.cs',[
 ('''        private static void PublishInstructionResult(ref ExecutableWorkState execution,''','''        private void PublishInstructionResult(in ExecutableInstruction instruction, in ExecutableResult result)
            => PublishInstructionResult(ref _execution, this, instruction, result);

        private static void PublishInstructionResult(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, in ExecutableResult result)
        {
            if (result.CasedSplit is null)
                PublishInstructionResult(ref execution, invocation, instruction, result.Value.RequireNotNull());
            else
            {
                if (instruction.HasLocalResult)
                    throw new InvalidOperationException("Private iteration cannot be published into a Python binding.");
                execution.Stack.PublishIterationResult(instruction.ResultSlot, instruction.LiveExtent, result);
            }
        }

        private static void PublishInstructionResult(ref ExecutableWorkState execution,''',1)])
edit('Runtime/LythonRuntime.Executable.Interpreter.Dispatch.cs',[
 ('                                instruction.ResultSlot, instruction.LiveExtent, instruction.Span)) break;','                                instruction.InputStart, instruction.ResultSlot, instruction.LiveExtent, instruction.Span)) break;',1),
 ('execution.ReturnValue is not null','execution.ReturnValue.Value is not null',1)])
edit('Runtime/LythonRuntime.ExecutionSupport.cs',[
 ('''            Value = value;
        }

        public object Value { get; }''','''            Result = new(value);
        }

        internal ReturnSignal(in ExecutableResult result) { Result = result; }

        internal ExecutableResult Result { get; }
        public object Value => Result.Value.RequireNotNull();''',1)])
edit('Runtime/LythonRuntime.Executable.Binding.cs',[
 ('throw new ReturnSignal(returned.Value);','throw new ReturnSignal(returned.Result);',1)])
edit('Runtime/LythonRuntime.Executable.Interpreter.cs',[
 ('_execution.ReturnValue = null;','_execution.ReturnValue = default;',1),
 ('_completedCallValue = null;','_completedCallResult = default;',1),
 ('ExecutionContext? invocation, object value, LythonSourceSpan span)','ExecutionContext? invocation, in ExecutableResult value, LythonSourceSpan span)',1),
 ('''                case ExecutableOpCode.Return:
                    return DeliverReturn(ref execution, invocation, TakeOperand(ref execution, instruction, scope), instruction.Span);''','''                case ExecutableOpCode.Return:
                    var result = execution.Stack.ReadResult(instruction.InputStart,
                        ReadOperand(ref execution, instruction.InputStart, scope, 0));
                    _ = TakeOperand(ref execution, instruction, scope);
                    return DeliverReturn(ref execution, invocation, result, instruction.Span);''',1),
 ('DeliverReturn(ref execution, invocation, PyNone.Instance, instruction.Span)','DeliverReturn(ref execution, invocation, new ExecutableResult(PyNone.Instance), instruction.Span)',1),
 ('_execution.ReturnValue is not null','_execution.ReturnValue.Value is not null',2),
 ('_execution.ReturnValue.RequireNotNull()','_execution.ReturnValue.Value.RequireNotNull()',1)])
edit('Runtime/LythonRuntime.Executable.Interpreter.Routing.cs',[
 ('new PendingReturn(signal.Value)','new PendingReturn(signal.Result)',1),
 ('_execution.ReturnValue = signal.Value;','_execution.ReturnValue = signal.Result;',1)])
edit('Runtime/LythonRuntime.Executable.NativeReturn.cs',[
 ('out object result','out ExecutableResult result',2),
 ('result = PyNone.Instance;','result = default;',2),
 ('result = ExecuteNativeBody(codeObject, body, context, arguments, entrySpan, consumedByIteration);','ExecuteNativeBody(codeObject, body, context, arguments, entrySpan, ref result, consumedByIteration);',1),
 ('result = ExecuteNativeBody(code, body, context, arguments, span, consumedByIteration);','ExecuteNativeBody(code, body, context, arguments, span, ref result, consumedByIteration);',1),
 ('private static object ExecuteNativeBody','private static void ExecuteNativeBody',1),
 ('ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan,\n        bool consumedByIteration = false)','ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan, ref ExecutableResult result,\n        bool consumedByIteration = false)',1),
 ('            object result = PyNone.Instance;','            result = new(PyNone.Instance);',1),
 ('''                result = EvaluateNativeReturn(body.Root + code.FirstTemporarySlot, code, body.Values, arguments, context,
                    entryException, consumedByIteration);''','''                var value = EvaluateNativeReturn(body.Root + code.FirstTemporarySlot, code, body.Values, arguments, context,
                    entryException, ref result, consumedByIteration);
                result.Value = value;''',1),
 ('            return result;\n','',1),
 ('bool consumedByIteration = false)\n    {\n        if (index < code.FirstTemporarySlot)','ref ExecutableResult returned, bool consumedByIteration = false)\n    {\n        if (index < code.FirstTemporarySlot)',1),
 ('context, entryException);','context, entryException, ref returned);',7),
 ('context, entryException,\n                consumedByIteration);','context, entryException,\n                ref returned, consumedByIteration);',1),
 ('context, entryException, consumedByIteration);','context, entryException, ref returned, consumedByIteration);',1),
 ('''                try { return ExecuteCasedSplit(code.CallSites[instruction.CallSiteIndex].CasedSplit!, (PyString)first,
                    context, consumedByIteration); }''','''                try
                {
                    ExecuteCasedSplit(code.CallSites[instruction.CallSiteIndex].CasedSplit!, (PyString)first,
                        context, out var split, consumedByIteration);
                    if (consumedByIteration) returned = split;
                    return split.Value.RequireNotNull();
                }''',1)])
edit('Runtime/PyExecutableFunction.cs',[
 ('''        => TryExecuteWithoutFrame(arguments, span, false, out result);''','''    {
        var completed = TryExecuteWithoutFrame(arguments, span, false, out var value);
        result = value.Value ?? PyNone.Instance;
        return completed;
    }''',1),
 ('bool consumedByIteration, out object result)','bool consumedByIteration, out LythonRuntime.ExecutableResult result)',3),
 ('result = PyNone.Instance;','result = default;',2)])
edit('Runtime/LythonRuntime.Executable.Calls.Invoke.cs',[
 ('                    object value;','                    ExecutableResult value;',1)])
edit('Runtime/LythonRuntime.Executable.Calls.cs',[
 ('private object? _completedCallValue;','private ExecutableResult _completedCallResult;',1),
 ('current.CompletePendingCall(null, error);','current.CompletePendingCall(default, error);',1),
 ('var result = current.HasFrameReturn ? current.FrameReturnValue : PyNone.Instance;','var result = current.HasFrameReturn ? current._execution.ReturnValue : new ExecutableResult(PyNone.Instance);',1),
 ('CompletePendingCall(null, error);','CompletePendingCall(default, error);',3),
 ('CompletePendingCall(work.ReturnValue ?? PyNone.Instance, null);','CompletePendingCall(work.ReturnValue.Value is null ? new ExecutableResult(PyNone.Instance) : work.ReturnValue, null);',1),
 ('private void CompletePendingCall(object? value, Exception? failure)','private void CompletePendingCall(in ExecutableResult value, Exception? failure)',1),
 ('_completedCallValue = value;','_completedCallResult = value;',1),
 ('var value = _completedCallValue;','var result = _completedCallResult;\n            var value = result.Value;',1),
 ('_completedCallValue = null;','_completedCallResult = default;',1),
 ('else value = instance;','else { value = instance; result = new(instance); }',1),
 ('PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], RuntimeValue(value));','PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], result);',1)])
test=Path('tests/Lokad.Lython.Tests/SimpleFunctionExecutionBoundaryTests.cs');t=test.read_text(encoding='utf-8')
old='Assert.True(Assert.IsType<bool>(result));';assert t.count(old)==1;t=t.replace(old,'Assert.True(Assert.IsType<bool>(result.Value));');test.write_text(t,encoding='utf-8')
print('Unified private return/iteration state migration applied. Compile/test errors remain unqualified until focused checks.')
