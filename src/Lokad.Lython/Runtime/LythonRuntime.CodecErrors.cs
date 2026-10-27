using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Native codec failures use the existing exception args/attributes. Their
    // retained source is owned before publication, including stream windows.
    private static LythonRuntimeException DecodeError(ReadOnlySpan<byte> source, string encoding,
        int start, int end, string reason, MemoryGovernor governor, LythonSourceSpan? span, ExecutionGuards? guards)
    {
        guards?.CheckExecution(span);
        using var storage = governor.ReserveTemporary(PyBytes.EstimateApproximateBytes(source.Length), span);
        var data = new PyBytes(source.ToArray(), governor, span, storage);
        return CodecError("UnicodeDecodeError", encoding, data, start, end, reason, governor, span, guards);
    }

    private static LythonRuntimeException CodecError(string type, string encoding, object data, int start, int end,
        string reason, MemoryGovernor governor, LythonSourceSpan? span, ExecutionGuards? guards)
    {
        var name = PyString.FromString(encoding, governor, span);
        var explanation = PyString.FromString(reason, governor, span);
        var args = new PyTuple([name, data, new BigInteger(start), new BigInteger(end), explanation], governor, span);
        var rendered = FormatCodecError(type, name, data, start, end, explanation, governor, span, guards);
        // Both the native exception and guest value borrow this one message.
        var messageBytes = checked(32L + 2L * rendered.Utf8Bytes.Length);
        governor.Reserve(messageBytes, span);
        string message;
        try { message = rendered.AsString(); }
        catch { governor.ReleaseReserved(messageBytes); throw; }
        governor.Commit(messageBytes);
        var identity = PythonExceptionIdentity.FromRuntimeTypeName(type);
        var payload = new PyDict(governor, span);
        string[] fields = ["encoding", "object", "start", "end", "reason"];
        for (var i = 0; i < fields.Length; i++) payload.SetItem(PyString.FromString(fields[i]), args[i]);
        var value = PyException.CreateOwned(identity, message, payload, governor, span, args);
        return new LythonRuntimeException(identity, message, span) { OriginalPythonException = value };
    }

    internal static async ValueTask<PyString?> RenderCodecErrorAsync(PyException exception, PyRenderingContext context, bool asynchronous)
    {
        if (!exception.Identity.IsBuiltin || exception.TypeName is not ("UnicodeDecodeError" or "UnicodeEncodeError") ||
            exception.Value is not PyDict attributes)
            return null;
        if (!attributes.TryGetValue(PyString.FromString("object"), out _)) return PyString.Empty;
        // Python renders reason before encoding, then reads the object/span:
        // either callback can edit the remaining native fields.
        var why = attributes.TryGetValue(PyString.FromString("reason"), out var rawReason) ? rawReason : PyString.FromString("<NULL>");
        var reason = asynchronous ? await PyRendering.ToInterpolatedPyStringAsync(why, context).ConfigureAwait(false)
            : PyRendering.ToInterpolatedPyString(why, context);
        var name = attributes.TryGetValue(PyString.FromString("encoding"), out var rawName) ? rawName : PyString.FromString("<NULL>");
        var encoding = asynchronous ? await PyRendering.ToInterpolatedPyStringAsync(name, context).ConfigureAwait(false)
            : PyRendering.ToInterpolatedPyString(name, context);
        if (!attributes.TryGetValue(PyString.FromString("object"), out var data)) return PyString.Empty;
        if (
            !attributes.TryGetValue(PyString.FromString("start"), out var first) || !PyNumberOps.TryAsInteger(first, out var start) ||
            !attributes.TryGetValue(PyString.FromString("end"), out var last) || !PyNumberOps.TryAsInteger(last, out var end) ||
            start < long.MinValue || start > long.MaxValue || end < long.MinValue || end > long.MaxValue)
            return null;
        if (exception.TypeName == "UnicodeDecodeError" ? data is not PyBytes : data is not PyString)
            throw RuntimeErrors.Type(exception.TypeName == "UnicodeDecodeError"
                ? "object attribute must be bytes" : "object attribute must be unicode", null);
        return FormatCodecError(exception.TypeName, encoding, data, (long)start, (long)end, reason,
            context.Context.MemoryGovernor, null, context.Context.State.Guards);
    }

    private static PyString FormatCodecError(string type, PyString encoding, object data, long start, long end,
        PyString reason, MemoryGovernor governor, LythonSourceSpan? span, ExecutionGuards? guards)
    {
        var builder = new GovernedByteBuilder(governor, span);
        try
        {
            builder.Append((byte)'\''); builder.Append(encoding);
            builder.AppendAscii(type == "UnicodeDecodeError" ? "' codec can't decode " : "' codec can't encode ");
            if (start < long.MaxValue && end == start + 1 && start >= 0 && data is PyBytes bytes && start < bytes.Length)
            {
                builder.AppendAscii("byte 0x" + bytes.Bytes[(int)start].ToString("x2") + " in position ");
                builder.AppendAscii(start.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (start < int.MaxValue && end == start + 1 && start >= 0 && data is PyString text && TryRune(text, (int)start, guards, span, out var rune))
            {
                var escaped = rune.Value <= 255 ? "\\x" + rune.Value.ToString("x2")
                    : rune.Value <= 65535 ? "\\u" + rune.Value.ToString("x4") : "\\U" + rune.Value.ToString("x8");
                builder.AppendAscii("character '" + escaped + "' in position ");
                builder.AppendAscii(start.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                builder.AppendAscii(type == "UnicodeDecodeError" ? "bytes in position " : "characters in position ");
                builder.AppendAscii(start.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append((byte)'-');
                builder.AppendAscii((new BigInteger(end) - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            builder.AppendAscii(": "); builder.Append(reason);
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static bool TryRune(PyString text, int index, ExecutionGuards? guards, LythonSourceSpan? span, out Rune rune)
    {
        var position = 0;
        var source = text.Utf8Bytes.Span;
        for (var offset = 0; offset < source.Length;)
        {
            if ((position & 1023) == 0) guards?.CheckExecution(span);
            _ = Rune.DecodeFromUtf8(source[offset..], out var current, out var consumed);
            if (position++ == index) { rune = current; return true; }
            offset += consumed;
        }
        rune = default;
        return false;
    }
}
