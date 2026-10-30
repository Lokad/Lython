namespace Lokad.Lython.Runtime;

// Execution-owned text/binary files and gzip streams close after actual users.
// Writers also publish staged content unless admission/cancellation is terminal.
internal interface IExecutionFile
{
    object Exit(LythonRuntime.ExecutionContext context);

    ValueTask<object> ExitAsync(LythonRuntime.ExecutionContext context);
}
