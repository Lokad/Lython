using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class OpenPyxlConditionalFormattingRule : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlConditionalFormattingRule(string? type, string? operatorValue, int? priority, IReadOnlyList<string> formulas, XElement? sourceXml)
        {
            Type = type;
            Operator = operatorValue;
            Priority = priority;
            Formulas = formulas;
            SourceXml = sourceXml;
        }

        public string? Type { get; }

        public string? Operator { get; }

        public int? Priority { get; }

        public IReadOnlyList<string> Formulas { get; }

        internal XElement? SourceXml { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "type" => Type is null ? PyNone.Instance : PyString.FromString(Type),
                "operator" => Operator is null ? PyNone.Instance : PyString.FromString(Operator),
                "priority" => Priority is null ? PyNone.Instance : new BigInteger(Priority.Value),
                "formula" => new PyList(Formulas.Select(formula => (object)PyString.FromString(formula))),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.formatting.rule.Rule>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlConditionalFormatting
    {
        private readonly List<CellRangeAddress> _ranges = new();

        public OpenPyxlConditionalFormatting(string sqref, IReadOnlyList<OpenPyxlConditionalFormattingRule> rules, LythonSourceSpan span)
        {
            foreach (var reference in sqref.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _ranges.Add(ParseCellOrRange(reference, span));
            }

            Rules = rules;
        }

        private OpenPyxlConditionalFormatting(IEnumerable<CellRangeAddress> ranges, IReadOnlyList<OpenPyxlConditionalFormattingRule> rules)
        {
            _ranges.AddRange(ranges);
            Rules = rules;
        }

        public IReadOnlyList<CellRangeAddress> Ranges => _ranges;

        public string Sqref => string.Join(" ", _ranges.Select(range => range.CellOrRangeReference));

        public IReadOnlyList<OpenPyxlConditionalFormattingRule> Rules { get; }

        public OpenPyxlConditionalFormatting Copy()
            => new(_ranges, Rules);

        internal void RewriteRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
            => RewriteDistinctCellRanges(_ranges, rewrite);
    }

    private sealed class OpenPyxlConditionalFormattingCollection :
        IPyDynamicAttributes,
        IPySubscriptableValue,
        IPyIterableValue,
        IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;
        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan? _allocationSpan;

        public OpenPyxlConditionalFormattingCollection(OpenPyxlWorksheet worksheet)
            : this(worksheet, governor: null, allocationSpan: null)
        {
        }

        public OpenPyxlConditionalFormattingCollection(OpenPyxlWorksheet worksheet, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
        {
            _worksheet = worksheet;
            _governor = governor;
            _allocationSpan = allocationSpan;
            if (governor is not null)
            {
                governor.Reserve(64L, allocationSpan);
                governor.Commit(64L);
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ranges" => _governor is null
                    ? new PyList(_worksheet.ConditionalFormattings.Select(formatting => (object)PyString.FromString(formatting.Sqref)))
                    : new PyList(_worksheet.ConditionalFormattings.Select(formatting => (object)PyString.FromString(formatting.Sqref, _governor, _allocationSpan)), _governor, _allocationSpan),
                "items" => BoundCallable.Create(Items, "ConditionalFormattingList.items", []),
                "add" => BoundCallable.Create(Add, "ConditionalFormattingList.add", ["range_string", "rule"], requiredCount: 2),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var reference = NormalizeCellOrRangeReference(ExpectString(index, "Worksheet.conditional_formatting[...] key", span), span);
            var formatting = _worksheet.ConditionalFormattings.FirstOrDefault(item => string.Equals(item.Sqref, reference, StringComparison.Ordinal));
            if (_governor is null)
            {
                return formatting is null
                    ? new PyList(Array.Empty<object>())
                    : new PyList(formatting.Rules.Cast<object>());
            }

            return formatting is null
                ? new PyList(Array.Empty<object>(), _governor, _allocationSpan)
                : new PyList(formatting.Rules.Cast<object>(), _governor, _allocationSpan);
        }

        public IEnumerable<object> Iterate()
            => _worksheet.ConditionalFormattings.Select(formatting => _governor is null
                ? (object)PyString.FromString(formatting.Sqref)
                : PyString.FromString(formatting.Sqref, _governor, _allocationSpan));

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.formatting.formatting.ConditionalFormattingList>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Items(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ConditionalFormattingList.items() expects no arguments.", span);
            }

            return new PyList(_worksheet.ConditionalFormattings
                .Select(formatting => (object)PyTuple.FromOwnedArray(
                    [
                        PyString.FromString(formatting.Sqref, context.MemoryGovernor, span),
                        new PyList(formatting.Rules.Cast<object>(), context.MemoryGovernor, span),
                    ],
                    context.MemoryGovernor,
                    span))
                .ToArray(), context.MemoryGovernor, span);
        }

        private static object Add(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", "ConditionalFormattingList.add(...) is not supported by Lython.", span);
        }
    }
}
