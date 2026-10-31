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

    internal readonly record struct RegexReplacementPiece(int Group, int Start, int Length);

    internal sealed class RegexReplacementPlan(byte[] literals, RegexReplacementPiece[] parts, int count,
        MemoryGovernor.TemporaryMemoryReservation storage) : IDisposable
    {
        private byte[] _literals = literals;
        private RegexReplacementPiece[] _parts = parts;
        private MemoryGovernor.TemporaryMemoryReservation? _storage = storage;

        internal void AppendTo(GovernedByteBuilder builder, ReMatchObject match, ExecutionContext context, LythonSourceSpan span)
        {
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                var part = _parts[i];
                if (part.Group < 0) AppendRegexBytes(builder, _literals.AsSpan(part.Start, part.Length), context, span);
                else if (part.Group == 0) AppendRegexBytes(builder, match.Value.Utf8Bytes.Span, context, span);
                else if (match.Captures[part.Group - 1] is { } capture)
                    AppendRegexBytes(builder, capture.Value.Utf8Bytes.Span, context, span);
            }
        }

        internal void AppendTo(GovernedByteBuilder builder, RegexSubjectRange range, LythonRegexMatch match,
            ExecutionContext context, LythonSourceSpan span)
        {
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                var part = _parts[i];
                if (part.Group < 0) AppendRegexBytes(builder, _literals.AsSpan(part.Start, part.Length), context, span);
                else if (match.Captures[part.Group] is { } capture)
                    AppendRegexBytes(builder, range.Original.Utf8Bytes.Span.Slice(capture.StartByte,
                        capture.EndByte - capture.StartByte), context, span);
            }
        }

        public void Dispose()
        {
            _literals = [];
            _parts = [];
            Interlocked.Exchange(ref _storage, null)?.Dispose();
        }
    }

    internal static partial class ReMatchMembers
    {
        internal static PyString ExpandReplacementTemplate(ReMatchObject match, PyString template,
            ExecutionContext context, LythonSourceSpan span)
        {
            using var plan = CompileReplacementTemplate(match.Pattern, template, context, span);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                plan.AppendTo(builder, match, context, span);
                var result = builder.ToPyString();
                context.CheckExecution(span);
                return result;
            }
            finally { builder.Release(); }
        }

        internal static RegexReplacementPlan CompileReplacementTemplate(RePatternObject pattern, PyString template,
            ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var source = template.Utf8Bytes.Span;
            var slashes = 0;
            for (var offset = 0; offset < source.Length;)
            {
                context.CheckExecution(span);
                var chunk = source.Slice(offset, Math.Min(1024, source.Length - offset));
                slashes += chunk.Count((byte)'\\');
                offset += chunk.Length;
            }
            var capacityLong = 1L + 2L * slashes;
            if (capacityLong > int.MaxValue) throw RuntimeErrors.Runtime("replacement template program is too large.", span);
            var capacity = (int)capacityLong;
            // Fund the private program, literal capacity, transient name keys and
            // construction overlap before allocation. No template storage escapes
            // the operation; the result obtains its independent admission.
            MemoryGovernor.TemporaryMemoryReservation? storage = context.MemoryGovernor.ReserveTemporary(
                checked(256L + 4L * source.Length + 32L * capacity), span);
            GovernedByteBuilder? builder = null;
            try
            {
                context.CheckExecution(span);
                builder = new GovernedByteBuilder(source.Length); // capacity is prefunded above
                var parts = new RegexReplacementPiece[capacity];
                var count = 0;
                var literalStart = 0;
                void FlushLiteral()
                {
                    if (builder.Length == literalStart) return;
                    parts[count++] = new(-1, literalStart, builder.Length - literalStart);
                    literalStart = builder.Length;
                }
                void AddGroup(int index)
                {
                    FlushLiteral();
                    parts[count++] = new(index, 0, 0);
                }
                for (var offset = 0; offset < source.Length;)
                {
                    context.CheckExecution(span);
                    var chunk = source.Slice(offset, Math.Min(1024, source.Length - offset));
                    var slash = chunk.IndexOf((byte)'\\');
                    if (slash < 0)
                    {
                        builder.Append(chunk);
                        offset += chunk.Length;
                        continue;
                    }

                    builder.Append(chunk[..slash]);
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
                        while (offset < source.Length && source[offset] != '>')
                        {
                            if ((offset & 1023) == 0) context.CheckExecution(span);
                            if (source[offset++] == '\\')
                            {
                                if (offset == source.Length)
                                    throw ReplacementError("bad escape (end of pattern)", template, offset - 1, context, span);
                                // A quoted > belongs to the name, not its terminator.
                                offset++;
                                while (offset < source.Length && (source[offset] & 0xc0) == 0x80) offset++;
                            }
                        }
                        if (offset == source.Length)
                            throw ReplacementError(offset == nameStart ? "missing group name" : "missing >, unterminated name",
                                template, nameStart, context, span);
                        CheckReplacementLookahead(template, offset + 1, context, span);
                        if (offset == nameStart)
                            throw ReplacementError("missing group name", template, nameStart, context, span);
                        var name = source[nameStart..offset++];
                        AddGroup(ResolveReplacementGroup(pattern, name, template, nameStart, context, span));
                    }
                    else if (escaped == '0')
                    {
                        var value = 0;
                        for (var digits = 0; digits < 2 && offset < source.Length && IsOctal(source[offset]); digits++)
                        {
                            value = value * 8 + source[offset++] - '0';
                            CheckReplacementLookahead(template, offset, context, span);
                        }
                        builder.Append(new Rune(value));
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
                                builder.Append(new Rune(value));
                                continue;
                            }
                        }
                        if (index >= pattern.CaptureSlotCount)
                            throw ReplacementError($"invalid group reference {index}", template, escapeStart + 1, context, span);
                        AddGroup(index);
                    }
                    else
                    {
                        var literal = escaped switch
                        {
                            (byte)'a' => 7, (byte)'b' => 8, (byte)'f' => 12,
                            (byte)'n' => 10, (byte)'r' => 13, (byte)'t' => 9,
                            (byte)'v' => 11, (byte)'\\' => '\\', _ => -1,
                        };
                        if (literal >= 0) builder.Append((byte)literal);
                        else if (escaped is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z')
                            throw ReplacementError($"bad escape \\{(char)escaped}", template, escapeStart, context, span);
                        else
                        {
                            // Unknown punctuation/non-ASCII escapes keep the slash.
                            // Leave a non-ASCII scalar intact for the next chunk.
                            builder.Append((byte)'\\');
                            if (escaped < 128) builder.Append(escaped);
                            else offset--;
                        }
                    }
                }

                context.CheckExecution(span);
                FlushLiteral();
                var plan = new RegexReplacementPlan(builder.ToArrayAndRelease(), parts, count, storage);
                storage = null; // the operation owns this private reservation
                return plan;
            }
            finally { builder?.Release(); storage?.Dispose(); }
        }

        private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';
        private static bool IsOctal(byte value) => value is >= (byte)'0' and <= (byte)'7';

        private static void CheckReplacementLookahead(PyString template, int offset, ExecutionContext context, LythonSourceSpan span)
        {
            // Python reads the following token before interpreting the current
            // one: a dangling slash there takes precedence over its group error.
            var source = template.Utf8Bytes.Span;
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
                var reference = Encoding.UTF8.GetString(name).TrimStart('0');
                throw ReplacementError($"invalid group reference {reference}", template, nameStart, context, span);
            }

            // The template transaction funds this transient lookup key.
            var key = PyString.FromUtf8(name.ToArray());
            if (!PyStringOps.IsIdentifier(key, context, span))
                throw ReplacementError($"bad character in group name {PyRendering.ToReprPyString(key, new(context)).AsString()}",
                    template, nameStart, context, span);
            if (pattern.NamedGroups.TryGetValue(key, out index)) return index;
            throw new LythonRuntimeException("IndexError", $"unknown group name '{key.AsString()}'", span);
        }

        private static LythonRuntimeException ReplacementError(string message, PyString template, int byteOffset,
            ExecutionContext context, LythonSourceSpan span)
        {
            var source = template.Utf8Bytes.Span;
            var position = 0;
            for (var i = 0; i < byteOffset; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if ((source[i] & 0xc0) != 0x80) position++;
            }
            return new LythonRuntimeException(ModuleException("re", "PatternError"), $"{message} at position {position}", span);
        }
    }
}
