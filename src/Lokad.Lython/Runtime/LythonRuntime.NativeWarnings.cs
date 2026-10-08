using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Native features use the default Python warning action. This does not
    // expose the separate warnings module/filter/custom-hook API. stderr is
    // mediated and captured through the same output handle as sys.stderr.
    internal static async ValueTask EmitDefaultWarningAsync(string category, string message,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context.CheckExecution(span);
        var filename = context.SourcePath ?? "<string>";
        if (!context.State.MarkDefaultWarning(filename, span.Line, category, message, span)) return;
        // Fund managed formatting independently of the resulting UTF-8 value
        // and the captured output. No source file lookup or ambient I/O occurs.
        using var formatting = context.MemoryGovernor.ReserveTemporary(
            256L + 4L * Encoding.UTF8.GetByteCount(filename) + 4L * Encoding.UTF8.GetByteCount(message), span);
        var text = PyString.FromString(filename + ":" + span.Line.ToString(CultureInfo.InvariantCulture)
            + ": " + category + ": " + message + "\n", context.MemoryGovernor, span);
        try
        {
            if (asynchronous) await context.State.Stderr.WriteAsync(text, span).ConfigureAwait(false);
            else context.State.Stderr.Write(text, span);
        }
        finally { context.MemoryGovernor.Release(text.CommittedOwnedBytes); }
    }
}
