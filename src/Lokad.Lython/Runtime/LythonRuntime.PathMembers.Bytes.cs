using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private static async ValueTask<object> ReadPathBytesAsync(PyString pathValue, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            using var pathScratch = PathOps.ReserveWorking(pathValue, context, span);
            var path = pathValue.AsString();
            using var acquired=asynchronous?await ReadGovernedHostBytesAsync(path,context,span).ConfigureAwait(false)
                :ReadGovernedHostBytes(path,context,span);
            return acquired.TakeValue();
        }

        private static async ValueTask<object> WritePathBytesAsync(PyString pathValue, object value,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            using var pathScratch = PathOps.ReserveWorking(pathValue, context, span);
            var path = pathValue.AsString();
            if (value is not PyBytes bytes)
                throw RuntimeErrors.Type("a bytes-like object is required", span);
            // Immutable guest storage remains funded through the actual borrow
            // and native close; the host must copy any bytes it retains afterwards.
            if(asynchronous)await context.WriteHostBytesAsync(path,bytes.Memory,span).ConfigureAwait(false);
            else context.WriteHostBytes(path,bytes.Memory,span);
            GC.KeepAlive(bytes);
            return new BigInteger(bytes.Length);
        }
    }
}
