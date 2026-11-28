namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private struct GeneratorRowBuffer(ExecutableSlot[] destination, int limit, int start)
        {
            internal int Position = start;

            internal bool TryAppend(ref ExecutableWorkState work, in Frontend.ExecutableInstruction instruction)
            {
                if (Position >= limit) return false;
                destination[Position++] = work.Stack.TakeSlotValue(instruction.InputStart, instruction.ResultSlot);
                return true;
            }
        }

        internal static async ValueTask<(StagedGeneratorYield Staged, int Position)> RunStagedGeneratorPrefixAsync(
            StagedGeneratorYield staged, ExecutableSlot[] destination, int limit, int start)
        {
            // Like async frame advancement, isolate ambient state from host
            // admission callbacks even when this prefix completes synchronously.
            await ValueTask.CompletedTask;
            return RunStagedGeneratorPrefix(staged, destination, limit, start);
        }

        internal static (StagedGeneratorYield Staged, int Position) RunStagedGeneratorPrefix(
            StagedGeneratorYield staged, ExecutableSlot[] destination, int limit, int start)
        {
            var function = staged.Function!;
            var scope = function.DefinitionContext;
            var code = function.CodeObject;
            var span = staged.Span!;
            var entryException = scope.State.CurrentException;
            ExecutableWorkState work = default;
            var row = new GeneratorRowBuffer(destination, limit, start);
            var enteredFunction = false;
            var enteredInterpreter = false;
            var transferred = false;
            try
            {
                scope.EnterFunctionCall(span);
                enteredFunction = true;
                scope.EnterInterpreterFrame(span);
                enteredInterpreter = true;
                InitializeExecutableWorkState(ref work, code, scope, staged.Arguments, function.Captures,
                    lookupOwner: function, borrowedInvocation: true);
                var pause = ExecuteInstructionPrefix(ref work, scope, null, true, out _, out _, out _, row: new(ref row));
                if (pause == InstructionPause.Boundary) return (default, row.Position);
                if (pause != InstructionPause.ContextRequired)
                    throw new InvalidOperationException("A private row prefix cannot suspend.");

                var frame = function.EnterInvocationFrame(staged.Arguments, span, enterFunctionCall: false);
                frame._boundStorageOwner = staged.Arguments.StorageOwner;
                frame.AdoptExecutableExecution(ref work, entryException);
                transferred = true;
                return (new(frame, span), row.Position);
            }
            catch (LythonRuntimeException exception)
            {
                PyFunctionBinding.AnnotateException(exception, scope, function.Name, span);
                if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                    throw GeneratorRaisedStopIteration(exception, scope, span);
                throw;
            }
            finally
            {
                try
                {
                    if (work.Code is not null)
                    {
                        try { work.Stack.UnwindTo(code.FirstTemporarySlot); }
                        finally { work.Stack.ReleaseStorage(); }
                    }
                }
                finally
                {
                    try { if (!transferred) staged.Release(); }
                    finally
                    {
                        scope.State.SetCurrentException(entryException);
                        if (enteredInterpreter) scope.LeaveInterpreterFrame();
                        if (enteredFunction) scope.LeaveFunctionCall();
                    }
                }
            }
        }
    }
}
