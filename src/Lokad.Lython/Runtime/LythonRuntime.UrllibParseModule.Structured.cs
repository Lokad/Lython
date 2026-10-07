using System.Numerics;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class UrllibParseModule
    {
        private static readonly HashSet<string> UsesNetloc = new(StringComparer.Ordinal)
        {
            "", "ftp", "http", "gopher", "nntp", "telnet", "imap", "wais", "file", "mms",
            "https", "shttp", "snews", "prospero", "rtsp", "rtsps", "rtspu", "rsync", "svn",
            "svn+ssh", "sftp", "nfs", "git", "git+ssh", "ws", "wss", "itms-services",
        };
        private static readonly HashSet<string> UsesParams = new(StringComparer.Ordinal)
        { "", "ftp", "hdl", "prospero", "http", "imap", "https", "shttp", "rtsp", "rtsps", "rtspu", "sip", "sips", "mms", "sftp", "tel" };

        private static async ValueTask<object> ParseUrlAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var textInput = args[0] is PyString;
            if (await TruthAsync(args[1], span, context, asynchronous).ConfigureAwait(false) && (args[1] is PyString) != textInput)
                throw RuntimeErrors.Type("Cannot mix str and non-str arguments", span);
            var url = await UrlInputAsync(args[0], textInput, span, context, asynchronous).ConfigureAwait(false);
            object schemeValue = textInput ? args[1] : await UrlInputAsync(args[1], false, span, context, asynchronous).ConfigureAwait(false);
            var split = (UrlResult)await CachedUrlSplitAsync(
                [CallArgumentValue.Positional(url), CallArgumentValue.Positional(schemeValue), CallArgumentValue.Positional(args[2])],
                span, context, asynchronous).ConfigureAwait(false);
            var scheme = (PyString)split[0];
            var path = (PyString)split[2];
            var param = PyString.Empty;
            if (UsesParams.Contains(scheme.AsString()))
            {
                var source = path.Utf8Bytes;
                var segment = 0;
                for (var i = 0; i < source.Length; i++)
                { if ((i & 1023) == 0) context.CheckExecutionBudget(span); if (source.Span[i] == '/') segment = i + 1; }
                var semicolon = FindByte(source.Span, (byte)';', segment, source.Length, context, span);
                if (semicolon >= 0)
                {
                    param = OwnedText(source[(semicolon + 1)..], context, span);
                    path = OwnedText(source[..semicolon], context, span);
                }
            }
            var type = UrlResultType.Get(true, !textInput);
            context.MemoryGovernor.EnsureCanReserve(64 + PyTuple.EstimateApproximateBytes(type.Fields.Length), span);
            object[] values = [scheme, split[1], path, param, split[3], split[4]];
            if (!textInput)
                for (var i = 0; i < values.Length; i++) values[i] = AsciiBytes((PyString)values[i], context, span);
            var result = UrlResult.Create(type, values, context, span);
            GC.KeepAlive(split); GC.KeepAlive(args);
            return result;
        }

        private static async ValueTask<object> SplitUrlCoreAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var textInput = args[0] is PyString;
            if (await TruthAsync(args[1], span, context, asynchronous).ConfigureAwait(false) && (args[1] is PyString) != textInput)
                throw RuntimeErrors.Type("Cannot mix str and non-str arguments", span);
            var url = await UrlInputAsync(args[0], textInput, span, context, asynchronous).ConfigureAwait(false);
            object schemeValue = textInput ? args[1] : await UrlInputAsync(args[1], false, span, context, asynchronous).ConfigureAwait(false);
            var scheme = await UrlInputAsync(schemeValue, true, span, context, asynchronous).ConfigureAwait(false);
            url = CleanUrlText(url, false, context, span);
            scheme = CleanUrlText(scheme, true, context, span);
            var fragments = await TruthAsync(args[2], span, context, asynchronous).ConfigureAwait(false);
            var source = url.Utf8Bytes;
            var start = 0;
            var length = source.Length;
            var colon = FindByte(source.Span, (byte)':', 0, length, context, span);
            if (colon > 0 && IsAsciiAlpha(source.Span[0]))
            {
                var valid = true;
                for (var i = 0; i < colon; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecutionBudget(span);
                    var c = source.Span[i];
                    if (!(IsAsciiAlpha(c) || c is >= (byte)'0' and <= (byte)'9' or (byte)'+' or (byte)'-' or (byte)'.')) { valid = false; break; }
                }
                if (valid)
                {
                    scheme = LowerHost(OwnedText(source[..colon], context, span), context, span, false);
                    start = colon + 1;
                }
            }
            var netloc = PyString.Empty;
            if (length - start >= 2 && source.Span[start] == '/' && source.Span[start + 1] == '/')
            {
                var end = start + 2;
                while (end < length && source.Span[end] is not ((byte)'/' or (byte)'?' or (byte)'#'))
                { if ((end & 1023) == 0) context.CheckExecutionBudget(span); end++; }
                netloc = OwnedText(source.Slice(start + 2, end - start - 2), context, span);
                ValidateBrackets(netloc, context, span);
                start = end;
            }
            var fragment = PyString.Empty;
            var query = PyString.Empty;
            var fragmentAt = fragments ? FindByte(source.Span, (byte)'#', start, length, context, span) : -1;
            if (fragmentAt >= 0)
            {
                fragment = OwnedText(source[(fragmentAt + 1)..], context, span);
                length = fragmentAt;
            }
            var queryAt = FindByte(source.Span, (byte)'?', start, length, context, span);
            if (queryAt >= 0)
            {
                query = OwnedText(source.Slice(queryAt + 1, length - queryAt - 1), context, span);
                length = queryAt;
            }
            ValidateNetloc(netloc, context, span);
            var path = OwnedText(source.Slice(start, length - start), context, span);
            var type = UrlResultType.Get(false, !textInput);
            context.MemoryGovernor.EnsureCanReserve(64 + PyTuple.EstimateApproximateBytes(type.Fields.Length), span);
            object[] values = [scheme, netloc, path, query, fragment];
            if (!textInput)
                for (var i = 0; i < values.Length; i++) values[i] = AsciiBytes((PyString)values[i], context, span);
            var result = UrlResult.Create(type, values, context, span);
            GC.KeepAlive(url); GC.KeepAlive(args);
            return result;
        }

        private static async ValueTask<PyString> UrlInputAsync(object value, bool textInput, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (textInput)
            {
                if (value is PyString text) return text;
                throw new LythonRuntimeException(value is PyBytes ? "TypeError" : "AttributeError", "URL text argument must be a string", span);
            }
            if (!await TruthAsync(value, span, context, asynchronous).ConfigureAwait(false)) return PyString.Empty;
            if (value is PyBytes bytes) return DecodeOwned(bytes.Memory, TextEncodingMode.Ascii, TextErrorMode.Strict, context, span);
            var method = asynchronous ? await PyTextStream.ResolveMemberAsync(value, "decode", span, context).ConfigureAwait(false)
                : PyTextStream.ResolveMember(value, "decode", span, context);
            var decoded = await CallAsync(method, [CallArgumentValue.Positional(AsciiName), CallArgumentValue.Positional(StrictName)], span, context, asynchronous).ConfigureAwait(false);
            return decoded as PyString ?? throw RuntimeErrors.Type("decoded URL must be a string", span);
        }

        private static PyString CleanUrlText(PyString text, bool bothEnds, ExecutionContext context, LythonSourceSpan span)
        {
            var source = text.Utf8Bytes;
            var start = 0; var end = source.Length;
            while (start < end && source.Span[start] <= 32)
            { if ((start & 1023) == 0) context.CheckExecutionBudget(span); start++; }
            if (bothEnds)
                while (end > start && source.Span[end - 1] <= 32)
                { if ((end & 1023) == 0) context.CheckExecutionBudget(span); end--; }
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                var part = start; var removed = false;
                for (var i = start; i < end; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecutionBudget(span);
                    if (source.Span[i] is 9 or 10 or 13)
                    { builder.Append(source.Span[part..i]); part = i + 1; removed = true; }
                }
                if (!removed) return start == 0 && end == source.Length ? text : OwnedText(source.Slice(start, end - start), context, span);
                builder.Append(source.Span[part..end]);
                var result = builder.ToPyStringAndRelease();
                context.State.CallTemporaries.TrackFreshString(result, span);
                return result;
            }
            finally { builder.Release(); GC.KeepAlive(text); }
        }

        private static int FindByte(ReadOnlySpan<byte> source, byte value, int start, int end, ExecutionContext context, LythonSourceSpan span)
        {
            for (var i = start; i < end; i++)
            { if ((i & 1023) == 0) context.CheckExecutionBudget(span); if (source[i] == value) return i; }
            return -1;
        }
        private static bool IsAsciiAlpha(byte c) => c is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';
        private static bool IsHex(byte c) => c is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f';
        private static PyString OwnedText(ReadOnlyMemory<byte> bytes, ExecutionContext context, LythonSourceSpan span)
        {
            if (bytes.IsEmpty) return PyString.Empty;
            var result = PyString.FromUtf8(bytes, context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshString(result, span);
            CheckTextLength(result.Length, context, span);
            return result;
        }
        private static PyBytes AsciiBytes(PyString text, ExecutionContext context, LythonSourceSpan span)
            => OwnBytes(EncodeText(text, TextEncodingMode.Ascii, TextErrorMode.Strict, TextNewlineMode.PreserveUniversal, context, span), context, span);

        private static void ValidateNetloc(PyString netloc, ExecutionContext context, LythonSourceSpan span)
        {
            // Complete Unicode 15.1 NFKC delimiter hazards (19 scalars). They
            // avoid allocating an unbounded normalized copy of the authority.
            var source = netloc.Utf8Bytes.Span;
            for (var i = 0; i < source.Length;)
            {
                if ((i & 1023) == 0) context.CheckExecutionBudget(span);
                _ = Rune.DecodeFromUtf8(source[i..], out var rune, out var consumed);
                if (rune.Value is 0x2047 or 0x2048 or 0x2049 or 0x2100 or 0x2101 or 0x2105 or 0x2106 or 0x2A74 or 0xFE13 or 0xFE16 or 0xFE55 or 0xFE56 or 0xFE5F or 0xFE6B or 0xFF03 or 0xFF0F or 0xFF1A or 0xFF1F or 0xFF20)
                    throw new LythonRuntimeException("ValueError", "netloc contains invalid characters under NFKC normalization", span);
                i += consumed;
            }
        }

        private static void ValidateBrackets(PyString netloc, ExecutionContext context, LythonSourceSpan span)
        {
            var source = netloc.Utf8Bytes.Span;
            var open = FindByte(source, (byte)'[', 0, source.Length, context, span);
            var close = FindByte(source, (byte)']', 0, source.Length, context, span);
            if (open < 0 && close < 0) return;
            if (open < 0 || close < 0) throw BadBrackets(span);
            var hostStart = 0;
            for (var i = 0; i < source.Length; i++)
            { if ((i & 1023) == 0) context.CheckExecutionBudget(span); if (source[i] == '@') hostStart = i + 1; }
            open = FindByte(source, (byte)'[', hostStart, source.Length, context, span);
            ReadOnlySpan<byte> host;
            if (open >= 0)
            {
                if (open != hostStart) throw BadBrackets(span);
                close = FindByte(source, (byte)']', open + 1, source.Length, context, span);
                if (close < 0 || close + 1 < source.Length && source[close + 1] != ':') throw BadBrackets(span);
                host = source[(open + 1)..close];
            }
            else
            {
                var port = FindByte(source, (byte)':', hostStart, source.Length, context, span);
                host = source[hostStart..(port < 0 ? source.Length : port)];
            }
            if (host.Length > 0 && host[0] is (byte)'v' or (byte)'V')
            {
                var index = 1;
                while (index < host.Length && IsHex(host[index]))
                { if ((index & 1023) == 0) context.CheckExecutionBudget(span); index++; }
                if (index == 1 || index + 1 >= host.Length || host[index] != '.' || host[(index + 1)..].IndexOf((byte)'\n') >= 0) throw BadBrackets(span);
                return;
            }
            if (!IsIpv6(host, context, span)) throw BadBrackets(span);
        }
        private static LythonRuntimeException BadBrackets(LythonSourceSpan span) => new("ValueError", "Invalid IPv6 URL", span);

        private static bool IsIpv6(ReadOnlySpan<byte> host, ExecutionContext context, LythonSourceSpan span)
        {
            var percent = FindByte(host, (byte)'%', 0, host.Length, context, span);
            if (percent >= 0)
            {
                if (percent == host.Length - 1 || FindByte(host, (byte)'%', percent + 1, host.Length, context, span) >= 0) return false;
                host = host[..percent];
            }
            // At most eight hex groups and a terminal four-octet IPv4 tail.
            // Parse directly: platform IP parsers accept extra legacy forms.
            if (host.Length < 2 || host.Length > 45) return false;
            var compressed = false; var groups = 0; var pos = 0;
            if (host[0] == ':')
            { if (host[1] != ':') return false; compressed = true; pos = 2; }
            while (pos < host.Length)
            {
                var begin = pos;
                while (pos < host.Length && host[pos] != ':') pos++;
                var group = host[begin..pos];
                if (group.IndexOf((byte)'.') >= 0)
                {
                    if (pos != host.Length || !IsIpv4Tail(group)) return false;
                    groups += 2;
                }
                else
                {
                    if (group.Length is < 1 or > 4) return false;
                    foreach (var c in group) if (!IsHex(c)) return false;
                    groups++;
                }
                if (pos < host.Length)
                {
                    pos++;
                    if (pos == host.Length) return false;
                    if (host[pos] == ':')
                    { if (compressed) return false; compressed = true; pos++; }
                }
            }
            return compressed ? groups < 8 : groups == 8;
        }
        private static bool IsIpv4Tail(ReadOnlySpan<byte> source)
        {
            var pos = 0;
            for (var part = 0; part < 4; part++)
            {
                var start = pos; var number = 0;
                while (pos < source.Length && source[pos] != '.')
                {
                    if (source[pos] is < (byte)'0' or > (byte)'9') return false;
                    number = number * 10 + source[pos++] - '0';
                    if (number > 255 || pos - start > 3) return false;
                }
                if (pos == start || pos - start > 1 && source[start] == '0') return false;
                if (part < 3) { if (pos == source.Length) return false; pos++; }
            }
            return pos == source.Length;
        }

        private static PyString LowerHost(PyString host, ExecutionContext context, LythonSourceSpan span, bool preserveZone)
        {
            var source = host.Utf8Bytes.Span;
            var percent = preserveZone ? FindByte(source, (byte)'%', 0, source.Length, context, span) : -1;
            var end = percent < 0 ? source.Length : percent;
            // Reuse the string lower operation, including its Unicode rules;
            // scoped IPv6 identifiers keep their original spelling.
            var prefix = OwnedText(host.Utf8Bytes[..end], context, span);
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            var lower = PyStringOps.Lower(prefix);
            lower = (PyString)OwnMethodResult(lower, prefix, context.MemoryGovernor, span, context.State.CallTemporaries);
            if (!ReferenceEquals(lower, prefix)) context.State.CallTemporaries.TrackFreshString(lower, span);
            CheckTextLength(lower.Length, context, span);
            if (percent < 0) return lower;
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                builder.Append(lower); builder.Append(source[end..]);
                var result = builder.ToPyStringAndRelease();
                context.State.CallTemporaries.TrackFreshString(result, span);
                return result;
            }
            finally { builder.Release(); GC.KeepAlive(host); GC.KeepAlive(prefix); GC.KeepAlive(lower); }
        }

        private static async ValueTask<object> ReassembleUrlAsync(object input, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, bool parameters)
        {
            var values = await UrlComponentsAsync(input, span, context, asynchronous).ConfigureAwait(false);
            try { return await ReassembleValuesAsync(values, span, context, asynchronous, parameters).ConfigureAwait(false); }
            finally { GC.KeepAlive(values); }
        }
        private static async ValueTask<PyList> UrlComponentsAsync(object input, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var list = new PyList([], context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(list, list.CommittedStorageBytes);
            await foreach (var item in QuerySequenceAsync(input, span, context, asynchronous).ConfigureAwait(false))
            {
                if (list.Count == int.MaxValue) throw RuntimeErrors.Runtime("URL component sequence exceeds the supported size", span);
                context.ObserveCollectionCount(list.Count + 1, span); list.Add(item);
            }
            return list;
        }
        private static async ValueTask<object> ReassembleValuesAsync(IReadOnlyList<object> values, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, bool parameters)
        {
            if (values.Count == 0) throw new LythonRuntimeException("IndexError", "tuple index out of range", span);
            var textInput = values[0] is PyString;
            // Coercion precedes arity validation, including ASCII byte errors.
            for (var i = 1; i < values.Count; i++)
                if (await TruthAsync(values[i], span, context, asynchronous).ConfigureAwait(false) && (values[i] is PyString) != textInput)
                    throw RuntimeErrors.Type("Cannot mix str and non-str arguments", span);
            var decoded = new PyList([], context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(decoded, decoded.CommittedStorageBytes);
            foreach (var value in values)
                decoded.Add(textInput ? value : await UrlInputAsync(value, false, span, context, asynchronous).ConfigureAwait(false));
            if (decoded.Count != (parameters ? 6 : 5)) throw new LythonRuntimeException("ValueError", "wrong number of URL components", span);
            if (decoded.Any(value => value is not PyString))
                return await ReassembleProtocolValuesAsync(decoded, span, context, asynchronous, parameters).ConfigureAwait(false);
            var scheme = RequireUrlText(decoded[0], span);
            var netloc = RequireUrlText(decoded[1], span);
            var path = RequireUrlText(decoded[2], span);
            var param = parameters ? RequireUrlText(decoded[3], span) : PyString.Empty;
            var query = RequireUrlText(decoded[parameters ? 4 : 3], span);
            var fragment = RequireUrlText(decoded[parameters ? 5 : 4], span);
            var withParams = param.Length != 0;
            var hasPath = path.Length != 0 || withParams;
            var startsSlash = path.Length != 0 && path.Utf8Bytes.Span[0] == '/';
            var doubleSlash = path.Utf8Bytes.Length >= 2 && path.Utf8Bytes.Span[0] == '/' && path.Utf8Bytes.Span[1] == '/';
            var authorityPrefix = netloc.Length != 0 || doubleSlash || scheme.Length != 0 && UsesNetloc.Contains(scheme.AsString()) && (!hasPath || startsSlash);
            long scalarCount = scheme.Length + (long)netloc.Length + path.Length + param.Length + query.Length + fragment.Length;
            if (scheme.Length != 0) scalarCount++;
            if (authorityPrefix) scalarCount += 2;
            if (netloc.Length != 0 && hasPath && !startsSlash) scalarCount++;
            if (withParams) scalarCount++;
            if (query.Length != 0) scalarCount++;
            if (fragment.Length != 0) scalarCount++;
            CheckTextLength(scalarCount, context, span);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                Append(scheme); if (scheme.Length != 0) builder.Append((byte)':');
                if (authorityPrefix) builder.AppendAscii("//");
                Append(netloc);
                if (netloc.Length != 0 && hasPath && !startsSlash) builder.Append((byte)'/');
                Append(path); if (withParams) { builder.Append((byte)';'); Append(param); }
                if (query.Length != 0) { builder.Append((byte)'?'); Append(query); }
                if (fragment.Length != 0) { builder.Append((byte)'#'); Append(fragment); }
                var result = builder.ToPyStringAndRelease();
                context.State.CallTemporaries.TrackFreshString(result, span);
                return textInput ? result : AsciiBytes(result, context, span);
            }
            finally { builder.Release(); GC.KeepAlive(decoded); GC.KeepAlive(values); }
            void Append(PyString text)
            {
                var bytes = text.Utf8Bytes;
                for (var pos = 0; pos < bytes.Length; pos += Math.Min(1024, bytes.Length - pos))
                { context.CheckExecutionBudget(span); builder.Append(bytes.Span.Slice(pos, Math.Min(1024, bytes.Length - pos))); }
            }
        }

        private static async ValueTask<object> ReassembleProtocolValuesAsync(IReadOnlyList<object> values, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, bool parameters)
        {
            var scheme = values[0]; var netloc = values[1]; var path = values[2];
            var query = values[parameters ? 4 : 3]; var fragment = values[parameters ? 5 : 4];
            if (parameters && await TruthAsync(values[3], span, context, asynchronous).ConfigureAwait(false))
            {
                var left = await FormatSuspendedFieldAsync(path, 's', null, context, span, asynchronous).ConfigureAwait(false);
                var right = await FormatSuspendedFieldAsync(values[3], 's', null, context, span, asynchronous).ConfigureAwait(false);
                path = await Add(await Add(left, PyString.FromString(";")), right);
            }
            var doubleSlash = PyString.FromString("//");
            if (await TruthAsync(netloc, span, context, asynchronous).ConfigureAwait(false))
            {
                if (await TruthAsync(path, span, context, asynchronous).ConfigureAwait(false) && !await EqualsSlash(1, Slash)) path = await Add(Slash, path);
                path = await Add(await Add(doubleSlash, netloc), path);
            }
            else if (await EqualsSlash(2, doubleSlash)) path = await Add(doubleSlash, path);
            else if (await TruthAsync(scheme, span, context, asynchronous).ConfigureAwait(false) && scheme is PyString nativeScheme && UsesNetloc.Contains(nativeScheme.AsString()) &&
                (!await TruthAsync(path, span, context, asynchronous).ConfigureAwait(false) || await EqualsSlash(1, Slash))) path = await Add(doubleSlash, path);
            if (await TruthAsync(scheme, span, context, asynchronous).ConfigureAwait(false)) path = await Add(await Add(scheme, PyString.FromString(":")), path);
            if (await TruthAsync(query, span, context, asynchronous).ConfigureAwait(false)) path = await Add(await Add(path, PyString.FromString("?")), query);
            if (await TruthAsync(fragment, span, context, asynchronous).ConfigureAwait(false)) path = await Add(await Add(path, PyString.FromString("#")), fragment);
            return path;

            ValueTask<object> Add(object left, object right) => asynchronous
                ? EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, left, right, context, span)
                : ValueTask.FromResult(EvaluateBinaryOperator(BinaryOperatorSyntax.Add, left, right, context, span));
            async ValueTask<bool> EqualsSlash(int count, PyString expected)
            {
                var slice = new PySlice(BigInteger.Zero, new BigInteger(count), PyNone.Instance);
                object part;
                if (path is PyInstance)
                {
                    var member = asynchronous ? await PyTextStream.ResolveMemberAsync(path, "__getitem__", span, context).ConfigureAwait(false)
                        : PyTextStream.ResolveMember(path, "__getitem__", span, context);
                    part = await CallAsync(member, [CallArgumentValue.Positional(slice)], span, context, asynchronous).ConfigureAwait(false);
                }
                else part = ReadSubscriptValue(path, slice, span, context);
                return asynchronous ? await MembershipEqualsAsync(part, expected, context, span).ConfigureAwait(false) : MembershipEquals(part, expected, context, span);
            }
        }
        private static PyString RequireUrlText(object value, LythonSourceSpan span)
            => value as PyString ?? throw RuntimeErrors.Type("URL component must be a string", span);
    }
}
