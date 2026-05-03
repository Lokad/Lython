namespace Lokad.Lython;

public interface ILythonSubprocessRunner
{
    ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request, CancellationToken cancellationToken);
}
