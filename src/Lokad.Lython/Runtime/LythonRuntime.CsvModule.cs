using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal enum CsvQuotingMode
    {
        Minimal = 0,
        All = 1,
        NonNumeric = 2,
        None = 3,
    }

    internal enum CsvExtrasAction
    {
        Raise,
        Ignore,
    }

    internal enum CsvCellKind
    {
        Text,
        Numeric,
    }

    private sealed class CsvModule : PyModule
    {
        public static readonly CsvModule Instance = new();

        private CsvModule() : base("csv")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "reader" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvReader, Reader),
                "writer" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvWriter, Writer),
                "DictReader" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvDictReader, DictReader),
                "DictWriter" => BuiltinCallable.Create(LythonKnownCallableSignatures.CsvDictWriter, DictWriter),
                "Error" => new ExceptionTypeValue("Error"),
                "QUOTE_MINIMAL" => new BigInteger((int)CsvQuotingMode.Minimal),
                "QUOTE_ALL" => new BigInteger((int)CsvQuotingMode.All),
                "QUOTE_NONNUMERIC" => new BigInteger((int)CsvQuotingMode.NonNumeric),
                "QUOTE_NONE" => new BigInteger((int)CsvQuotingMode.None),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Reader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            var records = ParseCsvRecords(arguments[0], options, span, context);
            return new CsvReaderObject(records.Rows, records.PhysicalLineCount, context.MemoryGovernor, span);
        }

        private object DictReader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictReader(f[, fieldnames][, restkey][, restval][, ...]) expects at least one argument.", span);
            }

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Dictionary, span);
            var records = ParseCsvRecords(arguments[0], options, span, context);
            var fieldNames = arguments.Length > 1 && arguments[1] is not PyNone
                ? ToCsvFieldNames(arguments[1], "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", span)
                : records.Rows.Count == 0
                    ? null
                    : ToFieldNameList((PyList)records.Rows[0], span);

            var firstDataRow = arguments.Length > 1 && arguments[1] is not PyNone ? 0 : 1;
            var rows = new PyList([], context.MemoryGovernor, span);
            if (fieldNames is not null)
            {
                for (var i = firstDataRow; i < records.Rows.Count; i++)
                {
                    rows.Add(CreateDictReaderRow(
                        (PyList)records.Rows[i],
                        fieldNames,
                        RestKey(arguments, 2),
                        RestValue(arguments, 3),
                        context,
                        span));
                }
            }

            return new CsvDictReaderObject(rows, fieldNames, records.PhysicalLineCount, context.MemoryGovernor, span);
        }

        private object Writer(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            ExecutionContext.TextFileHandle? file = null;
            if (arguments.Length > 0 && arguments[0] is not PyNone)
            {
                if (arguments[0] is ExecutionContext.TextFileHandle handle)
                {
                    file = handle;
                }
                else
                {
                    throw new LythonRuntimeException("TypeError", "csv.writer(fileobj[, dialect][, ...]) expects a text file handle.", span);
                }
            }

            var options = GetOptions(arguments, CsvOptionArgumentLayout.Standard, span);
            return new CsvWriterObject(options, file);
        }

        private object DictWriter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 2 || arguments[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(fileobj, fieldnames, ...) expects a text file handle and field names.", span);
            }

            var fieldNames = ToCsvFieldNames(arguments[1], "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", span);
            var restVal = arguments.Length > 2 && arguments[2] is not PyNone ? arguments[2] : PyString.Empty;
            var extrasAction = GetExtrasAction(arguments, 3, span);
            var options = GetOptions(arguments, CsvOptionArgumentLayout.Dictionary, span);
            return new CsvDictWriterObject(new CsvWriterObject(options, file), fieldNames, restVal, extrasAction);
        }

        private static CsvOptions GetOptions(
            object[] arguments,
            CsvOptionArgumentLayout layout,
            LythonSourceSpan span)
        {
            ValidateDialect(arguments, layout.Dialect, span);

            var delimiter = GetCharacterOption(arguments, layout.Delimiter, PyStringOps.CommaLiteral, "delimiter", allowNone: false, span).RequireNotNull();
            if (delimiter.AsString() is "\r" or "\n")
            {
                throw CsvError("csv delimiter cannot be a newline.", span);
            }

            var quotechar = GetCharacterOption(arguments, layout.QuoteCharacter, PyString.FromString("\""), "quotechar", allowNone: true, span);
            var quoting = GetQuoting(arguments, layout.Quoting, span);
            var doublequote = GetBooleanOption(arguments, layout.DoubleQuote, defaultValue: true, "doublequote", span);
            var escapechar = GetCharacterOption(arguments, layout.EscapeCharacter, null, "escapechar", allowNone: true, span);
            var skipinitialspace = GetBooleanOption(arguments, layout.SkipInitialSpace, defaultValue: false, "skipinitialspace", span);
            var lineterminator = GetStringOption(arguments, layout.LineTerminator, PyString.FromString("\n"), "lineterminator", allowNone: false, span);
            var strict = GetBooleanOption(arguments, layout.Strict, defaultValue: false, "strict", span);

            if (quoting == CsvQuotingMode.None && escapechar is null)
            {
                // This is legal until escaping is actually required.
            }

            return new CsvOptions(delimiter, quotechar, quoting, doublequote, escapechar, skipinitialspace, lineterminator, strict);
        }

        private static void ValidateDialect(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return;
            }

            if (PyStringOps.TryAsString(arguments[index], out var dialect) &&
                string.Equals(dialect.AsString(), "excel", StringComparison.Ordinal))
            {
                return;
            }

            throw new LythonRuntimeException("TypeError", "csv dialect registry is unsupported; pass explicit CSV options instead.", span);
        }

        private static PyString GetStringOption(object[] arguments, int index, PyString defaultValue, string name, bool allowNone, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                if (!allowNone && arguments.Length > index && arguments[index] is PyNone)
                {
                    throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
                }

                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var value))
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            return value;
        }

        private static PyString? GetCharacterOption(object[] arguments, int index, PyString? defaultValue, string name, bool allowNone, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            return GetRequiredCharacter(arguments[index], name, allowNone, span);
        }

        private static PyString? GetRequiredCharacter(object value, string name, bool allowNone, LythonSourceSpan span)
        {
            if (value is PyNone)
            {
                if (allowNone)
                {
                    return null;
                }

                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            if (text.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be one character.", span);
            }

            return text;
        }

        private static CsvQuotingMode GetQuoting(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return CsvQuotingMode.Minimal;
            }

            if (arguments[index] is not BigInteger integer || integer < 0 || integer > (int)CsvQuotingMode.None)
            {
                throw new LythonRuntimeException("TypeError", "csv quoting must be one of the QUOTE_* constants.", span);
            }

            return (CsvQuotingMode)(int)integer;
        }

        private static bool GetBooleanOption(object[] arguments, int index, bool defaultValue, string name, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (arguments[index] is not bool value)
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a bool.", span);
            }

            return value;
        }

        private static PyString[] ToCsvFieldNames(object value, string message, LythonSourceSpan span)
        {
            var names = new List<PyString>();
            foreach (var item in ToSequence(value, span))
            {
                if (!PyStringOps.TryAsString(item, out var name))
                {
                    throw new LythonRuntimeException("TypeError", message, span);
                }

                names.Add(name);
            }

            return [.. names];
        }

        private static PyString[] ToFieldNameList(PyList row, LythonSourceSpan span)
        {
            var names = new PyString[row.Count];
            for (var i = 0; i < row.Count; i++)
            {
                if (!PyStringOps.TryAsString(row[i], out var name))
                {
                    throw CsvError("csv.DictReader header row must contain strings.", span);
                }

                names[i] = name;
            }

            return names;
        }

        private static object RestKey(object[] arguments, int index)
            => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

        private static object RestValue(object[] arguments, int index)
            => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

        private static CsvExtrasAction GetExtrasAction(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return CsvExtrasAction.Raise;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var action))
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(..., extrasaction=...) expects a string.", span);
            }

            var text = action.AsString();
            if (!string.Equals(text, "raise", StringComparison.Ordinal) &&
                !string.Equals(text, "ignore", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("ValueError", "extrasaction must be 'raise' or 'ignore'.", span);
            }

            return text == "ignore" ? CsvExtrasAction.Ignore : CsvExtrasAction.Raise;
        }

        private static PyDict CreateDictReaderRow(PyList row, PyString[] fieldNames, object restKey, object restValue, ExecutionContext context, LythonSourceSpan span)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            var count = Math.Min(row.Count, fieldNames.Length);
            for (var i = 0; i < count; i++)
            {
                dict.SetItem(fieldNames[i], row[i]);
            }

            for (var i = count; i < fieldNames.Length; i++)
            {
                dict.SetItem(fieldNames[i], restValue);
            }

            if (row.Count > fieldNames.Length)
            {
                var extras = new object[row.Count - fieldNames.Length];
                for (var i = fieldNames.Length; i < row.Count; i++)
                {
                    extras[i - fieldNames.Length] = row[i];
                }

                dict.SetItem(ValidateDictionaryKey(restKey, span, context.MemoryGovernor), new PyList(extras, context.MemoryGovernor, span));
            }

            return dict;
        }

        private static CsvReadResult ParseCsvRecords(object source, CsvOptions options, LythonSourceSpan span, ExecutionContext context)
        {
            var parser = new CsvRecordParser(options, context, span);
            var physicalLineCount = 0;
            foreach (var item in ToSequence(source, span))
            {
                context.CheckExecutionBudget(span);
                if (!PyStringOps.TryAsString(item, out var line))
                {
                    throw new LythonRuntimeException("TypeError", "csv.reader(csvfile) expects an iterable of strings.", span);
                }

                physicalLineCount++;
                parser.Feed(line.AsString());
            }

            parser.Finish();
            return new CsvReadResult(parser.Rows, physicalLineCount);
        }

        private sealed record CsvReadResult(PyList Rows, int PhysicalLineCount);

        private sealed class CsvRecordParser
        {
            private readonly CsvOptions _options;
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly string _delimiter;
            private readonly string? _quoteCharacter;
            private readonly string? _escapeCharacter;
            private readonly List<object> _row = new();
            private readonly StringBuilder _field = new();
            private bool _inQuotes;
            private bool _fieldStarted;
            private bool _afterQuote;
            private bool _recordStarted;

            public CsvRecordParser(CsvOptions options, ExecutionContext context, LythonSourceSpan span)
            {
                _options = options;
                _context = context;
                _span = span;
                _delimiter = options.Delimiter.AsString();
                _quoteCharacter = options.QuoteChar?.AsString();
                _escapeCharacter = options.EscapeChar?.AsString();
                Rows = new PyList([], context.MemoryGovernor, span);
            }

            public PyList Rows { get; }

            public void Feed(string text)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    var c = text[i];
                    if (_inQuotes)
                    {
                        if (TryConsumeEscape(text, ref i))
                        {
                            continue;
                        }

                        if (MatchesQuoteAt(text, i))
                        {
                            var quoteCharacter = _quoteCharacter.RequireNotNull();
                            if (_options.DoubleQuote && MatchesAt(text, i + quoteCharacter.Length, quoteCharacter))
                            {
                                // A doubled quote denotes one literal quote, including for a
                                // non-BMP Python character represented by two UTF-16 code units.
                                _field.Append(quoteCharacter);
                                i += (2 * quoteCharacter.Length) - 1;
                            }
                            else
                            {
                                _inQuotes = false;
                                _afterQuote = true;
                                i += quoteCharacter.Length - 1;
                            }

                            continue;
                        }

                        _field.Append(c);
                        continue;
                    }

                    if (c == '\r' || c == '\n')
                    {
                        FinishRecord();
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            i++;
                        }

                        continue;
                    }

                    if (MatchesAt(text, i, _delimiter))
                    {
                        FinishField();
                        i += _delimiter.Length - 1;
                        _recordStarted = true;
                        _afterQuote = false;
                        continue;
                    }

                    if (_options.SkipInitialSpace && !_fieldStarted && _field.Length == 0 && c == ' ')
                    {
                        continue;
                    }

                    if (MatchesQuoteAt(text, i) && !_fieldStarted)
                    {
                        _inQuotes = true;
                        _fieldStarted = true;
                        _recordStarted = true;
                        i += _quoteCharacter.RequireNotNull().Length - 1;
                        continue;
                    }

                    if (_afterQuote)
                    {
                        // Once a quoted field closes, only a delimiter or physical line ending
                        // may follow it; accepting ordinary text here would hide malformed CSV.
                        throw CsvError("Invalid csv input.", _span);
                    }

                    if (TryConsumeEscape(text, ref i))
                    {
                        continue;
                    }

                    _fieldStarted = true;
                    _recordStarted = true;
                    _field.Append(c);
                }

                if (_inQuotes)
                {
                    if (text.Length == 0 || (text[^1] != '\n' && text[^1] != '\r'))
                    {
                        _field.Append('\n');
                    }

                    return;
                }

                if (text.Length == 0)
                {
                    Rows.Add(new PyList([], _context.MemoryGovernor, _span));
                    return;
                }

                if (text[^1] != '\n' && text[^1] != '\r')
                {
                    FinishRecord();
                }
            }

            public void Finish()
            {
                if (_inQuotes)
                {
                    throw CsvError("Invalid csv input.", _span);
                }
            }

            private bool TryConsumeEscape(string text, ref int index)
            {
                if (_escapeCharacter is null || !MatchesAt(text, index, _escapeCharacter))
                {
                    return false;
                }

                if (index + _escapeCharacter.Length >= text.Length)
                {
                    if (_options.Strict)
                    {
                        throw CsvError("Invalid csv input.", _span);
                    }

                    _field.Append(_escapeCharacter);
                    index += _escapeCharacter.Length - 1;
                    return true;
                }

                index += _escapeCharacter.Length;
                _field.Append(text[index]);
                _fieldStarted = true;
                _recordStarted = true;
                return true;
            }

            private void FinishField()
            {
                _row.Add(PyString.FromString(_field.ToString()));
                _field.Clear();
                _fieldStarted = false;
                _afterQuote = false;
            }

            private void FinishRecord()
            {
                if (!_recordStarted && !_fieldStarted && _field.Length == 0 && _row.Count == 0)
                {
                    Rows.Add(new PyList([], _context.MemoryGovernor, _span));
                    return;
                }

                FinishField();
                Rows.Add(new PyList(_row, _context.MemoryGovernor, _span));
                _row.Clear();
                _recordStarted = false;
                _afterQuote = false;
            }

            private bool MatchesQuoteAt(string text, int index)
                => _quoteCharacter is not null && MatchesAt(text, index, _quoteCharacter);
        }

        private static bool MatchesAt(string text, int index, string value)
        {
            if (index + value.Length > text.Length)
            {
                return false;
            }

            return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }
    }

    internal sealed record CsvOptions(
        PyString Delimiter,
        PyString? QuoteChar,
        CsvQuotingMode Quoting,
        bool DoubleQuote,
        PyString? EscapeChar,
        bool SkipInitialSpace,
        PyString LineTerminator,
        bool Strict);

    private static LythonRuntimeException CsvError(string message, LythonSourceSpan span)
        => new("Error", message, span);

}
