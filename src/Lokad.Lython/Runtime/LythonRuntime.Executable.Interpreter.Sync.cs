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
                _ = ExecuteInstructionPrefix(false, out _, out _);
            }
            finally
            {
                if (!HasYield && !HasPendingCall)
                {
                    _preparedCallValue = null;
                    AbandonFrame();
                    _stack.ReleaseStorage();
                }
            }
        }
    }
}
