namespace Lokad.Lython.Runtime;

// Writers retain their staged content until close or execution-end publication.
// The shared run state owns the same lifecycle for text and binary handles.
internal interface IExecutionFileWriter
{
    object Exit();

    ValueTask<object> ExitAsync();
}
