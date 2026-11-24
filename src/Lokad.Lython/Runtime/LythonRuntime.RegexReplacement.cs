using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void AppendRegexBytes(GovernedByteBuilder builder, ReadOnlySpan<byte> value,
        ExecutionContext context, LythonSourceSpan span)
    {
        for (var offset = 0; offset < value.Length;)
        {
            context.CheckExecution(span);
            var count = Math.Min(4096, value.Length - offset);
            builder.Append(value.Slice(offset, count));
            offset += count;
        }
        context.CheckExecution(span);
    }

    internal readonly record struct RegexReplacementPiece(int Group, int Start, int Length)
    {
        internal const int LiteralRange = -1;
        internal const int EscapedRange = -2;
    }

    internal sealed class RegexReplacementPlan(RegexReplacementPiece[] parts, byte[] escaped, int count,
        MemoryGovernor.TemporaryMemoryReservation storage) : IDisposable
    {
        private RegexReplacementPiece[] _parts = parts;
        private byte[] _escaped = escaped;
        private MemoryGovernor.TemporaryMemoryReservation? _storage = storage;

        internal PyString Expand(PyString template, ReMatchObject match, ExecutionContext context, LythonSourceSpan span)
        {
            long length = 0;
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                length = checked(length + ExpansionBytes(template, match, _parts[i]).Length);
            }
            context.CheckExecution(span);
            if (length > Array.MaxLength) throw RuntimeErrors.Runtime("byte buffer allocation is too large.", span);
            if (length == 0) return PyString.Empty;

            var governor = context.MemoryGovernor;
            var bytes = PyString.EstimateApproximateBytes((int)length);
            governor.Reserve(bytes, span);
            try
            {
                context.CheckExecution(span);
                var result = PyString.CreateReservedUtf8((int)length, governor);
                var destination = result.UnpublishedUtf8;
                for (var i = 0; i < count; i++)
                {
                    context.CheckExecution(span);
                    var piece = ExpansionBytes(template, match, _parts[i]);
                    PyStringOps.CopyJoinBytes(piece, destination, context, span);
                    destination = destination[piece.Length..];
                }
                context.CheckExecution(span);
                result.CompleteUnpublishedUtf8((int)length);
                governor.Commit(bytes);
                return result;
            }
            catch { governor.ReleaseReserved(bytes); throw; }
        }

        private ReadOnlySpan<byte> ExpansionBytes(PyString template, ReMatchObject match, RegexReplacementPiece part)
            => part.Group < 0 ? LiteralBytes(template, part) : match.GroupBytes(part.Group);

        internal void AppendTo(GovernedByteBuilder builder, PyString template, ReadOnlySpan<byte> original,
            ReadOnlySpan<LythonRegexCapture?> captures, ExecutionContext context, LythonSourceSpan span)
        {
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                var part = _parts[i];
                if (part.Group < 0) AppendRegexBytes(builder, LiteralBytes(template, part), context, span);
                else if (captures[part.Group] is { } capture)
                    AppendRegexBytes(builder, original.Slice(capture.StartByte,
                        capture.EndByte - capture.StartByte), context, span);
            }
        }

        private ReadOnlySpan<byte> LiteralBytes(PyString template, RegexReplacementPiece part)
        {
            var source = part.Group == RegexReplacementPiece.LiteralRange ? template.Utf8Span : _escaped.AsSpan();
            return source.Slice(part.Start, part.Length);
        }

        public void Dispose()
        {
            _parts = [];
            _escaped = [];
            Interlocked.Exchange(ref _storage, null)?.Dispose();
        }
    }

    internal static partial class ReMatchMembers
    {
        internal static PyString ExpandReplacementTemplate(ReMatchObject match, PyString template,
            ExecutionContext context, LythonSourceSpan span)
        {
            using var plan = CompileReplacementTemplate(match.Pattern, template, context, span);
            return plan is null ? PyString.CopyFromUtf8(template.Utf8Span, context.MemoryGovernor, span, context)
                : plan.Expand(template, match, context, span);
        }

        // Unescaped text is already in its final UTF-8 representation.
        internal static RegexReplacementPlan? CompileReplacementTemplate(RePatternObject pattern, PyString template,
            ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var source = template.Utf8Span;
            var slashes = 0;
            for (var offset = 0; offset < source.Length;)
            {
                context.CheckExecution(span);
                var chunk = source.Slice(offset, Math.Min(1024, source.Length - offset));
                slashes += chunk.Count((byte)'\\');
                offset += chunk.Length;
            }
            if (slashes == 0) return null;
            var capacityLong = 1L + 2L * slashes;
            if (capacityLong > int.MaxValue) throw RuntimeErrors.Runtime("replacement template program is too large.", span);
            var capacity = (int)capacityLong;
            // The private plan borrows ranges only during replay. The caller
            // keeps the immutable template funded through that use.
            MemoryGovernor.TemporaryMemoryReservation? storage = context.MemoryGovernor.ReserveTemporary(
                checked(256L + 32L * capacity), span);
            try
            {
                context.CheckExecution(span);
                var parts = new RegexReplacementPiece[capacity];
                var count = 0;
                var literalStart = 0;
                byte[]? escapedBytes = null;
                var escapeLength = 0;
                var escapeRunStart = 0;
                void FlushLiterals(int end)
                {
                    if (escapeLength != escapeRunStart)
                    {
                        parts[count++] = new(RegexReplacementPiece.EscapedRange, escapeRunStart, escapeLength - escapeRunStart);
                        escapeRunStart = escapeLength;
                    }
                    if (end != literalStart)
                        parts[count++] = new(RegexReplacementPiece.LiteralRange, literalStart, end - literalStart);
                }
                void AddGroup(int group, int escapeStart, int escapeEnd)
                {
                    FlushLiterals(escapeStart);
                    parts[count++] = new(group, 0, 0);
                    literalStart = escapeEnd;
                }
                void AppendEscape(int value, int escapeStart, int escapeEnd)
                {
                    if (escapeStart != literalStart) FlushLiterals(escapeStart);
                    if (escapedBytes is null)
                    {
                        // Recognized escapes emit at most two UTF-8 bytes
                        // and consume at least two original bytes.
                        var capacity = (int)Math.Min(template.ByteLength, 2L * slashes);
                        storage!.Grow(32L + capacity, span);
                        context.CheckExecution(span);
                        escapedBytes = new byte[capacity];
                    }
                    escapeLength += new Rune(value).EncodeToUtf8(escapedBytes.AsSpan(escapeLength));
                    literalStart = escapeEnd;
                }
                for (var offset = 0; offset < source.Length;)
                {
                    context.CheckExecution(span);
                    var chunk = source.Slice(offset, Math.Min(1024, source.Length - offset));
                    var slash = chunk.IndexOf((byte)'\\');
                    if (slash < 0)
                    {
                        offset += chunk.Length;
                        continue;
                    }

                    offset += slash;
                    var escapeStart = offset++;
                    if (offset == source.Length)
                        throw ReplacementError("bad escape (end of pattern)", template, escapeStart, context, span);
                    var escaped = source[offset++];
                    CheckReplacementLookahead(template, offset, context, span);
                    if (escaped == 'g')
                    {
                        if (offset == source.Length || source[offset] != '<')
                            throw ReplacementError("missing <", template, offset, context, span);
                        var nameStart = ++offset;
                        while (offset < source.Length)
                        {
                            context.CheckExecution(span);
                            var nameChunk = source.Slice(offset, Math.Min(1024, source.Length - offset));
                            var special = nameChunk.IndexOfAny((byte)'>', (byte)'\\');
                            if (special < 0)
                            {
                                offset += nameChunk.Length;
                                continue;
                            }
                            offset += special;
                            if (source[offset] == '>') break;
                            if (++offset == source.Length)
                                throw ReplacementError("bad escape (end of pattern)", template, offset - 1, context, span);
                            // A quoted > belongs to the name, not its terminator.
                            offset++;
                            while (offset < source.Length && (source[offset] & 0xc0) == 0x80) offset++;
                        }
                        if (offset == source.Length)
                            throw ReplacementError(offset == nameStart ? "missing group name" : "missing >, unterminated name",
                                template, nameStart, context, span);
                        CheckReplacementLookahead(template, offset + 1, context, span);
                        if (offset == nameStart)
                            throw ReplacementError("missing group name", template, nameStart, context, span);
                        var name = source[nameStart..offset++];
                        var group = ResolveReplacementGroup(pattern, name, template, nameStart, context, span);
                        AddGroup(group, escapeStart, offset);
                    }
                    else if (escaped == '0')
                    {
                        var value = 0;
                        for (var digits = 0; digits < 2 && offset < source.Length && IsOctal(source[offset]); digits++)
                        {
                            value = value * 8 + source[offset++] - '0';
                            CheckReplacementLookahead(template, offset, context, span);
                        }
                        AppendEscape(value, escapeStart, offset);
                    }
                    else if (escaped is >= (byte)'1' and <= (byte)'9')
                    {
                        var index = escaped - '0';
                        if (offset < source.Length && IsDigit(source[offset]))
                        {
                            var second = source[offset++];
                            CheckReplacementLookahead(template, offset, context, span);
                            index = index * 10 + second - '0';
                            if (IsOctal(escaped) && IsOctal(second) && offset < source.Length && IsOctal(source[offset]))
                            {
                                var value = (escaped - '0') * 64 + (second - '0') * 8 + source[offset++] - '0';
                                CheckReplacementLookahead(template, offset, context, span);
                                if (value > 255)
                                    throw ReplacementError($"octal escape value {Encoding.UTF8.GetString(source[escapeStart..offset])} outside of range 0-0o377",
                                        template, escapeStart, context, span);
                                AppendEscape(value, escapeStart, offset);
                                continue;
                            }
                        }
                        if (index >= pattern.CaptureSlotCount)
                            throw ReplacementError($"invalid group reference {index}", template, escapeStart + 1, context, span);
                        AddGroup(index, escapeStart, offset);
                    }
                    else
                    {
                        var literal = escaped switch
                        {
                            (byte)'a' => 7, (byte)'b' => 8, (byte)'f' => 12,
                            (byte)'n' => 10, (byte)'r' => 13, (byte)'t' => 9,
                            (byte)'v' => 11, (byte)'\\' => '\\', _ => -1,
                        };
                        if (literal >= 0)
                            AppendEscape(literal, escapeStart, offset);
                        else if (escaped is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z')
                            throw ReplacementError($"bad escape \\{(char)escaped}", template, escapeStart, context, span);
                        // Unknown punctuation/non-ASCII escapes remain in the literal range.
                    }
                }

                context.CheckExecution(span);
                FlushLiterals(source.Length);
                var plan = new RegexReplacementPlan(parts, escapedBytes ?? [], count, storage);
                storage = null; // the operation owns this private reservation
                return plan;
            }
            finally { storage?.Dispose(); }
        }

        private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';
        private static bool IsOctal(byte value) => value is >= (byte)'0' and <= (byte)'7';

        private static void CheckReplacementLookahead(PyString template, int offset, ExecutionContext context, LythonSourceSpan span)
        {
            // Python reads the following token before interpreting the current
            // one: a dangling slash there takes precedence over its group error.
            var source = template.Utf8Span;
            if (offset == source.Length - 1 && source[offset] == '\\')
                throw ReplacementError("bad escape (end of pattern)", template, offset, context, span);
        }

        private static int ResolveReplacementGroup(RePatternObject pattern, ReadOnlySpan<byte> name,
            PyString template, int nameStart, ExecutionContext context, LythonSourceSpan span)
        {
            var numeric = true;
            var index = 0;
            foreach (var digit in name)
            {
                context.CheckExecution(span);
                if (!IsDigit(digit)) { numeric = false; break; }
                index = (int)Math.Min(int.MaxValue, (long)index * 10 + digit - '0');
            }
            if (numeric)
            {
                if (index < pattern.CaptureSlotCount) return index;
                // Only diagnostics cross into CLR text. Normal names and values
                // remain native, including supplementary/combining characters.
                using var scratch = ReserveReplacementNameDiagnostic(name.Length, context, span);
                context.CheckExecution(span);
                var reference = Encoding.UTF8.GetString(name).TrimStart('0');
                throw ReplacementError($"invalid group reference {reference}", template, nameStart, context, span);
            }

            var valid = PyStringOps.IsIdentifier(name, context, span);
            if (valid && pattern.NamedGroups.TryReadNativeStringInteger(name, context, span, out index)) return index;

            using var diagnostic = ReserveReplacementNameDiagnostic(name.Length, context, span);
            context.CheckExecution(span);
            if (!valid)
            {
                var key = PyString.CopyFromUtf8(name, null, span, context);
                throw ReplacementError($"bad character in group name {PyRendering.ToReprPyString(key, new(context)).AsString()}",
                    template, nameStart, context, span);
            }
            throw new LythonRuntimeException("IndexError", $"unknown group name '{Encoding.UTF8.GetString(name)}'", span);
        }

        private static MemoryGovernor.TemporaryMemoryReservation ReserveReplacementNameDiagnostic(
            int length, ExecutionContext context, LythonSourceSpan span)
            => context.MemoryGovernor.ReserveTemporary(checked(192L + 4L * length), span);

        private static LythonRuntimeException ReplacementError(string message, PyString template, int byteOffset,
            ExecutionContext context, LythonSourceSpan span)
        {
            var source = template.Utf8Span;
            var position = 0;
            for (var i = 0; i < byteOffset; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if ((source[i] & 0xc0) != 0x80) position++;
            }
            return CreatePatternErrorFailure(message, template, position, context.MemoryGovernor,
                context.State.CancellationToken, span);
        }
    }
}
