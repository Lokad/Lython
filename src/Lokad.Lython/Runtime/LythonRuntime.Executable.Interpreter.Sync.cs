namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private ExecutionContext? ExecuteUntilCall()
        {
            try
            {
                ResumePendingCall();
                RoutePendingFrameException();
                while (true)
                {
                    var pause = ExecuteInstructionPrefix(ref _execution, this, this, false, out _, out _, out var nextCall);
                    if (pause != InstructionPause.Call) return null;
                    var child = nextCall.GetAwaiter().GetResult();
                    if (!ReferenceEquals(child, this)) return child;
                }
            }
            finally
            {
                if (!HasYield && !HasPendingCall)
                {
                    _preparedCallValue = null;
                    AbandonFrame();
                    _execution.Stack.ReleaseStorage();
                }
            }
        }
    }
}
