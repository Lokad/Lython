using System.Numerics;

namespace Lokad.Lython.Runtime;

// The binary protocol uses ordinary guest attribute/call dispatch. It never
// exposes a CLR stream, and async descriptors and methods keep their suspension.
internal static class PyBinaryStream
{
    internal static async ValueTask<object> CallAsync(object stream, string name, object[] arguments,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var member = asynchronous
            ? await PyTextStream.ResolveMemberAsync(stream, name, span, context).ConfigureAwait(false)
            : PyTextStream.ResolveMember(stream, name, span, context);
        var values = arguments.Select(CallArgumentValue.Positional).ToArray();
        var value = asynchronous
            ? await LythonRuntime.InvokeCallableTargetAsync(member, span, span, context,
                () => ValueTask.FromResult(values)).ConfigureAwait(false)
            : LythonRuntime.InvokeCallableTarget(member, span, span, context, () => values);
        context.State.CallTemporaries.TrackCallResult(value, span);
        return value;
    }

    internal static async ValueTask<PyBytes> ReadAsync(object stream, int size,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var value = await CallAsync(stream, "read", [new BigInteger(size)], span, context, asynchronous).ConfigureAwait(false);
        if (value is not PyBytes bytes)
            throw new LythonRuntimeException("TypeError", "binary stream read() must return bytes", span);
        if (bytes.Length > size)
            throw new LythonRuntimeException("ValueError", "binary stream read() returned more bytes than requested", span);
        return bytes;
    }
}
