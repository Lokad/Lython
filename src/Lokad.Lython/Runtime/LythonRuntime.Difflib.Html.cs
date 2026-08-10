using System.Net;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class DifflibHtmlDiffObject
    {
        private readonly int _tabsize;
        private readonly int? _wrapcolumn;
        private readonly object? _linejunk;
        private readonly object? _charjunk;

        public DifflibHtmlDiffObject(int tabsize, int? wrapcolumn, object? linejunk, object? charjunk)
        {
            _tabsize = tabsize;
            _wrapcolumn = wrapcolumn;
            _linejunk = linejunk;
            _charjunk = charjunk;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "make_table" => BoundCallable.Create(MakeTable, LythonCallableSignature.Create("HtmlDiff.make_table", ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines"], RequiredCount: 2)),
                "make_file" => BoundCallable.Create(MakeFile, LythonCallableSignature.Create("HtmlDiff.make_file", ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines", "charset"], RequiredCount: 2)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object MakeTable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseHtmlArguments(arguments, includeCharset: false, span);
            var fromLines = DifflibModule.RequireStringSequence(arguments[0], "HtmlDiff.make_table(fromlines, tolines)", span);
            var toLines = DifflibModule.RequireStringSequence(arguments[1], "HtmlDiff.make_table(fromlines, tolines)", span);
            return PyString.FromString(BuildTable(fromLines, toLines, options, context, span));
        }

        private object MakeFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseHtmlArguments(arguments, includeCharset: true, span);
            var fromLines = DifflibModule.RequireStringSequence(arguments[0], "HtmlDiff.make_file(fromlines, tolines)", span);
            var toLines = DifflibModule.RequireStringSequence(arguments[1], "HtmlDiff.make_file(fromlines, tolines)", span);
            var table = BuildTable(fromLines, toLines, options, context, span);
            var html =
                "<!DOCTYPE html>\n" +
                "<html><head><meta charset=\"" + Html(options.Charset) + "\">\n" +
                "<style>.diff{font-family:Consolas,monospace;border-collapse:collapse}.diff td,.diff th{padding:2px 6px;border:1px solid #ddd}.diff_add{background:#e6ffed}.diff_sub{background:#ffeef0}.diff_chg{background:#fff5b1}.diff_header{background:#f6f8fa}</style>\n" +
                "</head><body>\n" +
                table +
                "\n</body></html>\n";
            return PyString.FromString(html);
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
                ParseHtmlString(arguments, 2, string.Empty, "fromdesc", span),
                ParseHtmlString(arguments, 3, string.Empty, "todesc", span),
                arguments.Length >= 5 && arguments[4] is not PyNone && IsTruthy(arguments[4]),
                arguments.Length >= 6 && arguments[5] is not PyNone ? DifflibModule.RequireInt32(arguments[5], "HtmlDiff numlines expects an integer.", span) : 5,
                includeCharset ? ParseHtmlString(arguments, 6, "utf-8", "charset", span) : "utf-8");
        }

        private static string ParseHtmlString(object[] arguments, int index, string defaultValue, string name, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"HtmlDiff {name} expects a string.", span);
            }

            return text.AsString();
        }

        private string BuildTable(IReadOnlyList<PyString> fromLines, IReadOnlyList<PyString> toLines, HtmlOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var a = fromLines.Select(line => PyString.FromString(PrepareHtmlLine(line.AsString()))).ToArray();
            var b = toLines.Select(line => PyString.FromString(PrepareHtmlLine(line.AsString()))).ToArray();
            var matcher = new DifflibSequenceMatcherObject(_linejunk, new PyList(a.Cast<object>()), new PyList(b.Cast<object>()), autojunk: true, span, context);
            var groups = options.Context
                ? matcher.BuildGroupedOpcodes(options.NumLines).ToArray()
                : [matcher.BuildOpcodes()];

            var builder = new StringBuilder();
            builder.Append("<table class=\"diff\" summary=\"Differences\">\n");
            if (options.FromDescription.Length != 0 || options.ToDescription.Length != 0)
            {
                builder.Append("<thead><tr><th class=\"diff_next\"></th><th colspan=\"2\" class=\"diff_header\">");
                builder.Append(Html(options.FromDescription));
                builder.Append("</th><th class=\"diff_next\"></th><th colspan=\"2\" class=\"diff_header\">");
                builder.Append(Html(options.ToDescription));
                builder.Append("</th></tr></thead>\n");
            }

            builder.Append("<tbody>\n");
            var firstGroup = true;
            foreach (var group in groups)
            {
                if (!firstGroup)
                {
                    builder.Append("<tr><td colspan=\"6\" class=\"diff_next\"></td></tr>\n");
                }

                firstGroup = false;
                foreach (var opcode in group)
                {
                    AppendHtmlOpcode(builder, opcode, a, b);
                }
            }

            builder.Append("</tbody>\n</table>");
            return builder.ToString();
        }

        private string PrepareHtmlLine(string line)
        {
            var withoutNewline = line.TrimEnd('\r', '\n');
            return ExpandTabs(withoutNewline, _tabsize);
        }

        private static void AppendHtmlOpcode(StringBuilder builder, DiffOpcode opcode, IReadOnlyList<PyString> a, IReadOnlyList<PyString> b)
        {
            switch (opcode.Tag)
            {
                case DiffTag.Equal:
                    for (var offset = 0; offset < opcode.I2 - opcode.I1; offset++)
                    {
                        AppendHtmlRow(builder, opcode.I1 + offset + 1, a[opcode.I1 + offset].AsString(), string.Empty, opcode.J1 + offset + 1, b[opcode.J1 + offset].AsString(), string.Empty);
                    }

                    break;
                case DiffTag.Delete:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        AppendHtmlRow(builder, i + 1, a[i].AsString(), "diff_sub", null, string.Empty, string.Empty);
                    }

                    break;
                case DiffTag.Insert:
                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        AppendHtmlRow(builder, null, string.Empty, string.Empty, j + 1, b[j].AsString(), "diff_add");
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
                            hasLeft ? a[opcode.I1 + offset].AsString() : string.Empty,
                            hasLeft ? "diff_chg" : string.Empty,
                            hasRight ? opcode.J1 + offset + 1 : null,
                            hasRight ? b[opcode.J1 + offset].AsString() : string.Empty,
                            hasRight ? "diff_chg" : string.Empty);
                    }

                    break;
            }
        }

        private static void AppendHtmlRow(StringBuilder builder, int? leftNumber, string leftText, string leftClass, int? rightNumber, string rightText, string rightClass)
        {
            builder.Append("<tr><td class=\"diff_next\"></td><td class=\"diff_header\">");
            builder.Append(leftNumber?.ToString() ?? string.Empty);
            builder.Append("</td><td nowrap=\"nowrap\"");
            AppendClass(builder, leftClass);
            builder.Append(">");
            builder.Append(HtmlText(leftText));
            builder.Append("</td><td class=\"diff_next\"></td><td class=\"diff_header\">");
            builder.Append(rightNumber?.ToString() ?? string.Empty);
            builder.Append("</td><td nowrap=\"nowrap\"");
            AppendClass(builder, rightClass);
            builder.Append(">");
            builder.Append(HtmlText(rightText));
            builder.Append("</td></tr>\n");
        }

        private static void AppendClass(StringBuilder builder, string className)
        {
            if (className.Length != 0)
            {
                builder.Append(" class=\"");
                builder.Append(className);
                builder.Append("\"");
            }
        }

        private static string HtmlText(string text)
            => Html(text).Replace(" ", "&nbsp;", StringComparison.Ordinal).Replace("\t", "&nbsp;", StringComparison.Ordinal);

        private static string Html(string text)
            => WebUtility.HtmlEncode(text);

        private static string ExpandTabs(string text, int tabsize)
        {
            if (tabsize <= 0 || text.IndexOf('\t') < 0)
            {
                return text;
            }

            var builder = new StringBuilder();
            var column = 0;
            foreach (var ch in text)
            {
                if (ch == '\t')
                {
                    var spaces = tabsize - column % tabsize;
                    builder.Append(' ', spaces);
                    column += spaces;
                }
                else
                {
                    builder.Append(ch);
                    column++;
                }
            }

            return builder.ToString();
        }

        private readonly record struct HtmlOptions(
            string FromDescription,
            string ToDescription,
            bool Context,
            int NumLines,
            string Charset);
    }

}
