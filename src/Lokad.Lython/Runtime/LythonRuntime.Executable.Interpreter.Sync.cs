namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
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
                    _preparedTypeSubscription = null;
                    _stack.RemoveTail(_stack.Count);
                    _stack.ReleaseStorage();
                }
            }
        }
    }
}
