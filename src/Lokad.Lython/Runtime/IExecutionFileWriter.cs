namespace Lokad.Lython.Runtime;

// Writers retain their staged content until close or execution-end publication.
// The shared run state owns the same lifecycle for text and binary handles;
// gzip readers use it to retire native inflater storage before their owner ends.
internal interface IExecutionFileWriter
{
    object Exit(LythonRuntime.ExecutionContext context);

    ValueTask<object> ExitAsync(LythonRuntime.ExecutionContext context);
}
