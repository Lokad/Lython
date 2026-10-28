namespace Lokad.Lython.Runtime;

// The already-started operation has settled. File cleanup must not retry it:
// success may have published all bytes, and a fault may have published a prefix.
internal sealed class SynchronousHostContractException : LythonRuntimeException
{
    public SynchronousHostContractException(string operation, LythonSourceSpan? span, Exception? completionFailure)
        : base("RuntimeError", $"{operation} violated its synchronous host capability contract.", span, completionFailure)
    {
        Attribution = new(LythonFailureReason.HostOperationFailed);
    }
}
