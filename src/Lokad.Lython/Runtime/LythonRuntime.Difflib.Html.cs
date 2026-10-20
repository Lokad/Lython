using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class DifflibHtmlDiffObject
    {
        private readonly int _tabsize;
        private readonly ICallable? _linejunk;

        public DifflibHtmlDiffObject(int tabsize, ICallable? linejunk)
        {
            _tabsize = tabsize;
            _linejunk = linejunk;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "make_table" => BoundCallable.Create(MakeTable, LythonCallableSignature.Create("HtmlDiff.make_table", ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines"], requiredCount: 2)),
                "make_file" => BoundCallable.Create(MakeFile, LythonCallableSignature.Create("HtmlDiff.make_file", ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines", "charset"], requiredCount: 2)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object MakeTable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Render(arguments, false, span, context);

        private object MakeFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Render(arguments, true, span, context);

        private object Render(object[] arguments, bool file, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseHtmlArguments(arguments, file, span);
            var fromLines = DifflibModule.RequireStringSequence(arguments[0], "HtmlDiff fromlines", span, context);
            var toLines = DifflibModule.RequireStringSequence(arguments[1], "HtmlDiff tolines", span, context);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                if (file)
                {
                    builder.AppendAscii("<!DOCTYPE html>\n<html><head><meta charset=\"");
                    AppendEscaped(builder, options.Charset, false, context, span);
                    builder.AppendAscii("\">\n<style>.diff{font-family:Consolas,monospace;border-collapse:collapse}.diff td,.diff th{padding:2px 6px;border:1px solid #ddd}.diff_add{background:#e6ffed}.diff_sub{background:#ffeef0}.diff_chg{background:#fff5b1}.diff_header{background:#f6f8fa}</style>\n</head><body>\n");
                }
                BuildTable(builder, fromLines, toLines, options, context, span);
                if (file) builder.AppendAscii("\n</body></html>\n");
                context.CheckExecution(span);
                return builder.ToPyString();
            }
            finally { builder.Release(); }
        }

        private HtmlOptions ParseHtmlArguments(object[] arguments, bool includeCharset, LythonSourceSpan span)
        {
            var maximum = includeCharset ? 7 : 6;
            if (arguments.Length is < 2 || arguments.Length > maximum)
            {
                var owner = includeCharset ? "HtmlDiff.make_file" : "HtmlDiff.make_table";
                throw new LythonRuntimeException("TypeError", $"{owner}(fromlines, tolines[, fromdesc][, todesc][, context][, numlines]) received an unsupported argument count.", span);
            }

            return new HtmlOptions(
                ParseHtmlString(arguments, 2, PyString.Empty, "fromdesc", span),
                ParseHtmlString(arguments, 3, PyString.Empty, "todesc", span),
                arguments.Length >= 5 && arguments[4] is not PyNone && IsTruthy(arguments[4]),
                arguments.Length >= 6 && arguments[5] is not PyNone ? DifflibModule.RequireInt32(arguments[5], "HtmlDiff numlines expects an integer.", span) : 5,
                includeCharset ? ParseHtmlString(arguments, 6, Utf8Charset, "charset", span) : Utf8Charset);
        }

        private static PyString ParseHtmlString(object[] arguments, int index, PyString defaultValue, string name, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"HtmlDiff {name} expects a string.", span);
            }

            return text;
        }

        private void BuildTable(GovernedByteBuilder builder, IReadOnlyList<PyString> fromLines, IReadOnlyList<PyString> toLines, HtmlOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            using var lineStorage = context.MemoryGovernor.ReserveTemporary(64L + 16L * ((long)fromLines.Count + toLines.Count), span);
            var a = fromLines.Select(line => PrepareHtmlLine(line, context, span)).ToArray();
            var b = toLines.Select(line => PrepareHtmlLine(line, context, span)).ToArray();
            var matcher = new DifflibSequenceMatcherObject(_linejunk, new PyList(a.Cast<object>(), context.MemoryGovernor, span), new PyList(b.Cast<object>(), context.MemoryGovernor, span), autojunk: true, span, context);
            var groups = options.Context
                ? matcher.BuildGroupedOpcodes(options.NumLines, span, context).ToArray()
                : [matcher.BuildOpcodes(span, context)];

            builder.AppendAscii("<table class=\"diff\" summary=\"Differences\">\n");
            if (!options.FromDescription.Utf8Bytes.IsEmpty || !options.ToDescription.Utf8Bytes.IsEmpty)
            {
                builder.AppendAscii("<thead><tr><th class=\"diff_next\"></th><th colspan=\"2\" class=\"diff_header\">");
                AppendEscaped(builder, options.FromDescription, false, context, span);
                builder.AppendAscii("</th><th class=\"diff_next\"></th><th colspan=\"2\" class=\"diff_header\">");
                AppendEscaped(builder, options.ToDescription, false, context, span);
                builder.AppendAscii("</th></tr></thead>\n");
            }

            builder.AppendAscii("<tbody>\n");
            var firstGroup = true;
            foreach (var group in groups)
            {
                if (!firstGroup)
                {
                    builder.AppendAscii("<tr><td colspan=\"6\" class=\"diff_next\"></td></tr>\n");
                }

                firstGroup = false;
                foreach (var opcode in group)
                {
                    context.CheckExecution(span);
                    AppendHtmlOpcode(builder, opcode, a, b, context, span);
                }
            }

            builder.AppendAscii("</tbody>\n</table>");
        }

        private PyString PrepareHtmlLine(PyString line, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var bytes = line.Utf8Bytes.Span;
            var length = bytes.Length;
            while (length > 0 && bytes[length - 1] is (byte)'\r' or (byte)'\n') length--;
            bytes = bytes[..length];
            if (_tabsize <= 0 || bytes.IndexOf((byte)'\t') < 0)
                return length == line.Utf8Bytes.Length ? line : PyString.CopyFromUtf8(bytes, context.MemoryGovernor, span);

            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                long column = 0;
                while (!bytes.IsEmpty)
                {
                    context.CheckExecution(span);
                    Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
                    if (rune.Value == '\t')
                    {
                        var spaces = (int)(_tabsize - column % _tabsize);
                        column += spaces;
                        while (spaces > 0)
                        {
                            context.CheckExecution(span);
                            var count = Math.Min(spaces, 4096);
                            builder.AppendRepeated((byte)' ', count);
                            spaces -= count;
                        }
                    }
                    else
                    {
                        builder.Append(bytes[..consumed]);
                        column += rune.Utf16SequenceLength;
                    }
                    bytes = bytes[consumed..];
                }
                return builder.ToPyString();
            }
            finally { builder.Release(); }
        }

        private static void AppendHtmlOpcode(GovernedByteBuilder builder, DiffOpcode opcode, IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, ExecutionContext context, LythonSourceSpan span)
        {
            switch (opcode.Tag)
            {
                case DiffTag.Equal:
                    for (var offset = 0; offset < opcode.I2 - opcode.I1; offset++)
                    {
                        AppendHtmlRow(builder, opcode.I1 + offset + 1, a[opcode.I1 + offset], string.Empty, opcode.J1 + offset + 1, b[opcode.J1 + offset], string.Empty, context, span);
                    }

                    break;
                case DiffTag.Delete:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        AppendHtmlRow(builder, i + 1, a[i], "diff_sub", null, PyString.Empty, string.Empty, context, span);
                    }

                    break;
                case DiffTag.Insert:
                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        AppendHtmlRow(builder, null, PyString.Empty, string.Empty, j + 1, b[j], "diff_add", context, span);
                    }

                    break;
                case DiffTag.Replace:
                    var leftCount = opcode.I2 - opcode.I1;
                    var rightCount = opcode.J2 - opcode.J1;
                    var paired = Math.Max(leftCount, rightCount);
                    for (var offset = 0; offset < paired; offset++)
                    {
                        var hasLeft = offset < leftCount;
                        var hasRight = offset < rightCount;
                        AppendHtmlRow(
                            builder,
                            hasLeft ? opcode.I1 + offset + 1 : null,
                            hasLeft ? a[opcode.I1 + offset] : PyString.Empty,
                            hasLeft ? "diff_chg" : string.Empty,
                            hasRight ? opcode.J1 + offset + 1 : null,
                            hasRight ? b[opcode.J1 + offset] : PyString.Empty,
                            hasRight ? "diff_chg" : string.Empty, context, span);
                    }

                    break;
            }
        }

        private static void AppendHtmlRow(GovernedByteBuilder builder, int? leftNumber, PyString leftText, string leftClass, int? rightNumber, PyString rightText, string rightClass, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            builder.AppendAscii("<tr><td class=\"diff_next\"></td><td class=\"diff_header\">");
            builder.AppendAscii(leftNumber?.ToString() ?? string.Empty);
            builder.AppendAscii("</td><td nowrap=\"nowrap\"");
            AppendClass(builder, leftClass);
            builder.AppendAscii(">");
            AppendEscaped(builder, leftText, true, context, span);
            builder.AppendAscii("</td><td class=\"diff_next\"></td><td class=\"diff_header\">");
            builder.AppendAscii(rightNumber?.ToString() ?? string.Empty);
            builder.AppendAscii("</td><td nowrap=\"nowrap\"");
            AppendClass(builder, rightClass);
            builder.AppendAscii(">");
            AppendEscaped(builder, rightText, true, context, span);
            builder.AppendAscii("</td></tr>\n");
        }

        private static void AppendClass(GovernedByteBuilder builder, string className)
        {
            if (className.Length != 0)
            {
                builder.AppendAscii(" class=\"");
                builder.AppendAscii(className);
                builder.AppendAscii("\"");
            }
        }

        private static void AppendEscaped(GovernedByteBuilder builder, PyString text, bool cell,
            ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = text.Utf8Bytes.Span;
            Span<byte> digits = stackalloc byte[16];
            while (!bytes.IsEmpty)
            {
                context.CheckExecution(span);
                Rune.DecodeFromUtf8(bytes, out var rune, out var consumed);
                var entity = rune.Value switch
                {
                    '<' => "&lt;", '>' => "&gt;", '&' => "&amp;",
                    '\"' => "&quot;", '\'' => "&#39;",
                    ' ' or '\t' when cell => "&nbsp;", _ => null
                };
                if (entity is not null) builder.AppendAscii(entity);
                else if (rune.Value is >= 160 and <= 255 or > 0xffff)
                {
                    // Preserve WebUtility.HtmlEncode's numeric-entity spelling.
                    System.Buffers.Text.Utf8Formatter.TryFormat(rune.Value, digits, out var written);
                    builder.Append("&#"u8);
                    builder.Append(digits[..written]);
                    builder.Append((byte)';');
                }
                else builder.Append(bytes[..consumed]);
                bytes = bytes[consumed..];
            }
        }

        private static readonly PyString Utf8Charset = PyString.FromString("utf-8");

        private readonly record struct HtmlOptions(
            PyString FromDescription,
            PyString ToDescription,
            bool Context,
            int NumLines,
            PyString Charset);
    }

}
