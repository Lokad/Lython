namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private void ExecuteUntilCall()
        {
            try
            {
                ResumePendingCall();
                RoutePendingFrameException();
                _ = ExecuteInstructionPrefix(ref _execution, this, this, false, out _, out _);
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
