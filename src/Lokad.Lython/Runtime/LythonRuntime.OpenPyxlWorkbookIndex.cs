namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Keeps workbook identity and name lookups synchronized across every mutation path.
    /// Python worksheet titles are exact for subscription, but title uniqueness follows
    /// openpyxl's case-insensitive rule.
    /// </summary>
    private sealed class OpenPyxlWorkbookIndex
    {
        private readonly Dictionary<string, OpenPyxlWorksheet> _worksheetsByTitle = new(StringComparer.Ordinal);
        private readonly Dictionary<string, OpenPyxlWorksheet> _worksheetsByTitleIgnoreCase = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<OpenPyxlWorksheet> _worksheets = [];
        private readonly Dictionary<string, OpenPyxlStyleValue> _namedStylesByName = new(StringComparer.Ordinal);

        public void AddWorksheet(OpenPyxlWorksheet worksheet)
        {
            _worksheetsByTitle.Add(worksheet.Title, worksheet);
            _worksheetsByTitleIgnoreCase.Add(worksheet.Title, worksheet);
            _worksheets.Add(worksheet);
        }

        public void RemoveWorksheet(OpenPyxlWorksheet worksheet)
        {
            _worksheetsByTitle.Remove(worksheet.Title);
            _worksheetsByTitleIgnoreCase.Remove(worksheet.Title);
            _worksheets.Remove(worksheet);
        }

        public void RenameWorksheet(OpenPyxlWorksheet worksheet, string title)
        {
            _worksheetsByTitle.Remove(worksheet.Title);
            _worksheetsByTitleIgnoreCase.Remove(worksheet.Title);
            _worksheetsByTitle.Add(title, worksheet);
            _worksheetsByTitleIgnoreCase.Add(title, worksheet);
        }

        public bool TryGetWorksheet(string title, [MaybeNullWhen(false)] out OpenPyxlWorksheet worksheet)
            => _worksheetsByTitle.TryGetValue(title, out worksheet);

        public bool ContainsWorksheetTitle(string title)
            => _worksheetsByTitle.ContainsKey(title);

        public bool ContainsWorksheetTitle(string title, OpenPyxlWorksheet? current)
            => _worksheetsByTitleIgnoreCase.TryGetValue(title, out var worksheet) &&
               !ReferenceEquals(worksheet, current);

        public bool ContainsWorksheet(OpenPyxlWorksheet worksheet)
            => _worksheets.Contains(worksheet);

        public void ResetNamedStyles(IEnumerable<OpenPyxlStyleValue> styles)
        {
            _namedStylesByName.Clear();
            foreach (var style in styles)
            {
                _namedStylesByName.TryAdd(NamedStyleName(style, null), style);
            }
        }

        public OpenPyxlStyleValue? FindNamedStyle(string name)
            => _namedStylesByName.GetValueOrDefault(name);

        public bool ContainsNamedStyle(string name)
            => _namedStylesByName.ContainsKey(name);

        public void AddNamedStyle(string name, OpenPyxlStyleValue style)
            => _namedStylesByName.Add(name, style);
    }
}
