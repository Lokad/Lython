using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class ReMatchMembers
    {
        internal static PyString ExpandReplacementTemplate(ReMatchObject match, PyString template,
            ExecutionContext context, LythonSourceSpan span)
        {
            var source = template.Utf8Bytes.Span;
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
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
                        AppendReplacementGroup(match, ResolveReplacementGroup(match, name, template, nameStart, context, span), builder);
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
                        if (index >= match.CaptureSlotCount)
                            throw ReplacementError($"invalid group reference {index}", template, escapeStart + 1, context, span);
                        AppendReplacementGroup(match, index, builder);
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
                var result = builder.ToPyString();
                context.CheckExecution(span);
                return result;
            }
            finally { builder.Release(); }
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

        private static int ResolveReplacementGroup(ReMatchObject match, ReadOnlySpan<byte> name,
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
                if (index < match.CaptureSlotCount) return index;
                // Only diagnostics cross into CLR text. Normal names and values
                // remain native, including supplementary/combining characters.
                var reference = Encoding.UTF8.GetString(name).TrimStart('0');
                throw ReplacementError($"invalid group reference {reference}", template, nameStart, context, span);
            }

            var key = PyString.CopyFromUtf8(name, context.MemoryGovernor, span);
            if (!PyStringOps.IsIdentifier(key, context, span))
                throw ReplacementError($"bad character in group name {PyRendering.ToReprPyString(key, new(context)).AsString()}",
                    template, nameStart, context, span);
            if (match.NamedGroups.TryGetValue(key, out index)) return index;
            throw new LythonRuntimeException("IndexError", $"unknown group name '{key.AsString()}'", span);
        }

        private static void AppendReplacementGroup(ReMatchObject match, int index, GovernedByteBuilder builder)
        {
            if (index == 0) builder.Append(match.Value);
            else if (match.Captures[index - 1] is { } group) builder.Append(group.Value);
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
