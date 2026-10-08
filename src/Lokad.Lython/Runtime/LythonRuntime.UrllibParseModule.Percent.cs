using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class UrllibParseModule
    {
        internal static PyString QuoteBytes(ReadOnlySpan<byte> source, SafeSet safe, bool plus,
            ExecutionContext context, LythonSourceSpan span)
        {
            long length = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                length += safe.Contains(source[i]) ? 1 : 3;
            }
            CheckTextLength(length, context, span);
            if (length == 0) return PyString.Empty;
            if (length > int.MaxValue) throw RuntimeErrors.Runtime("quoted URL exceeds the supported buffer size", span);
            context.MemoryGovernor.EnsureCanReserve(PyString.EstimateApproximateBytes((int)length), span);
            var output = new byte[(int)length];
            var offset = 0;
            const string hex = "0123456789ABCDEF";
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                var b = source[i];
                if (plus && b == 32 && safe.Contains(b)) output[offset++] = (byte)'+';
                else if (safe.Contains(b)) output[offset++] = b;
                else
                {
                    output[offset++] = (byte)'%';
                    output[offset++] = (byte)hex[b >> 4];
                    output[offset++] = (byte)hex[b & 15];
                }
            }
            var result = PyString.FromOwnedUtf8(output, context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshString(result, span);
            return result;
        }

        private static PyBytes OwnBytes(byte[] value, ExecutionContext context, LythonSourceSpan span)
        {
            if (value.Length == 0) return EmptyBytes;
            var result = new PyBytes(value, context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            return result;
        }

        internal static PyBytes PercentDecodeBytes(ReadOnlySpan<byte> source, ExecutionContext context, LythonSourceSpan span, bool plus = false)
        {
            if (source.Length == 0) return EmptyBytes;
            var length = source.Length;
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (IsEscape(source, i)) { length -= 2; i += 2; }
            }
            context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(length), span);
            var output = new byte[length];
            var offset = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (IsEscape(source, i))
                {
                    output[offset++] = (byte)((Hex(source[i + 1]) << 4) | Hex(source[i + 2]));
                    i += 2;
                }
                else output[offset++] = plus && source[i] == (byte)'+' ? (byte)' ' : source[i];
            }
            return OwnBytes(output, context, span);
        }

        private static bool IsEscape(ReadOnlySpan<byte> source, int i)
            => source[i] == (byte)'%' && source.Length - i >= 3 && Hex(source[i + 1]) >= 0 && Hex(source[i + 2]) >= 0;

        private static int Hex(byte b) => b is >= (byte)'0' and <= (byte)'9' ? b - '0'
            : b is >= (byte)'a' and <= (byte)'f' ? b - 'a' + 10
            : b is >= (byte)'A' and <= (byte)'F' ? b - 'A' + 10 : -1;

        private static PyString DecodeOwned(ReadOnlyMemory<byte> source, TextEncodingMode encoding,
            TextErrorMode errors, ExecutionContext context, LythonSourceSpan span)
        {
            var result = DecodeText(source, encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
            context.State.CallTemporaries.TrackFreshString(result, span);
            context.ObserveString(result, span);
            return result;
        }

        private static PyString UnquoteText(PyString source, TextEncodingMode encoding, TextErrorMode errors,
            ExecutionContext context, LythonSourceSpan span)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var bytes = source.Utf8Bytes.Span;
            long runes = 0;
            try
            {
                for (var i = 0; i < bytes.Length;)
                {
                    context.CheckExecution(span);
                    var start = i;
                    if (bytes[i] >= 128)
                    {
                        while (i < bytes.Length && bytes[i] >= 128)
                        {
                            if ((i & 1023) == 0) context.CheckExecution(span);
                            _ = Rune.DecodeFromUtf8(bytes[i..], out _, out var consumed);
                            i += consumed;
                            runes++;
                        }
                        CheckTextLength(runes, context, span);
                        builder.Append(bytes[start..i]);
                    }
                    else
                    {
                        while (i < bytes.Length && bytes[i] < 128)
                        {
                            if ((i & 1023) == 0) context.CheckExecution(span);
                            i++;
                        }
                        var unquoted = PercentDecodeBytes(bytes[start..i], context, span);
                        try
                        {
                            var decoded = DecodeOwned(unquoted.Memory, encoding, errors, context, span);
                            runes += decoded.Length;
                            CheckTextLength(runes, context, span);
                            builder.Append(decoded);
                            GC.KeepAlive(decoded);
                        }
                        finally { GC.KeepAlive(unquoted); }
                    }
                }
                var result = builder.ToPyStringAndRelease();
                context.State.CallTemporaries.TrackFreshString(result, span);
                return result;
            }
            finally { builder.Release(); GC.KeepAlive(source); }
        }

        private static void CheckTextLength(long length, ExecutionContext context, LythonSourceSpan span)
        {
            if (context.Limits.MaxStringLength is { } maximum && length > maximum)
                throw RuntimeErrors.Runtime($"maximum string length exceeded ({maximum})", span);
        }
    }
}
