using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class OpenPyxlWorkbook
    {
        private object CreateSheet(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var title = arguments.Length >= 1 && arguments[0] is not PyNone
                ? ExpectString(arguments[0], "Workbook.create_sheet(title)", span)
                : NextSheetName();
            title = MakeUniqueSheetTitle(title, current: null, span);

            var worksheet = new OpenPyxlWorksheet(title) { Workbook = this };
            var active = _worksheets.Count == 0 ? null : _worksheets[ActiveIndex];
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                if (!PyNumberOps.TryAsInteger(arguments[1], out var indexInteger) ||
                    indexInteger < new BigInteger(int.MinValue) ||
                    indexInteger > new BigInteger(int.MaxValue))
                {
                    throw new LythonRuntimeException("ValueError", "Workbook.create_sheet(..., index=...) expects a valid worksheet index.", span);
                }

                _worksheets.Insert(NormalizeInsertIndex((int)indexInteger), worksheet);
                if (active is not null)
                {
                    _activeIndex = _worksheets.IndexOf(active);
                }
            }
            else
            {
                _worksheets.Add(worksheet);
            }

            RegisterWorksheet(worksheet);

            return worksheet;
        }

        private object Remove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlWorksheet worksheet || !ReferenceEquals(worksheet.Workbook, this))
            {
                throw new LythonRuntimeException("TypeError", "Workbook.remove(worksheet) expects a worksheet from this workbook.", span);
            }

            RemoveWorksheet(worksheet, span);
            return PyNone.Instance;
        }

        private void SetActive(object value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                if (integer < BigInteger.Zero || integer >= new BigInteger(_worksheets.Count))
                {
                    throw new LythonRuntimeException("ValueError", "Workbook.active expects a valid worksheet index.", span);
                }

                _activeIndex = (int)integer;
                return;
            }

            var worksheet = ResolveOwnedWorksheet(value, "Workbook.active", span);
            _activeIndex = _worksheets.IndexOf(worksheet);
        }

        private void RemoveWorksheet(OpenPyxlWorksheet worksheet, LythonSourceSpan span)
        {
            if (_worksheets.Count == 1)
            {
                throw new LythonRuntimeException("ValueError", "Cannot remove the only worksheet.", span);
            }

            var removedIndex = _worksheets.IndexOf(worksheet);
            _worksheets.Remove(worksheet);
            _index.RemoveWorksheet(worksheet);
            worksheet.Workbook = null;
            if (_activeIndex == removedIndex)
            {
                _activeIndex = Math.Min(removedIndex, _worksheets.Count - 1);
            }
            else if (removedIndex >= 0 && removedIndex < _activeIndex)
            {
                _activeIndex--;
            }
        }

        private object CopyWorksheet(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var source = ExpectOwnedWorksheet(arguments, "Workbook.copy_worksheet(from_worksheet)", span);
            var copyTitle = MakeUniqueSheetTitle(MakeCopyTitle(source.Title), current: null, span);
            var copy = source.Copy(copyTitle);
            _worksheets.Add(copy);
            RegisterWorksheet(copy);
            return copy;

            static string MakeCopyTitle(string sourceTitle)
            {
                const string suffix = " Copy";
                var prefixLength = Math.Min(sourceTitle.Length, 31 - suffix.Length);
                return sourceTitle[..prefixLength] + suffix;
            }
        }

        private object Index(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var worksheet = ExpectOwnedWorksheet(arguments, "Workbook.index(worksheet)", span);
            return new BigInteger(_worksheets.IndexOf(worksheet));
        }

        private object MoveSheet(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var worksheet = ResolveWorksheetArgument(arguments[0], "Workbook.move_sheet(sheet)", span);
            var offset = 0;
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                if (!PyNumberOps.TryAsInteger(arguments[1], out var offsetInteger) ||
                    offsetInteger < new BigInteger(int.MinValue) ||
                    offsetInteger > new BigInteger(int.MaxValue))
                {
                    throw new LythonRuntimeException("ValueError", "Workbook.move_sheet(..., offset=...) expects an integer offset.", span);
                }

                offset = (int)offsetInteger;
            }

            var oldIndex = _worksheets.IndexOf(worksheet);
            var newIndex = Math.Clamp(oldIndex + offset, 0, _worksheets.Count - 1);
            if (newIndex != oldIndex)
            {
                var active = _worksheets[ActiveIndex];
                _worksheets.RemoveAt(oldIndex);
                _worksheets.Insert(newIndex, worksheet);
                _activeIndex = _worksheets.IndexOf(active);
            }

            return PyNone.Instance;
        }

        private object AddNamedStyle(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlStyleValue style || style.Kind != OpenPyxlStyleKind.NamedStyle)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.add_named_style(style) expects an openpyxl.styles.NamedStyle.", span);
            }

            var name = NamedStyleName(style, span);
            if (_index.ContainsNamedStyle(name))
            {
                throw new LythonRuntimeException("ValueError", "Style " + name + " exists already.", span);
            }

            _namedStyles.Add(style);
            _index.AddNamedStyle(name, style);
            return PyNone.Instance;
        }

        private object GetSheetNames(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.get_sheet_names() expects no arguments.", span);
            }

            return CreateSheetNames();
        }

        private string NextSheetName()
        {
            var index = _worksheets.Count + 1;
            while (true)
            {
                var candidate = "Sheet" + index.ToString(CultureInfo.InvariantCulture);
                if (!_index.ContainsWorksheetTitle(candidate))
                {
                    return candidate;
                }

                index++;
            }
        }

        internal string MakeUniqueSheetTitle(string title, OpenPyxlWorksheet? current, LythonSourceSpan? span)
        {
            ValidateSheetTitle(title, span);
            if (!ContainsSheetTitle(title, current))
            {
                return title;
            }

            var suffix = 1;
            while (true)
            {
                var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
                var prefixLength = Math.Min(title.Length, 31 - suffixText.Length);
                var candidate = title[..prefixLength] + suffixText;
                if (!ContainsSheetTitle(candidate, current))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private bool ContainsSheetTitle(string title, OpenPyxlWorksheet? current)
            => _index.ContainsWorksheetTitle(title, current);

        internal string RenameWorksheet(OpenPyxlWorksheet worksheet, string title, LythonSourceSpan? span)
        {
            var uniqueTitle = MakeUniqueSheetTitle(title, worksheet, span);
            if (string.Equals(worksheet.Title, uniqueTitle, StringComparison.Ordinal))
            {
                return uniqueTitle;
            }

            _index.RenameWorksheet(worksheet, uniqueTitle);
            return uniqueTitle;
        }

        private int NormalizeInsertIndex(int index)
        {
            if (index < 0)
            {
                return Math.Max(0, _worksheets.Count + index);
            }

            return Math.Min(index, _worksheets.Count);
        }

        private OpenPyxlWorksheet ExpectOwnedWorksheet(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects a worksheet from this workbook.", span);
            }

            return ResolveOwnedWorksheet(arguments[0], owner, span);
        }

        private OpenPyxlWorksheet ResolveWorksheetArgument(object value, string owner, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out var title))
            {
                var titleText = title.AsString();
                if (!_index.TryGetWorksheet(titleText, out var worksheet))
                {
                    throw new LythonRuntimeException("KeyError", $"Worksheet {titleText} does not exist.", span);
                }

                return worksheet;
            }

            return ResolveOwnedWorksheet(value, owner, span);
        }

        private OpenPyxlWorksheet ResolveOwnedWorksheet(object value, string owner, LythonSourceSpan? span)
        {
            if (value is OpenPyxlWorksheet worksheet &&
                ReferenceEquals(worksheet.Workbook, this) &&
                _index.ContainsWorksheet(worksheet))
            {
                return worksheet;
            }

            throw new LythonRuntimeException("TypeError", owner + " expects a worksheet from this workbook.", span);
        }

        private void RegisterWorksheet(OpenPyxlWorksheet worksheet)
        {
            _index.AddWorksheet(worksheet);
            worksheet.Workbook = this;
        }
    }
}
