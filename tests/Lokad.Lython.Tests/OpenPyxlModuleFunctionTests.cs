using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class OpenPyxlModuleFunctionTests
{
    [Fact]
    public void OpenPyxlWorkbook_CreatesSavesAndReloadsWithCellApi()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import openpyxl
from openpyxl import Workbook, load_workbook
from openpyxl.utils import column_index_from_string, get_column_letter

wb = Workbook()
ws = wb.active
ws.title = "Inventory"
ws["A1"] = "sku"
ws.cell(row=1, column=2, value="qty")
ws.append(["A001", 5])
ws["C2"] = "=B2*2"
other = wb.create_sheet("Second")
other["B2"] = "extra"
wb.save("/out.xlsx")

loaded = load_workbook("/out.xlsx", data_only=False)
row_text = ""
for sku, qty, double_qty in loaded["Inventory"].iter_rows(min_row=2, max_row=2, min_col=1, max_col=3, values_only=True):
    row_text = str(sku) + "|" + str(qty) + "|" + str(double_qty)

parts = [
    loaded.sheetnames[0],
    row_text,
    loaded["Second"]["B2"].value,
    get_column_letter(28) + str(column_index_from_string("AB")),
]
loaded["Inventory"]["D2"] = "resaved"
loaded.save("/roundtrip.xlsx")
__lython_file = open("/result.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.True(host.Exists("/out.xlsx"));
        Assert.True(host.Exists("/roundtrip.xlsx"));
        Assert.Equal("Inventory|A001|5|=B2*2|extra|AB28", host.ReadText("/result.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_ReadsExcelAuthoredWorkbook()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", data_only=False)
ws = wb["Inventory"]
cached = openpyxl.load_workbook("/input.xlsx", data_only=True)["Inventory"]["E2"].value
parts = [
    wb.sheetnames[0],
    str(ws.max_row),
    str(ws.max_column),
    ws["A2"].value,
    ws.cell(row=2, column=2).value.strip(),
    str(ws["C2"].value),
    str(ws["D2"].value),
    ws["E2"].value,
    str(cached),
    wb["Second"]["B2"].value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Inventory|3|5|A001|Widget|5|True|=C2*2|10|extra", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlCell_ExposesCommonCoordinateHelpers()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws.title = "Cells"
ws["B2"] = "anchor"
cell = ws["B2"]
target = cell.offset(row=1, column=-1)
target.value = "offset"
same = cell.offset()
parts = [
    cell.column_letter,
    str(cell.col_idx),
    cell.parent.title,
    cell.internal_value,
    str(cell.is_date),
    cell.base_date,
    same.coordinate,
    target.coordinate,
    ws["A3"].value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("B|2|Cells|anchor|False|1899-12-30 00:00:00|B2|A3|offset", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlFormulaText_RoundTripsWhenDataOnlyFalse()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = 2
ws["B1"] = "=A1*3"
wb.save("/formula.xlsx")

loaded = load_workbook("/formula.xlsx", data_only=False)
first = loaded.active["B1"].value
loaded.save("/resaved.xlsx")
again = load_workbook("/resaved.xlsx", data_only=False).active["B1"].value
__lython_file = open("/out.txt", "w")
__lython_file.write(first + "|" + again)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("=A1*3|=A1*3", host.ReadText("/out.txt"));
        var workbookXml = WorkbookPartText(host.ReadWorkbook("/formula.xlsx"), "xl/workbook.xml");
        Assert.Contains("fullCalcOnLoad=\"1\"", workbookXml, StringComparison.Ordinal);
        Assert.Contains("forceFullCalc=\"1\"", workbookXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlFormulaCachedValues_ArePreservedWhenPresent()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/cached.xlsx", CachedFormulaWorkbookBytes());

        var result = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/cached.xlsx", data_only=False)
text = wb.active["B1"].value
wb.save("/copy.xlsx")
cached = load_workbook("/copy.xlsx", data_only=True).active["B1"].value
__lython_file = open("/out.txt", "w")
__lython_file.write(text + "|" + str(cached))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("=A1*3|6", host.ReadText("/out.txt"));

        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("<c r=\"B1\"><f>A1*3</f><v>6</v></c>", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlFormulaMetadata_PreservesSharedAndArrayFormulaElements()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/formulas.xlsx", SharedAndArrayFormulaWorkbookBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/formulas.xlsx", data_only=False)
wb.active["D1"] = "touched"
wb.save("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(wb.active["B1"].value + "|" + wb.active["B2"].value + "|" + wb.active["C1"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("=A1*2|=|=A1:A2*2", host.ReadText("/out.txt"));
        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("<f t=\"shared\" ref=\"B1:B2\" si=\"0\">A1*2</f>", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("<f t=\"shared\" si=\"0\" />", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("<f t=\"array\" ref=\"C1:C2\">A1:A2*2</f>", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlDataOnlyWorkbook_RejectsSave()
    {
        var host = new MockLythonHost();

        var seed = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
wb.active["A1"] = 2
wb.active["B1"] = "=A1*3"
wb.save("/formula.xlsx")
""",
            host);

        Assert.True(seed.Success, seed.Failure?.Message ?? string.Join(Environment.NewLine, seed.Diagnostics.Select(d => d.Message)));

        var result = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/formula.xlsx", data_only=True)
wb.save("/destroyed.xlsx")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("data_only=True", result.Failure?.Message, StringComparison.Ordinal);
        Assert.False(host.Exists("/destroyed.xlsx"));
    }

    [Fact]
    public void OpenPyxlStrings_PreserveWhitespaceAndNewLines()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "  padded  "
ws["A2"] = "line1\nline2"
wb.save("/strings.xlsx")

loaded = load_workbook("/strings.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded.active["A1"].value + "|" + loaded.active["A2"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("  padded  |line1\nline2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlRichText_LoadsAsPlainTextIncludingRichTextMode()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/rich.xlsx", RichTextWorkbookBytes());

        var result = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/rich.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(wb.active["A1"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("rich text", host.ReadText("/out.txt"));

        var richText = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/rich.xlsx", rich_text=True)
__lython_file = open("/rich.txt", "w")
__lython_file.write(wb.active["A1"].value)
__lython_file.close()
""",
            host);

        Assert.True(richText.Success, richText.Failure?.Message ?? string.Join(" | ", richText.Diagnostics.Select(d => d.Message)));
        Assert.Equal("rich text", host.ReadText("/rich.txt"));
    }

    [Fact]
    public void OpenPyxlSave_RewritesSharedStringsAsInlineStrings()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/shared.xlsx", RichTextWorkbookBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/shared.xlsx")
before = wb.active["A1"].value
wb.active["A1"] = before + " saved"
wb.save("/copy.xlsx")

loaded = openpyxl.load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded.active["A1"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("rich text saved", host.ReadText("/out.txt"));
        Assert.False(WorkbookHasPart(host.ReadWorkbook("/copy.xlsx"), "xl/sharedStrings.xml"));
        Assert.DoesNotContain("sharedStrings", WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/_rels/workbook.xml.rels"), StringComparison.Ordinal);
        Assert.Contains("t=\"inlineStr\"", WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlCellValues_SupportDecimalValues()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from decimal import Decimal
from openpyxl import Workbook
from openpyxl import load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = Decimal("1.23")
ws["A2"] = Decimal("2")
wb.save("/decimal.xlsx")

loaded = load_workbook("/decimal.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{loaded.active['A1'].value}|{loaded.active['A2'].value}")
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1.23|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlCellValues_SupportDateTimeValues()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import datetime
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = datetime.date(2024, 1, 2)
ws["B1"] = datetime.datetime(2024, 1, 2, 3, 4, 5)
ws["C1"] = datetime.time(7, 8, 9)
ws["D1"] = datetime.timedelta(days=1, hours=2, minutes=3)
wb.save("/dates.xlsx")

loaded = load_workbook("/dates.xlsx")
out = loaded.active
values = [
    out["A1"].value.isoformat(), out["A1"].data_type, str(out["A1"].is_date), out["A1"].number_format,
    out["B1"].value.isoformat(), out["B1"].data_type, str(out["B1"].is_date), out["B1"].number_format,
    out["C1"].value.isoformat(), out["C1"].data_type, str(out["C1"].is_date), out["C1"].number_format,
    str(int(out["D1"].value.total_seconds())), out["D1"].data_type, str(out["D1"].is_date), out["D1"].number_format,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "2024-01-02|d|True|yyyy-mm-dd|" +
            "2024-01-02T03:04:05|d|True|yyyy-mm-dd h:mm:ss|" +
            "07:08:09|d|True|h:mm:ss|" +
            "93780|d|True|[hh]:mm:ss",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_UsesDate1904Epoch()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/date1904.xlsx", Date1904WorkbookBytes());

        var result = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/date1904.xlsx")
cell = wb.active["A1"]
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{wb.epoch}|{cell.base_date}|{cell.value.isoformat()}|{cell.data_type}|{cell.is_date}")
__lython_file.close()
wb.save("/copy.xlsx")
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1904-01-01 00:00:00|1904-01-01 00:00:00|1904-01-02|d|True", host.ReadText("/out.txt"));

        var workbookXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/workbook.xml");
        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("<workbookPr date1904=\"1\" />", workbookXml, StringComparison.Ordinal);
        Assert.Contains("<v>1</v>", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlCell_SupportsNumberFormatRoundTrip()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = 123.456
ws["A1"].number_format = "0.00"
ws["B1"].number_format = "@"
ws["C1"] = 0.25
ws["C1"].number_format = "0.00%"
ws["D1"] = 1000
ws["D1"].number_format = "#,##0"
wb.save("/formats.xlsx")

loaded = load_workbook("/formats.xlsx")
out = loaded.active
text = f"{out['A1'].number_format}|{out['B1'].number_format}|{out['C1'].number_format}|{out['D1'].number_format}|{out['A1'].style_id}|{out['B1'].style_id}|{out['A1'].value}|{out['B1'].value}|{out['C1'].value}|{out['D1'].value}"
__lython_file = open("/out.txt", "w")
__lython_file.write(text)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("0.00|@|0.00%|#,##0|2|4|123.456|None|0.25|1000", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlCell_SupportsStyleValueAssignments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.styles import Alignment, Border, Font, PatternFill, Protection, Side

wb = Workbook()
cell = wb.active["A1"]
side = Side(style="thin", color="FF0000")
cell.font = Font(bold=True, italic=True, color="00FF00")
cell.fill = PatternFill(fill_type="solid", fgColor="FFFF00")
cell.border = Border(left=side)
cell.alignment = Alignment(horizontal="center", wrap_text=True)
cell.protection = Protection(locked=False, hidden=True)
parts = [
    str(cell.font.bold), str(cell.font.italic), cell.font.color,
    cell.fill.fill_type, cell.fill.fgColor,
    cell.border.left.style, cell.border.left.color,
    cell.alignment.horizontal, str(cell.alignment.wrap_text),
    str(cell.protection.locked), str(cell.protection.hidden),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|00FF00|solid|FFFF00|thin|FF0000|center|True|False|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlStyles_SupportCopyModuleForStyleValues()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from copy import copy, deepcopy
from openpyxl import Workbook
from openpyxl.styles import Border, Font, NamedStyle, PatternFill, Side

wb = Workbook()
ws = wb.active
font = Font(bold=True, color="00FF00")
fill = PatternFill(fill_type="solid", fgColor="FFFF00")
side = Side(style="thin", color="FF0000")
border = Border(left=side)
named = NamedStyle(name="accent")

font_copy = copy(font)
fill_copy = deepcopy(fill)
border_copy = deepcopy(border)
named_copy = copy(named)

ws["A1"].font = font_copy
ws["A1"].fill = fill_copy
ws["A1"].border = border_copy
ws["A1"].style = named_copy

parts = [
    str(font_copy is font),
    str(fill_copy is fill),
    str(border_copy is border),
    str(border_copy.left is border.left),
    str(font_copy.bold),
    font_copy.color,
    fill_copy.fgColor,
    border_copy.left.style,
    border_copy.left.color,
    named_copy.name,
    ws["A1"].style,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("False|False|False|False|True|00FF00|FFFF00|thin|FF0000|accent|accent", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlSave_RoundTripsStyleValueAssignments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook
from openpyxl.styles import Alignment, Border, Font, PatternFill, Protection, Side

wb = Workbook()
ws = wb.active
cell = ws["A1"]
side = Side(style="thin", color="FF0000")
cell.value = "styled"
cell.number_format = "0.00"
cell.font = Font(bold=True, italic=True, color="00FF00")
cell.fill = PatternFill(fill_type="solid", fgColor="FFFF00")
cell.border = Border(left=side)
cell.alignment = Alignment(horizontal="center", wrap_text=True)
cell.protection = Protection(locked=False, hidden=True)
wb.save("/styled.xlsx")

loaded = load_workbook("/styled.xlsx")
out = loaded.active["A1"]
parts = [
    out.value,
    out.number_format,
    str(out.font.bold), str(out.font.italic), out.font.color,
    out.fill.fill_type, out.fill.fgColor,
    out.border.left.style, out.border.left.color,
    out.alignment.horizontal, str(out.alignment.wrap_text),
    str(out.protection.locked), str(out.protection.hidden),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("styled|0.00|True|True|00FF00|solid|FFFF00|thin|FF0000|center|True|False|True", host.ReadText("/out.txt"));

        var stylesXml = WorkbookPartText(host.ReadWorkbook("/styled.xlsx"), "xl/styles.xml");
        Assert.Contains("applyFont=\"1\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("applyFill=\"1\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("applyBorder=\"1\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("applyAlignment=\"1\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("applyProtection=\"1\"", stylesXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlStyles_RoundTripsColorObjects()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook
from openpyxl.styles import Border, Font, PatternFill, Side
from openpyxl.styles.colors import Color

wb = Workbook()
ws = wb.active
cell = ws["A1"]
cell.value = "colors"
cell.font = Font(color=Color(rgb="FF00FF00"))
cell.fill = PatternFill(fill_type="solid", fgColor=Color(indexed=64, tint=0.5), bgColor=Color(auto=True))
cell.border = Border(left=Side(style="thin", color=Color(theme=1, tint=-0.25)))

before = [
    cell.font.color.type, cell.font.color.rgb, cell.font.color,
    cell.fill.fgColor.type, str(cell.fill.fgColor.indexed), str(cell.fill.fgColor.tint),
    cell.fill.bgColor.type, str(cell.fill.bgColor.auto),
    cell.border.left.color.type, str(cell.border.left.color.theme), str(cell.border.left.color.tint),
    str(cell.font.color == "FF00FF00"),
]
wb.save("/colors.xlsx")

loaded = load_workbook("/colors.xlsx")
out = loaded.active["A1"]
after = [
    out.font.color.type, out.font.color.rgb, out.font.color,
    out.fill.fgColor.type, str(out.fill.fgColor.indexed), str(out.fill.fgColor.tint),
    out.fill.bgColor.type, str(out.fill.bgColor.auto),
    out.border.left.color.type, str(out.border.left.color.theme), str(out.border.left.color.tint),
    str(out.font.color == "FF00FF00"),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(before) + "\n" + "|".join(after))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "rgb|FF00FF00|FF00FF00|indexed|64|0.5|auto|True|theme|1|-0.25|True\n" +
            "rgb|FF00FF00|FF00FF00|indexed|64|0.5|auto|True|theme|1|-0.25|True",
            host.ReadText("/out.txt"));

        var stylesXml = WorkbookPartText(host.ReadWorkbook("/colors.xlsx"), "xl/styles.xml");
        Assert.Contains("rgb=\"FF00FF00\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("indexed=\"64\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("auto=\"1\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("theme=\"1\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("tint=\"0.5\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("tint=\"-0.25\"", stylesXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlWorkbook_SupportsNamedStyleRegistrationAndAssignment()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.styles import Border, Font, NamedStyle, PatternFill, Side

wb = Workbook()
currency = NamedStyle(
    name="currency",
    number_format="0.00",
    font=Font(bold=True, color="FF0000"),
    fill=PatternFill(fill_type="solid", fgColor="FFFF00"),
    border=Border(left=Side(style="thin", color="00FF00")),
)
wb.add_named_style(currency)
ws = wb.active
ws["A1"].style = currency
ws["A2"].style = "currency"
parts = [
    str(wb.named_styles),
    str(wb.style_names),
    wb.named_styles[1].name,
    wb.named_styles[1].number_format,
    str(wb.named_styles[1].font.bold),
    wb.named_styles[1].fill.fgColor,
    ws["A1"].style,
    ws["A1"].number_format,
    str(ws["A1"].font.bold),
    ws["A1"].fill.fgColor,
    ws["A1"].border.left.style,
    ws["A1"].border.left.color,
    ws["A2"].style,
    ws["A2"].number_format,
    str(ws["A2"].font.bold),
    ws["A2"].fill.fgColor,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[Normal, currency]|[Normal, currency]|currency|0.00|True|FFFF00|currency|0.00|True|FFFF00|thin|00FF00|currency|0.00|True|FFFF00", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorkbook_LoadsNamedStylesAsObjects()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/named.xlsx", WorkbookWithNamedStyleBytes());

        var result = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/named.xlsx")
ws = wb.active
style = wb.named_styles[1]
ws["B1"].style = style
ws["C1"].style = "Headline"
parts = [
    str(wb.named_styles),
    str(wb.style_names),
    style.name,
    style.number_format,
    str(style.font.bold),
    style.font.color,
    style.fill.fgColor,
    style.border.left.style,
    style.border.left.color,
    ws["A1"].style,
    ws["A1"].number_format,
    str(ws["A1"].font.bold),
    ws["A1"].fill.fgColor,
    ws["B1"].style,
    ws["B1"].number_format,
    str(ws["B1"].font.bold),
    ws["B1"].fill.fgColor,
    ws["C1"].style,
    ws["C1"].number_format,
    str(ws["C1"].font.bold),
    ws["C1"].fill.fgColor,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[Normal, Headline]|[Normal, Headline]|Headline|0.00|True|FFFF0000|FFFFFF00|thin|FF00FF00|Headline|0.00|True|FFFFFF00|Headline|0.00|True|FFFFFF00|Headline|0.00|True|FFFFFF00", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlCell_PreservesErrorValues()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "#DIV/0!"
ws["A2"] = "#N/A"
wb.save("/errors.xlsx")

loaded = load_workbook("/errors.xlsx")
out = loaded.active
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{out['A1'].value}|{out['A1'].data_type}|{out['A2'].value}|{out['A2'].data_type}")
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("#DIV/0!|e|#N/A|e", host.ReadText("/out.txt"));

        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/errors.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("<c r=\"A1\" t=\"e\"><v>#DIV/0!</v></c>", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("<c r=\"A2\" t=\"e\"><v>#N/A</v></c>", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlCell_SupportsHyperlinkRoundTrip()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "Lokad"
ws["A1"].hyperlink = "https://www.lokad.com/"
wb.save("/links.xlsx")

loaded = load_workbook("/links.xlsx")
cell = loaded.active["A1"]
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{cell.value}|{cell.hyperlink.target}|{cell.hyperlink.ref}|{cell.hyperlink.display}")
__lython_file.close()
loaded.save("/copy.xlsx")
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Lokad|https://www.lokad.com/|A1|https://www.lokad.com/", host.ReadText("/out.txt"));

        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml");
        var relationshipsXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/_rels/sheet1.xml.rels");
        Assert.Contains("<hyperlink ref=\"A1\" r:id=\"rId1\" />", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\"", relationshipsXml, StringComparison.Ordinal);
        Assert.Contains("Target=\"https://www.lokad.com/\"", relationshipsXml, StringComparison.Ordinal);
        Assert.Contains("TargetMode=\"External\"", relationshipsXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlCell_SupportsComments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.comments import Comment

wb = Workbook()
ws = wb.active
ws["A1"].comment = Comment("review", "Analyst")
ws["A1"].comment.text = "approved"
copy = wb.copy_worksheet(ws)
__lython_file = open("/out.txt", "w")
__lython_file.write(ws["A1"].comment.text + "|" + ws["A1"].comment.author + "|" + copy["A1"].comment.text)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("approved|Analyst|approved", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlSubmoduleImports_RouteToSupportedMembers()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl.workbook import Workbook
from openpyxl.reader.excel import load_workbook
from openpyxl.utils.cell import column_index_from_string, get_column_letter

wb = Workbook()
ws = wb.active
ws.title = "Aliases"
ws["A1"] = get_column_letter(52)
ws["B1"] = column_index_from_string("AZ")
wb.save("/aliases.xlsx")

loaded = load_workbook("/aliases.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded["Aliases"]["A1"].value + "|" + str(loaded["Aliases"]["B1"].value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("AZ|52", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlUtilsCell_ExposesCommonPureHelpers()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl.utils.cell import (
    absolute_coordinate,
    cols_from_range,
    column_index_from_string,
    coordinate_from_string,
    coordinate_to_tuple,
    get_column_interval,
    get_column_letter,
    quote_sheetname,
    range_boundaries,
    rows_from_range,
)

col, row = coordinate_from_string("$B$12")
coord_row, coord_col = coordinate_to_tuple("C5")
cell_bounds = range_boundaries("B2:D4")
column_bounds = range_boundaries("A:B")
row_bounds = range_boundaries("1:3")

row_parts = []
for cells in rows_from_range("A1:B2"):
    row_parts.append("-".join(cells))

col_parts = []
for cells in cols_from_range("A1:B2"):
    col_parts.append("-".join(cells))

parts = [
    col + str(row),
    str(coord_row) + "x" + str(coord_col),
    ",".join(str(x) for x in cell_bounds),
    ",".join(str(x) for x in column_bounds),
    ",".join(str(x) for x in row_bounds),
    ",".join(get_column_interval("B", "D")),
    ",".join(get_column_interval(2, 4)),
    get_column_letter(18278) + str(column_index_from_string("ZZZ")),
    absolute_coordinate("A1:B2"),
    quote_sheetname("O'Brien"),
    ",".join(row_parts),
    ",".join(col_parts),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("B12|5x3|2,2,4,4|1,None,2,None|None,1,None,3|B,C,D|B,C,D|ZZZ18278|$A$1:$B$2|'O''Brien'|A1-B1,A2-B2|A1-A2,B1-B2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlSubmoduleImports_PreserveStaticCallShapeDiagnostics()
    {
        var result = new LythonEngine().Run(
            """
from openpyxl.utils.cell import get_column_letter
get_column_letter()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("openpyxl.utils.get_column_letter(col_idx) expects one argument.", StringComparison.Ordinal));

        var helperResult = new LythonEngine().Run(
            """
from openpyxl.utils.cell import range_boundaries
range_boundaries()
""",
            new MockLythonHost());

        Assert.False(helperResult.Success);
        Assert.Null(helperResult.Failure);
        Assert.Contains(helperResult.Diagnostics, d => d.Message.Contains("openpyxl.utils.range_boundaries(range_string) expects one argument.", StringComparison.Ordinal));

        var constructorResult = new LythonEngine().Run(
            """
from openpyxl.comments import Comment
Comment("text")
""",
            new MockLythonHost());

        Assert.False(constructorResult.Success);
        Assert.Null(constructorResult.Failure);
        Assert.Contains(constructorResult.Diagnostics, d => d.Message.Contains("openpyxl.comments.Comment(text, author) expects two arguments.", StringComparison.Ordinal));
    }

    [Fact]
    public void OpenPyxlUpstreamStyleSnippets_RunForDeclaredCompatibilityLevels()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import openpyxl
from datetime import date
from openpyxl import Workbook, load_workbook
from openpyxl.comments import Comment
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.worksheet.table import Table, TableStyleInfo

# Level 0: import and module discovery.
version = openpyxl.__version__

# Level 1: tabular workbook creation, append, save, load, and values_only rows.
wb = Workbook()
ws = wb.active
ws.title = "Data"
ws.append(["sku", "qty"])
ws.append(["A001", 5])
ws.append(["A002", 0])
wb.save("/levels.xlsx")

loaded = load_workbook("/levels.xlsx")
data = loaded["Data"]
available = []
for sku, qty in data.iter_rows(min_row=2, values_only=True):
    if int(qty) > 0:
        available.append(sku + ":" + str(qty))

# Level 2: Cell objects, coordinates, ranges, formulas, and dates.
qty_cell = data.cell(row=2, column=2)
qty_cell.value = qty_cell.value + 1
data["C2"] = "=B2*2"
data["D2"] = date(2024, 1, 2)
range_cells = data["A1:B2"]
cell_summary = qty_cell.coordinate + ":" + str(qty_cell.value) + ":" + range_cells[1][0].value + ":" + data["C2"].value + ":" + data["D2"].value.isoformat()

# Level 3: basic formatting and worksheet layout.
data.freeze_panes = "B2"
data.merge_cells("E1:F1")
data.column_dimensions["A"].width = 18
data.row_dimensions[1].height = 24
data["A1"].font = Font(bold=True)
data["B1"].fill = PatternFill(fill_type="solid", fgColor="FFFF00")
data["B1"].alignment = Alignment(horizontal="center")
data["B1"].border = Border(left=Side(style="thin"))
format_summary = str(data["A1"].font.bold) + ":" + data["B1"].fill.fgColor + ":" + data.freeze_panes + ":" + str(data.column_dimensions["A"].width) + ":" + str(data.row_dimensions[1].height)

# Level 4: broad workbook metadata objects in the supported subset.
data.auto_filter.ref = "A1:B3"
data["A2"].comment = Comment("reviewed", "qa")
table = Table(displayName="Inventory", ref="A1:B3")
table.tableStyleInfo = TableStyleInfo(name="TableStyleMedium2", showRowStripes=True)
data.add_table(table)
metadata_summary = data.auto_filter.ref + ":" + data["A2"].comment.text + ":" + data.tables["Inventory"].tableStyleInfo.name

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([version, ",".join(available), cell_summary, format_summary, metadata_summary]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("3.1.0+lython.0|A001:5|B2:6:A001:=B2*2:2024-01-02|True:FFFF00:B2:18:24|A1:B3:reviewed:TableStyleMedium2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlDeferredFeatureImports_ExposeNotImplementedStubs()
    {
        var host = new MockLythonHost();

        var imports = new LythonEngine().Run(
            """
from openpyxl.comments import Comment
from openpyxl.chart import BarChart, Reference
from openpyxl.styles import Alignment, Border, Font, NamedStyle, PatternFill, Protection, Side
from openpyxl.worksheet.table import Table, TableStyleInfo
from openpyxl.worksheet.datavalidation import DataValidation

__lython_file = open("/out.txt", "w")
__lython_file.write("ok")
__lython_file.close()
""",
            host);

        Assert.True(imports.Success, imports.Failure?.Message ?? string.Join(Environment.NewLine, imports.Diagnostics.Select(d => d.Message)));
        Assert.Equal("ok", host.ReadText("/out.txt"));

        var setupHost = new MockLythonHost();
        var setup = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.chart import BarChart, Reference

wb = Workbook()
ws = wb.active
ws.append(["sku", "qty"])
ws.append(["A001", 5])
chart = BarChart()
chart.title = "Sales"
chart.y_axis.title = "Qty"
data = Reference(ws, min_col=2, min_row=1, max_row=2)
chart.add_data(data, titles_from_data=True)
__lython_file = open("/out.txt", "w")
__lython_file.write(chart.type + "|" + chart.title + "|" + chart.y_axis.title + "|" + str(len(chart.series)))
__lython_file.close()
""",
            setupHost);

        Assert.True(setup.Success, setup.Failure?.Message ?? string.Join(Environment.NewLine, setup.Diagnostics.Select(d => d.Message)));
        Assert.Equal("BarChart|Sales|Qty|1", setupHost.ReadText("/out.txt"));

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.chart import BarChart

wb = Workbook()
wb.active.add_chart(BarChart(), "E2")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("Worksheet.add_chart", result.Failure?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Lython", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlCommonImportPaths_ExposeAliasesAndExceptionClasses()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/bad.xlsx", System.Text.Encoding.UTF8.GetBytes("not a zip"));

        var result = new LythonEngine().Run(
            """
import openpyxl
from openpyxl.cell.cell import Cell
from openpyxl.drawing.image import Image
from openpyxl.styles.colors import BLACK, BLUE, WHITE, Color
from openpyxl.utils import exceptions
from openpyxl.utils.exceptions import IllegalCharacterError, InvalidFileException, WorkbookAlreadySaved
from openpyxl.worksheet.worksheet import Worksheet

caught = []
try:
    raise WorkbookAlreadySaved("saved")
except WorkbookAlreadySaved as ex:
    caught.append(str(ex))

try:
    raise IllegalCharacterError("bad char")
except IllegalCharacterError as ex:
    caught.append(str(ex))

try:
    openpyxl.load_workbook("/bad.xlsx")
except InvalidFileException as ex:
    caught.append(ex.type)

__lython_file = open("/out.txt", "w")
__lython_file.write(BLACK + "," + WHITE + "," + BLUE + "|" + "|".join(caught))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("00000000,00FFFFFF,000000FF|WorkbookAlreadySaved(saved)|IllegalCharacterError(bad char)|InvalidFileException", host.ReadText("/out.txt"));

        var imageSetupHost = new MockLythonHost();
        var imageSetup = new LythonEngine().Run(
            """
from openpyxl.drawing.image import Image

image = Image("/image.png")
__lython_file = open("/image.txt", "w")
__lython_file.write(image.ref + "|" + image.format)
__lython_file.close()
""",
            imageSetupHost);

        Assert.True(imageSetup.Success, imageSetup.Failure?.Message ?? string.Join(Environment.NewLine, imageSetup.Diagnostics.Select(d => d.Message)));
        Assert.Equal("/image.png|png", imageSetupHost.ReadText("/image.txt"));

        var unsupported = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.drawing.image import Image

wb = Workbook()
wb.active.add_image(Image("/image.png"), "A1")
""",
            new MockLythonHost());

        Assert.False(unsupported.Success);
        Assert.Equal("NotImplementedError", unsupported.Failure?.ExceptionType);
        Assert.Contains("Worksheet.add_image", unsupported.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsTableMetadata()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook
from openpyxl.worksheet.table import Table, TableStyleInfo

wb = Workbook()
ws = wb.active
ws.append(["sku", "qty"])
ws.append(["A001", 5])
table = Table(displayName="Sales", ref="A1:B2")
table.tableStyleInfo = TableStyleInfo(name="TableStyleMedium9", showRowStripes=True)
ws.add_table(table)
found = ws.tables["Sales"]
__lython_file = open("/out.txt", "w")
__lython_file.write(found.displayName + "|" + found.ref + "|" + found.tableStyleInfo.name + "|" + str(found.tableStyleInfo.showRowStripes) + "|" + ",".join(ws.tables.keys()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Sales|A1:B2|TableStyleMedium9|True|Sales", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorkbook_SupportsCommonSheetListHelpers()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws.title = "Data"
ws["A1"] = "source"
copy = wb.copy_worksheet(ws)
inserted = wb.create_sheet("Data", -1)
inserted["A1"] = "inserted"
wb.move_sheet(copy, offset=-2)
names_before = str(wb.get_sheet_names())
indexes = str(wb.index(copy)) + "|" + str(wb.index(ws)) + "|" + str(wb.index(inserted))
copied_value = copy["A1"].value
wb.remove_sheet(inserted)
names_after_remove = str(wb.sheetnames)
del wb["Data Copy"]
__lython_file = open("/out.txt", "w")
__lython_file.write(names_before + "|" + indexes + "|" + copied_value + "|" + names_after_remove + "|" + str(wb.sheetnames))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[Data Copy, Data, Data1]|0|1|2|source|[Data Copy, Data]|[Data]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorkbook_DeleteSheetRejectsRemovingOnlyWorksheet()
    {
        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
del wb["Sheet"]
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Contains("only worksheet", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlWorkbook_SupportsActiveSheetAssignment()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
wb.active.title = "First"
second = wb.create_sheet("Second")
third = wb.create_sheet("Third")
wb.active = second
by_sheet = wb.active.title
wb.active = 2
by_index = wb.active.title
wb.save("/active.xlsx")
loaded = load_workbook("/active.xlsx")
loaded_active = loaded.active.title
wb.move_sheet(third, offset=-2)
after_move = wb.active.title + ":" + str(wb.index(wb.active))
__lython_file = open("/out.txt", "w")
__lython_file.write(by_sheet + "|" + by_index + "|" + loaded_active + "|" + after_move)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Second|Third|Third|Third:0", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorkbook_ExposesCommonMetadataProperties()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
before = [
    str(wb.named_styles),
    str(wb.style_names),
    str(wb.template),
    wb.mime_type,
    wb.epoch,
    wb.excel_base_date,
]
wb.template = True
after = [str(wb.template), wb.mime_type]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(before + after))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[Normal]|[Normal]|False|application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml|1899-12-30 00:00:00|1899-12-30 00:00:00|True|application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_ExposesRowsColumnsValuesAndDimensions()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws["B2"] = "left"
ws["D3"] = 7

all_rows = ws.rows
all_columns = ws.columns
all_values = ws.values
grid_start = all_rows[0][0].coordinate + ":" + str(all_rows[0][0].value)
occupied_row = all_rows[1][1].coordinate + ":" + str(all_rows[1][1].value)
occupied_column = all_columns[1][1].coordinate + ":" + str(all_columns[1][1].value)
occupied_value = str(all_values[1][1])

parts = [
    str(ws.min_row),
    str(ws.max_row),
    str(ws.min_column),
    str(ws.max_column),
    ws.dimensions,
    ws.calculate_dimension(),
    grid_start,
    occupied_row,
    occupied_column,
    occupied_value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("2|3|2|4|B2:D3|B2:D3|A1:None|B2:left|B2:left|left", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsRowColumnAndSliceIndexing()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws["B2"] = "b2"
ws["C2"] = "c2"
ws["B3"] = "b3"
ws["D4"] = "d4"

column_b = ws["B"]
columns_bd = ws["B:D"]
row_2 = ws[2]
rows_23 = ws[2:3]
cell_range = ws["B2":"D3"]

parts = [
    column_b[1].coordinate + ":" + str(column_b[1].value),
    columns_bd[2][3].coordinate + ":" + str(columns_bd[2][3].value),
    row_2[1].coordinate + ":" + str(row_2[1].value),
    rows_23[1][1].coordinate + ":" + str(rows_23[1][1].value),
    cell_range[0][0].coordinate + ":" + str(cell_range[0][0].value),
    cell_range[1][2].coordinate + ":" + str(cell_range[1][2].value),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("B2:b2|D4:d4|B2:b2|B3:b3|B2:b2|D3:None", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_AppendSupportsDictionaryRows()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws.append({"A": "sku", "C": "qty"})
ws.append({1: "A001", 3: 5})

parts = [
    ws["A1"].value,
    ws["C1"].value,
    ws["A2"].value,
    str(ws["C2"].value),
    str(ws.max_column),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("sku|qty|A001|5|3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsMergedCellMetadata()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "heading"
ws.merge_cells("A1:C1")
ws.merge_cells(start_row=2, start_column=1, end_row=2, end_column=2)
before = str(ws.merged_cells.ranges)
ws.unmerge_cells("A2:B2")
after = str(ws.merged_cell_ranges)
wb.save("/merged.xlsx")

loaded = load_workbook("/merged.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(before + "|" + after + "|" + str(loaded.active.merged_cells.ranges))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[A1:C1, A2:B2]|[A1:C1]|[A1:C1]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsInsertDeleteAndMoveRange()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "top"
ws["A2"] = "a2"
ws["B2"] = "b2"
ws["A3"] = "a3"
ws["C1"] = "c1"

ws.insert_rows(2)
ws.delete_rows(4)
ws.insert_cols(2)
ws.delete_cols(4)
ws.move_range("A3:C3", rows=1, cols=0)

parts = [
    ws["A1"].value,
    str(ws["A3"].value),
    ws["A4"].value,
    ws["C4"].value,
    str(ws.max_row),
    str(ws.max_column),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("top|None|a2|b2|4|3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsFreezePanesAndAutoFilterRef()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws.append(["sku", "qty", "ok"])
ws.append(["A001", 5, True])
ws.freeze_panes = "B2"
ws.auto_filter.ref = "A1:C2"
before = ws.freeze_panes + "|" + ws.auto_filter.ref
wb.save("/view.xlsx")

loaded = load_workbook("/view.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(before + "|" + loaded.active.freeze_panes + "|" + loaded.active.auto_filter.ref)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("B2|A1:C2|B2|A1:C2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsSheetViewMetadata()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["C3"] = "selected"
ws.show_gridlines = False
ws.sheet_view.tabSelected = True
ws.sheet_view.selection[0].activeCell = "C3"
ws.sheet_view.selection[0].sqref = "C3"
wb.save("/view.xlsx")

loaded = load_workbook("/view.xlsx")
view = loaded.active.sheet_view
parts = [
    str(loaded.active.show_gridlines),
    str(view.showGridLines),
    str(view.tabSelected),
    str(view.workbookViewId),
    view.selection[0].activeCell,
    view.selection[0].sqref,
    loaded.active["C3"].value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("False|False|True|0|C3|C3|selected", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_LoadsExcelAuthoredSheetViewMetadata()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-sheet-view.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx")
ws = wb["View"]
parts = [
    str(ws.show_gridlines),
    str(ws.sheet_view.showGridLines),
    str(ws.sheet_view.tabSelected),
    ws.sheet_view.selection[0].activeCell,
    ws.sheet_view.selection[0].sqref,
    ws["C3"].value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("False|False|True|C3|C3|selected", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsPrintSettings()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "sku"
ws["C5"] = "last"
ws.print_area = "A1:C5"
ws.print_title_rows = "1:2"
ws.print_title_cols = "A:B"
ws.page_margins.left = 0.25
ws.page_margins.right = 0.5
ws.page_margins.top = 0.75
ws.page_margins.bottom = 1.25
ws.page_margins.header = 0.125
ws.page_margins.footer = 0.375
ws.set_printer_settings(9, "landscape")
ws.page_setup.fitToWidth = 1
ws.page_setup.fitToHeight = 0
ws.page_setup.scale = 85
wb.save("/print.xlsx")

loaded = load_workbook("/print.xlsx")
setup = loaded.active.page_setup
margins = loaded.active.page_margins
parts = [
    loaded.active.print_area,
    loaded.active.print_title_rows,
    loaded.active.print_title_cols,
    str(margins.left),
    str(margins.right),
    str(margins.top),
    str(margins.bottom),
    str(margins.header),
    str(margins.footer),
    setup.orientation,
    str(setup.paperSize),
    str(setup.fitToWidth),
    str(setup.fitToHeight),
    str(setup.scale),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("A1:C5|1:2|A:B|0.25|0.5|0.75|1.25|0.125|0.375|landscape|9|1|0|85", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_LoadsExcelAuthoredPrintSettings()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-print-settings.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx")
ws = wb["Print"]
parts = [
    ws.print_area,
    ws.print_title_rows,
    ws.print_title_cols,
    str(ws.page_margins.left),
    str(ws.page_margins.right),
    str(ws.page_margins.top),
    str(ws.page_margins.bottom),
    str(ws.page_margins.header),
    str(ws.page_margins.footer),
    ws.page_setup.orientation,
    str(ws.page_setup.paperSize),
    ws["C5"].value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("A1:C5|1:2|A:B|0.25|0.35|0.44999999999999996|0.55|0.15|0.2|landscape|9|last", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_LoadsRichExcelAuthoredFixtureAndPreservesPackageParts()
    {
        var original = FixtureBytes("excel-rich-features.xlsx");
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", original);

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx")
ws = wb["Rich"]
parts = [
    ws.title,
    str(ws["A1"].font.bold),
    ws["B1"].fill.fgColor,
    ws["B1"].alignment.horizontal,
    ws["B1"].border.left.style,
    ws["C2"].value.isoformat(),
    ws["D2"].value,
    str(ws.merged_cell_ranges),
    str(ws.column_dimensions["A"].width),
    str(ws.row_dimensions[1].height),
    ws.freeze_panes,
    ws["A3"].hyperlink.target,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
wb.save("/copy.xlsx")
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Rich|True|FFFFFF00|center|thin|2024-01-02|=B2*2|[E1:F1]|18.77734375|24|B2|https://www.lokad.com/", host.ReadText("/out.txt"));

        var copy = host.ReadWorkbook("/copy.xlsx");
        foreach (var part in new[]
        {
            "xl/comments1.xml",
            "xl/drawings/vmlDrawing1.vml",
            "xl/drawings/drawing1.xml",
            "xl/drawings/_rels/drawing1.xml.rels",
            "xl/charts/chart1.xml",
            "xl/tables/table1.xml",
            "xl/styles.xml",
            "xl/theme/theme1.xml",
        })
        {
            Assert.Equal(WorkbookPartBytes(original, part), WorkbookPartBytes(copy, part));
        }

        var worksheetXml = WorkbookPartText(copy, "xl/worksheets/sheet1.xml");
        Assert.Contains("legacyDrawing", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("drawing", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("tableParts", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("mergeCell ref=\"E1:F1\"", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("hyperlink ref=\"A3\"", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlWorksheet_SupportsRowAndColumnDimensions()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook
from openpyxl.styles import NamedStyle

wb = Workbook()
ws = wb.active
ws["A1"] = "sku"
headline = NamedStyle(name="headline")
ws.column_dimensions["B"].width = 18.5
ws.column_dimensions["C"].hidden = True
ws.column_dimensions["D"].width = 0
ws.row_dimensions[2].height = 24.25
ws.row_dimensions[3].hidden = True
ws.column_dimensions["B"].style = headline
ws.row_dimensions[2].style = "detail"
wb.save("/dimensions.xlsx")

loaded = load_workbook("/dimensions.xlsx")
parts = [
    str(loaded.active.column_dimensions["B"].width),
    str(loaded.active.column_dimensions["C"].hidden),
    str(loaded.active.column_dimensions["D"].width),
    str(loaded.active.row_dimensions[2].height),
    str(loaded.active.row_dimensions[3].hidden),
    ws.column_dimensions["B"].style,
    ws.row_dimensions[2].style,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("18.5|True|0|24.25|True|headline|detail", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_LoadsExcelAuthoredRowAndColumnDimensions()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-dimensions.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx")
ws = wb["Dimensions"]
parts = [
    str(ws.column_dimensions["B"].width),
    str(ws.column_dimensions["C"].width),
    str(ws.column_dimensions["C"].hidden),
    str(ws.row_dimensions[2].height),
    str(ws.row_dimensions[3].hidden),
    ws["A2"].value,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("19.33203125|0|True|24.3|True|A001", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlWorksheet_MoveRangeRejectsFormulaTranslation()
    {
        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

ws = Workbook().active
ws["A1"] = "=B1"
ws.move_range("A1:A1", rows=1, cols=0, translate=True)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("translate=True", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlSave_PreservesStyledExcelAuthoredWorkbook()
    {
        var host = new MockLythonHost();
        var original = FixtureBytes("excel-basic.xlsx");
        host.SeedWorkbook("/input.xlsx", original);

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx")
wb["Inventory"]["A2"] = "A999"
wb.save("/copy.xlsx")

loaded = openpyxl.load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded["Inventory"]["A2"].value + "|" + loaded.active.title)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("A999|Second", host.ReadText("/out.txt"));
        Assert.Equal(
            WorkbookPartBytes(original, "xl/styles.xml"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "xl/styles.xml"));
        Assert.Equal(
            WorkbookPartBytes(original, "xl/theme/theme1.xml"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "xl/theme/theme1.xml"));
        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("sheetFormatPr", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("mc:Ignorable", worksheetXml, StringComparison.Ordinal);
        Assert.Contains(">A999<", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlSave_PreservesUnmodifiedPackageParts()
    {
        var host = new MockLythonHost();
        var original = WorkbookWithCustomPackagePartBytes();
        host.SeedWorkbook("/input.xlsx", original);

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx")
wb.active["A1"] = "changed"
wb.save("/copy.xlsx")

loaded = openpyxl.load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded.active["A1"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("changed", host.ReadText("/out.txt"));
        Assert.Equal(
            WorkbookPartBytes(original, "custom/item.bin"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "custom/item.bin"));
        Assert.Equal(
            WorkbookPartBytes(original, "docProps/core.xml"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "docProps/core.xml"));
        Assert.Equal(
            WorkbookPartBytes(original, "docProps/app.xml"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "docProps/app.xml"));
        Assert.Contains(
            "Extension=\"bin\" ContentType=\"application/octet-stream\"",
            WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "[Content_Types].xml"),
            StringComparison.Ordinal);
        Assert.Contains(
            "Type=\"http://example.com/root-metadata\" Target=\"custom/root.xml\"",
            WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "_rels/.rels"),
            StringComparison.Ordinal);
        Assert.Contains(
            "Type=\"http://example.com/workbook-metadata\" Target=\"custom/workbook.xml\"",
            WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/_rels/workbook.xml.rels"),
            StringComparison.Ordinal);
        var workbookXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/workbook.xml");
        Assert.Contains("filterPrivacy=\"1\"", workbookXml, StringComparison.Ordinal);
        Assert.Contains("showHorizontalScroll=\"0\"", workbookXml, StringComparison.Ordinal);
        Assert.Contains("<definedName name=\"ReportTitle\">Sheet!$A$1</definedName>", workbookXml, StringComparison.Ordinal);
        Assert.Contains("calcMode=\"manual\"", workbookXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlSave_PreservesThemeAndStyleTable()
    {
        var host = new MockLythonHost();
        var original = WorkbookWithCustomStyleTableBytes();
        host.SeedWorkbook("/styled.xlsx", original);

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/styled.xlsx")
before = str(wb.active["A1"].style_id)
wb.active["A1"] = "changed"
wb.save("/copy.xlsx")

loaded = openpyxl.load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(before + "|" + str(loaded.active["A1"].style_id) + "|" + loaded.active["A1"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1|1|changed", host.ReadText("/out.txt"));
        Assert.Equal(
            WorkbookPartBytes(original, "xl/styles.xml"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "xl/styles.xml"));
        Assert.Equal(
            WorkbookPartBytes(original, "xl/theme/theme1.xml"),
            WorkbookPartBytes(host.ReadWorkbook("/copy.xlsx"), "xl/theme/theme1.xml"));
        Assert.Contains("r=\"A1\" s=\"1\"", WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlSave_PreservesWorksheetRelatedFeatures()
    {
        var host = new MockLythonHost();
        var original = WorkbookWithWorksheetPreservedFeaturesBytes();
        host.SeedWorkbook("/features.xlsx", original);

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
wb.active["A1"] = "changed"
wb.save("/copy.xlsx")

loaded = openpyxl.load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded.active["A1"].value + "|" + loaded.active.auto_filter.ref)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("changed|A1:B2", host.ReadText("/out.txt"));
        var copy = host.ReadWorkbook("/copy.xlsx");
        foreach (var part in new[]
        {
            "xl/comments1.xml",
            "xl/tables/table1.xml",
            "xl/drawings/drawing1.xml",
            "xl/drawings/_rels/drawing1.xml.rels",
            "xl/charts/chart1.xml",
            "xl/drawings/vmlDrawing1.vml",
            "xl/media/image1.png",
        })
        {
            Assert.Equal(WorkbookPartBytes(original, part), WorkbookPartBytes(copy, part));
        }

        var worksheetXml = WorkbookPartText(copy, "xl/worksheets/sheet1.xml");
        Assert.Contains("dataValidations", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("conditionalFormatting", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("sheetProtection", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("tableParts", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("legacyDrawing", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("<drawing r:id=\"rId4\" />", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("workbookProtection", WorkbookPartText(copy, "xl/workbook.xml"), StringComparison.Ordinal);
        var worksheetRels = WorkbookPartText(copy, "xl/worksheets/_rels/sheet1.xml.rels");
        Assert.Contains("relationships/comments", worksheetRels, StringComparison.Ordinal);
        Assert.Contains("relationships/table", worksheetRels, StringComparison.Ordinal);
        Assert.Contains("relationships/drawing", worksheetRels, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_ExposesLoadedDrawingsChartsAndImages()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
ws = wb.active
drawing = ws._drawing
chart = ws._charts[0]
image = ws._images[0]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    str(len(ws._drawings)),
    str(len(ws.drawings)),
    drawing.path,
    str(len(drawing.charts)),
    drawing.charts[0].path,
    str(len(ws._charts)),
    chart.path,
    chart.drawing_path,
    str(chart.anchor is None),
    str(len(ws._images)),
    image.path,
    image.format,
    str(image.width is None),
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1|1|/xl/drawings/drawing1.xml|1|/xl/charts/chart1.xml|1|/xl/charts/chart1.xml|/xl/drawings/drawing1.xml|True|1|/xl/media/image1.png|png|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_LoadsPreservedCommentsAndMovesAnchors()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
ws = wb.active

loaded = ws["A1"].comment.text + "|" + ws["A1"].comment.author
ws.insert_rows(1)
after_insert = ws["A2"].comment.text + "|" + str(ws["A1"].comment is None)
ws.move_range("A2:A2", rows=0, cols=1)
after_move = ws["B2"].comment.author + "|" + str(ws["A2"].comment is None)
ws.delete_cols(2)
after_delete = str(ws["B2"].comment is None)

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([loaded, after_insert, after_move, after_delete]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("note|Analyst|note|True|Analyst|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_LoadsPreservedTableParts()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
ws = wb.active
table = ws.tables["Sales"]
first_value = ws.tables.values()[0]
first_item = ws.tables.items()[0]
parts = [
    table.displayName,
    table.name,
    table.ref,
    table.tableStyleInfo.name,
    str(table.tableStyleInfo.showRowStripes),
    ",".join(ws.tables.keys()),
    first_value.displayName,
    first_item[0] + ":" + first_item[1].ref,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("Sales|Sales|A1:B2|TableStyleMedium4|True|Sales|Sales|Sales:A1:B2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlTables_MoveRangesWithRowsAndColumns()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/tables.xlsx", WorkbookWithTableOnlyBytes());

        var result = new LythonEngine().Run(
            """
from openpyxl import load_workbook

wb = load_workbook("/tables.xlsx")
ws = wb.active
table = ws.tables["Sales"]
loaded = table.ref
ws.insert_rows(1)
after_insert_row = table.ref
ws.insert_cols(2)
after_insert_col = table.ref
ws.move_range("A2:C3", rows=1, cols=0)
after_move = table.ref
ws.delete_cols(1)
after_delete_col = table.ref
wb.save("/copy.xlsx")

again = load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    loaded,
    after_insert_row,
    after_insert_col,
    after_move,
    after_delete_col,
    again.active.tables["Sales"].ref,
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("A1:B2|A2:B3|A2:C3|A3:C4|A3:B4|A3:B4", host.ReadText("/out.txt"));
        var tableXml = WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/tables/table1.xml");
        Assert.Contains("ref=\"A3:B4\"", tableXml, StringComparison.Ordinal);
        Assert.Contains("<autoFilter ref=\"A3:B4\"", tableXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlStructuralEdits_RoundTripPreservedModeledWorksheetMetadata()
    {
        foreach (var item in new[]
        {
            new
            {
                Operation = "ws.insert_rows(1)",
                TableRef = "A2:B3",
                ValidationRef = "B3:B3",
                FormattingRef = "B3",
                Comment = "A2:note:Analyst",
            },
            new
            {
                Operation = "ws.delete_rows(1)",
                TableRef = "A1:B1",
                ValidationRef = "B1:B1",
                FormattingRef = "B1",
                Comment = "none",
            },
            new
            {
                Operation = "ws.insert_cols(1)",
                TableRef = "B1:C2",
                ValidationRef = "C2:C2",
                FormattingRef = "C2",
                Comment = "B1:note:Analyst",
            },
            new
            {
                Operation = "ws.delete_cols(1)",
                TableRef = "A1:A2",
                ValidationRef = "A2:A2",
                FormattingRef = "A2",
                Comment = "none",
            },
            new
            {
                Operation = "ws.move_range(\"A1:B2\", rows=1, cols=1)",
                TableRef = "B2:C3",
                ValidationRef = "C3:C3",
                FormattingRef = "C3",
                Comment = "B2:note:Analyst",
            },
        })
        {
            var original = WorkbookWithWorksheetPreservedFeaturesBytes();
            var host = new MockLythonHost();
            host.SeedWorkbook("/features.xlsx", original);

            var result = new LythonEngine().Run(
                $$"""
from openpyxl import load_workbook

wb = load_workbook("/features.xlsx")
ws = wb.active
{{item.Operation}}
wb.save("/copy.xlsx")

again = load_workbook("/copy.xlsx")
ws = again.active
comment = "none"
for ref in ["A1", "A2", "B1", "B2", "B3", "C2", "C3"]:
    if ws[ref].comment is not None:
        comment = ref + ":" + ws[ref].comment.text + ":" + ws[ref].comment.author
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    ws.tables["Sales"].ref,
    ws.data_validations.dataValidation[0].sqref,
    ws.conditional_formatting.ranges[0],
    comment,
    str(len(ws._drawings)),
    str(len(ws._charts)),
    str(len(ws._images)),
]))
__lython_file.close()
""",
                host);

            Assert.True(result.Success, item.Operation + Environment.NewLine + (result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message))));
            Assert.Equal(
                string.Join("|", item.TableRef, item.ValidationRef, item.FormattingRef, item.Comment, "1", "1", "1"),
                host.ReadText("/out.txt"));

            var copy = host.ReadWorkbook("/copy.xlsx");
            var worksheetXml = WorkbookPartText(copy, "xl/worksheets/sheet1.xml");
            Assert.Contains("sqref=\"" + item.ValidationRef + "\"", worksheetXml, StringComparison.Ordinal);
            Assert.Contains("sqref=\"" + item.FormattingRef + "\"", worksheetXml, StringComparison.Ordinal);
            Assert.Contains("tableParts", worksheetXml, StringComparison.Ordinal);
            Assert.Contains("<drawing r:id=\"rId4\" />", worksheetXml, StringComparison.Ordinal);
            Assert.Contains("ref=\"" + item.TableRef + "\"", WorkbookPartText(copy, "xl/tables/table1.xml"), StringComparison.Ordinal);
            var commentsXml = WorkbookPartText(copy, "xl/comments1.xml");
            if (item.Comment == "none")
            {
                Assert.DoesNotContain("<comment ref=", commentsXml, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("ref=\"" + item.Comment.Split(':')[0] + "\"", commentsXml, StringComparison.Ordinal);
            }

            Assert.Equal(WorkbookPartBytes(original, "xl/drawings/drawing1.xml"), WorkbookPartBytes(copy, "xl/drawings/drawing1.xml"));
            Assert.Equal(WorkbookPartBytes(original, "xl/charts/chart1.xml"), WorkbookPartBytes(copy, "xl/charts/chart1.xml"));
            Assert.Equal(WorkbookPartBytes(original, "xl/media/image1.png"), WorkbookPartBytes(copy, "xl/media/image1.png"));
        }
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_LoadsPreservedDataValidations()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
validations = wb.active.data_validations
validation = validations.dataValidation[0]
iterated = ""
for item in validations:
    iterated = item.type + ":" + item.sqref

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    str(validations.count),
    validation.type,
    validation.sqref,
    validation.formula1,
    validation.formula2,
    iterated,
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1|whole|B2:B2|0|10|whole:B2:B2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_LoadsPreservedConditionalFormatting()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
formatting = wb.active.conditional_formatting
rules = formatting["B2"]
first_item = formatting.items()[0]
parts = [
    formatting.ranges[0],
    rules[0].type,
    rules[0].operator,
    str(rules[0].priority),
    rules[0].formula[0],
    first_item[0] + ":" + first_item[1][0].type,
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("B2|cellIs|greaterThan|1|0|B2:cellIs", host.ReadText("/out.txt"));

        var mutation = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
wb.active.conditional_formatting.add("A1", None)
""",
            host);

        Assert.False(mutation.Success);
        Assert.Equal("NotImplementedError", mutation.Failure?.ExceptionType);
        Assert.Contains("ConditionalFormattingList.add", mutation.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_LoadsPreservedSheetProtection()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
protection = wb.active.protection
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    str(protection.sheet),
    str(protection.objects),
    str(protection.scenarios),
    str(protection.password is None),
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_LoadsPreservedWorkbookSecurity()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/features.xlsx", WorkbookWithWorksheetPreservedFeaturesBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
security = wb.security
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    str(security.lockStructure),
    str(security.lockWindows),
    str(security.lockRevision),
    security.workbookPassword,
    security.workbookPasswordCharacterSet,
    security.revisionsPassword,
    str(security.workbookSpinCount),
    security.workbookAlgorithmName,
    security.workbookHashValue,
    security.workbookSaltValue,
    str(security.revisionsSpinCount),
    security.revisionsAlgorithmName,
    security.revisionsHashValue,
    security.revisionsSaltValue,
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|False|True|ABCD|UTF-8|DCBA|2|SHA-512|HASH|SALT|3|SHA-512|RHASH|RSALT", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlSheetProtection_CreatesSavesAndReloads()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
ws = wb.active
ws["A1"] = "locked"
ws.protection.enable()
ws.protection.objects = True
ws.protection.scenarios = True
ws.protection.password = "ABCD"
ws.protection.spinCount = 2
wb.save("/protected.xlsx")

loaded = load_workbook("/protected.xlsx")
protection = loaded.active.protection
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    str(protection.sheet),
    str(protection.objects),
    str(protection.scenarios),
    protection.password,
    str(protection.spinCount),
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|True|ABCD|2", host.ReadText("/out.txt"));
        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/protected.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("<sheetProtection", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("password=\"ABCD\"", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlWorkbookSecurity_CreatesSavesAndReloads()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook

wb = Workbook()
security = wb.security
security.lockStructure = True
security.lockWindows = False
security.lockRevision = True
security.workbookPasswordCharacterSet = "UTF-8"
security.set_workbook_password("ABCD", already_hashed=True)
security.set_revisions_password("DCBA", already_hashed=True)
security.workbookSpinCount = 4
security.revisionsAlgorithmName = "SHA-512"
security.revisionsHashValue = "RHASH"
security.revisionsSaltValue = "RSALT"
security.revisionsSpinCount = 5
wb.save("/protected.xlsx")

loaded = load_workbook("/protected.xlsx")
security = loaded.security
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    str(security.lockStructure),
    str(security.lockWindows),
    str(security.lockRevision),
    security.workbookPassword,
    security.workbookPasswordCharacterSet,
    security.revisionsPassword,
    str(security.workbookSpinCount),
    security.revisionsAlgorithmName,
    security.revisionsHashValue,
    security.revisionsSaltValue,
    str(security.revisionsSpinCount),
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|False|True|ABCD|UTF-8|DCBA|4|SHA-512|RHASH|RSALT|5", host.ReadText("/out.txt"));

        var workbookXml = WorkbookPartText(host.ReadWorkbook("/protected.xlsx"), "xl/workbook.xml");
        Assert.Contains("workbookProtection", workbookXml, StringComparison.Ordinal);
        Assert.Contains("lockWindows=\"0\"", workbookXml, StringComparison.Ordinal);
        Assert.Contains("workbookPassword=\"ABCD\"", workbookXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlDataValidation_CreatesSavesAndReloads()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook
from openpyxl.worksheet.datavalidation import DataValidation

wb = Workbook()
ws = wb.active
ws["A1"] = "choice"
validation = DataValidation(type="list", formula1='"A,B"', allow_blank=True)
validation.errorTitle = "Invalid"
validation.add("A1:A3")
ws.add_data_validation(validation)
wb.save("/validation.xlsx")

loaded = load_workbook("/validation.xlsx")
loaded_validation = loaded.active.data_validations.dataValidation[0]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([
    loaded_validation.type,
    loaded_validation.formula1,
    loaded_validation.sqref,
    str(loaded_validation.allow_blank),
    loaded_validation.errorTitle,
]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("list|\"A,B\"|A1:A3|True|Invalid", host.ReadText("/out.txt"));
        var worksheetXml = WorkbookPartText(host.ReadWorkbook("/validation.xlsx"), "xl/worksheets/sheet1.xml");
        Assert.Contains("<dataValidations count=\"1\">", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("type=\"list\"", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("allowBlank=\"1\"", worksheetXml, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlDataValidation_RangesFollowStructuralEditsAfterLoad()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook, load_workbook
from openpyxl.worksheet.datavalidation import DataValidation

wb = Workbook()
ws = wb.active
validation = DataValidation(type="whole", formula1="0", formula2="10")
validation.add("B2:B3")
ws.add_data_validation(validation)
wb.save("/input.xlsx")

loaded = load_workbook("/input.xlsx")
ws = loaded.active
ws.insert_rows(2)
ws.insert_cols(2)
ws.move_range("C3:C4", rows=1, cols=0)
ws.delete_cols(1)
edited = ws.data_validations.dataValidation[0].sqref
loaded.save("/copy.xlsx")

again = load_workbook("/copy.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(edited + "|" + again.active.data_validations.dataValidation[0].sqref)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("B4:B5|B4:B5", host.ReadText("/out.txt"));
        Assert.Contains("sqref=\"B4:B5\"", WorkbookPartText(host.ReadWorkbook("/copy.xlsx"), "xl/worksheets/sheet1.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlSave_RejectsStructuralEditsThatWouldStalePreservedWorksheetFeatures()
    {
        foreach (var (operation, label) in new[]
        {
            ("ws.insert_rows(1)", "insert_rows"),
            ("ws.delete_rows(1)", "delete_rows"),
            ("ws.insert_cols(1)", "insert_cols"),
            ("ws.delete_cols(1)", "delete_cols"),
            ("ws.move_range(\"A1:B2\", rows=1, cols=1)", "move_range"),
        })
        {
            var host = new MockLythonHost();
            host.SeedWorkbook(
                "/features.xlsx",
                WorkbookWithWorksheetPreservedFeaturesBytes(
                    """
<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:ext cx="0" cy="0" /><xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="2" name="Chart 1" /><xdr:cNvGraphicFramePr /></xdr:nvGraphicFramePr><xdr:xfrm /><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart r:id="rId1" /></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData /></xdr:oneCellAnchor></xdr:wsDr>
""",
                    """
<xml xmlns:v="urn:schemas-microsoft-com:vml" xmlns:x="urn:schemas-microsoft-com:office:excel"><v:shape id="_x0000_s1025"><x:ClientData ObjectType="Note"><x:Row>0</x:Row><x:Column>0</x:Column></x:ClientData></v:shape></xml>
"""));

            var result = new LythonEngine().Run(
                $$"""
import openpyxl

wb = openpyxl.load_workbook("/features.xlsx")
ws = wb.active
{{operation}}
wb.save("/copy.xlsx")
""",
                host);

            Assert.False(result.Success, label + " should fail before writing stale preserved worksheet metadata.");
            Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
            Assert.Contains("structural edits", result.Failure?.Message, StringComparison.Ordinal);
            Assert.Contains("drawings/images/charts", result.Failure?.Message, StringComparison.Ordinal);
            Assert.False(host.Exists("/copy.xlsx"));
        }
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_AcceptsKeepLinksFalseWithoutExternalLinks()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", keep_links=False)
__lython_file = open("/out.txt", "w")
__lython_file.write(wb["Inventory"]["A2"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("A001", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_RejectsKeepLinksFalseWhenExternalLinksWouldBeDropped()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/linked.xlsx", WorkbookWithExternalLinkBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

openpyxl.load_workbook("/linked.xlsx", keep_links=False)
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("keep_links=False", result.Failure?.Message, StringComparison.Ordinal);
        Assert.Contains("external workbook links", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_AcceptsExplicitKeepVbaOption()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", keep_vba=True)
__lython_file = open("/out.txt", "w")
__lython_file.write(wb["Inventory"]["A2"].value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("A001", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlSave_RejectsMacroWorkbookWithoutKeepVbaBeforeWritingOutput()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/macro.xlsm", WorkbookWithVbaProjectBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/macro.xlsm")
wb.active["A1"] = "changed"
wb.save("/copy.xlsm")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("VBA project", result.Failure?.Message, StringComparison.Ordinal);
        Assert.Contains("keep_vba=True", result.Failure?.Message, StringComparison.Ordinal);
        Assert.False(host.Exists("/copy.xlsm"));
    }

    [Fact]
    public void OpenPyxlSave_PreservesVbaProjectWithKeepVba()
    {
        var host = new MockLythonHost();
        var original = WorkbookWithVbaProjectBytes();
        host.SeedWorkbook("/macro.xlsm", original);

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/macro.xlsm", keep_vba=True)
wb.active["A1"] = "changed"
wb.save("/copy.xlsm")

loaded = openpyxl.load_workbook("/copy.xlsm", keep_vba=True)
__lython_file = open("/out.txt", "w")
__lython_file.write(loaded.active["A1"].value + "|" + loaded.mime_type)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("changed|application/vnd.ms-excel.sheet.macroEnabled.main+xml", host.ReadText("/out.txt"));
        var copy = host.ReadWorkbook("/copy.xlsm");
        Assert.Equal(WorkbookPartBytes(original, "xl/vbaProject.bin"), WorkbookPartBytes(copy, "xl/vbaProject.bin"));
        Assert.Contains("relationships/vbaProject", WorkbookPartText(copy, "xl/_rels/workbook.xml.rels"), StringComparison.Ordinal);
        var contentTypes = WorkbookPartText(copy, "[Content_Types].xml");
        Assert.Contains("macroEnabled.main+xml", contentTypes, StringComparison.Ordinal);
        Assert.Contains("/xl/vbaProject.bin", contentTypes, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlSave_RejectsExternalLinkWorkbookBeforeWritingOutput()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/linked.xlsx", WorkbookWithExternalLinkBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/linked.xlsx")
wb.active["A1"] = "changed"
wb.save("/copy.xlsx")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("external link", result.Failure?.Message, StringComparison.Ordinal);
        Assert.False(host.Exists("/copy.xlsx"));
    }

    [Fact]
    public void OpenPyxlSave_RejectsEmbeddedObjectWorkbookBeforeWritingOutput()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/embedded.xlsx", WorkbookWithEmbeddedObjectBytes());

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/embedded.xlsx")
wb.active["A1"] = "changed"
wb.save("/copy.xlsx")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("OLE objects", result.Failure?.Message, StringComparison.Ordinal);
        Assert.False(host.Exists("/copy.xlsx"));
    }

    [Fact]
    public void OpenPyxlLoadWorkbook_RejectsNonPathWorkbookInput()
    {
        var result = new LythonEngine().Run(
            """
import openpyxl

openpyxl.load_workbook(123)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("filename must be a string or pathlib.Path", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_AllowsCellReads()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
ws = wb["Inventory"]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(wb.read_only) + "|" + ws["A2"].value + "|" + str(ws["C2"].value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|A001|5", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_RejectsWorksheetMutation()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
wb["Inventory"]["A2"] = "A999"
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("read-only", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_RejectsIterCols()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
wb.active.iter_cols(values_only=True)
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("iter_cols", result.Failure?.Message, StringComparison.Ordinal);
        Assert.Contains("read-only", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_RejectsDimensionMutation()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
wb.active.column_dimensions["B"].width = 12
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("read-only", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_RejectsViewMutation()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
wb.active.sheet_view.selection[0].activeCell = "C3"
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("read-only", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_RejectsPrintSettingsMutation()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
wb.active.page_margins.left = 0.25
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("read-only", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPyxlReadOnlyWorkbook_RejectsSave()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/input.xlsx", FixtureBytes("excel-basic.xlsx"));

        var result = new LythonEngine().Run(
            """
import openpyxl

wb = openpyxl.load_workbook("/input.xlsx", read_only=True)
wb.save("/copy.xlsx")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("read-only", result.Failure?.Message, StringComparison.Ordinal);
        Assert.False(host.Exists("/copy.xlsx"));
    }

    [Fact]
    public void OpenPyxlWriteOnlyWorkbook_EnforcesSaveOnce()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import os
import openpyxl

wb = openpyxl.Workbook(write_only=True)
wb.active.append(["sku", "qty"])
wb.save("/first.xlsx")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(wb.write_only) + "|" + str(os.path.exists("/first.xlsx")))
__lython_file.close()
wb.save("/second.xlsx")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("WorkbookAlreadySaved", result.Failure?.ExceptionType);
        Assert.Contains("already been saved", result.Failure?.Message, StringComparison.Ordinal);
        Assert.Equal("True|True", host.ReadText("/out.txt"));
        Assert.False(host.Exists("/second.xlsx"));
    }

    [Fact]
    public void OpenPyxlSave_FailsAtRuntimeWhenBinaryHostIoIsUnavailable()
    {
        var result = new LythonEngine().Run(
            """
import openpyxl
wb = openpyxl.Workbook()
wb.save("/out.xlsx")
""",
            new TextOnlyHost());

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("host binary file I/O is not available", result.Failure?.Message, StringComparison.Ordinal);
    }

    private static byte[] FixtureBytes(string name)
    {
        var root = FindOpenPyxlFixturesRoot();
        var directPath = Path.Combine(root, name);
        if (File.Exists(directPath))
        {
            return File.ReadAllBytes(directPath);
        }

        var caseId = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        if (extension.Length == 0)
        {
            extension = ".xlsx";
        }

        var caseInputPath = Path.Combine(root, "cases", caseId, "input" + extension);
        if (File.Exists(caseInputPath))
        {
            return File.ReadAllBytes(caseInputPath);
        }

        throw new FileNotFoundException("Could not locate openpyxl fixture '" + name + "'.", name);
    }

    private static string WorkbookPartText(byte[] payload, string path)
    {
        using var stream = new MemoryStream(payload);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException("Workbook part not found: " + path);
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        return reader.ReadToEnd();
    }

    private static byte[] WorkbookPartBytes(byte[] payload, string path)
    {
        using var stream = new MemoryStream(payload);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException("Workbook part not found: " + path);
        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static bool WorkbookHasPart(byte[] payload, string path)
    {
        using var stream = new MemoryStream(payload);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        return archive.GetEntry(path) is not null;
    }

    private static byte[] WorkbookWithExternalLinkBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/externalLinks/externalLink1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.externalLink+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets><externalReferences><externalReference r:id="rId2" /></externalReferences></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink" Target="externalLinks/externalLink1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>original</t></is></c></row></sheetData></worksheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/externalLinks/externalLink1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<externalLink xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><externalBook r:id="rId1"><sheetNames><sheetName val="Source" /></sheetNames></externalBook></externalLink>
""");
            WriteWorkbookPart(
                archive,
                "xl/externalLinks/_rels/externalLink1.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLinkPath" Target="https://example.com/source.xlsx" TargetMode="External" /></Relationships>
""");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithVbaProjectBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.ms-excel.sheet.macroEnabled.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/vbaProject.bin" ContentType="application/vnd.ms-office.vbaProject" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.microsoft.com/office/2006/relationships/vbaProject" Target="vbaProject.bin" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>original</t></is></c></row></sheetData></worksheet>
""");
            WriteWorkbookPart(archive, "xl/vbaProject.bin", "fake-vba-project");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithEmbeddedObjectBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Default Extension="bin" ContentType="application/vnd.openxmlformats-officedocument.oleObject" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>original</t></is></c></row></sheetData><oleObjects><oleObject progId="Package" shapeId="1025" r:id="rId1" /></oleObjects></worksheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/_rels/sheet1.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/oleObject" Target="../embeddings/oleObject1.bin" /></Relationships>
""");
            WriteWorkbookPart(archive, "xl/embeddings/oleObject1.bin", "embedded-payload");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithCustomPackagePartBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Default Extension="bin" ContentType="application/octet-stream" /><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml" /><Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml" /><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml" /><Relationship Id="rId8" Type="http://example.com/root-metadata" Target="custom/root.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><workbookPr filterPrivacy="1" /><bookViews><workbookView activeTab="0" showHorizontalScroll="0" /></bookViews><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets><definedNames><definedName name="ReportTitle">Sheet!$A$1</definedName></definedNames><calcPr calcMode="manual" /></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId9" Type="http://example.com/workbook-metadata" Target="custom/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>original</t></is></c></row></sheetData></worksheet>
""");
            WriteWorkbookPart(archive, "custom/item.bin", "\u0000custom-payload\u0001");
            WriteWorkbookPart(archive, "custom/root.xml", "<root-metadata />");
            WriteWorkbookPart(archive, "xl/custom/workbook.xml", "<workbook-metadata />");
            WriteWorkbookPart(archive, "docProps/core.xml", "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\"><cp:revision>7</cp:revision></cp:coreProperties>");
            WriteWorkbookPart(archive, "docProps/app.xml", "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"><Application>Excel</Application></Properties>");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithCustomStyleTableBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" /><Override PartName="/xl/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml" /><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="theme/theme1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/styles.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11" /><name val="Calibri" /></font><font><b /><color rgb="FFFF0000" /><sz val="12" /><name val="Calibri" /></font></fonts><fills count="2"><fill><patternFill patternType="none" /></fill><fill><patternFill patternType="gray125" /></fill></fills><borders count="1"><border><left /><right /><top /><bottom /><diagonal /></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" /></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" /><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" /></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0" /></cellStyles></styleSheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/theme/theme1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Custom Theme"><a:themeElements /></a:theme>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" s="1" t="inlineStr"><is><t>original</t></is></c></row></sheetData></worksheet>
""");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithNamedStyleBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/styles.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="0.00" /></numFmts><fonts count="2"><font><sz val="11" /><name val="Calibri" /></font><font><b /><color rgb="FFFF0000" /><sz val="12" /><name val="Calibri" /></font></fonts><fills count="3"><fill><patternFill patternType="none" /></fill><fill><patternFill patternType="gray125" /></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFFF00" /></patternFill></fill></fills><borders count="2"><border><left /><right /><top /><bottom /><diagonal /></border><border><left style="thin"><color rgb="FF00FF00" /></left><right /><top /><bottom /><diagonal /></border></borders><cellStyleXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" /><xf numFmtId="164" fontId="1" fillId="2" borderId="1" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1" /></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" /><xf numFmtId="164" fontId="1" fillId="2" borderId="1" xfId="1" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1" /></cellXfs><cellStyles count="2"><cellStyle name="Normal" xfId="0" builtinId="0" /><cellStyle name="Headline" xfId="1" /></cellStyles></styleSheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" s="1" t="inlineStr"><is><t>styled</t></is></c></row></sheetData></worksheet>
""");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithWorksheetPreservedFeaturesBytes(string? drawingXml = null, string? vmlDrawingXml = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Default Extension="vml" ContentType="application/vnd.openxmlformats-officedocument.vmlDrawing" /><Default Extension="png" ContentType="image/png" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/comments1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml" /><Override PartName="/xl/tables/table1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml" /><Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml" /><Override PartName="/xl/charts/chart1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><workbookProtection lockStructure="1" lockWindows="0" lockRevision="1" workbookPassword="ABCD" workbookPasswordCharacterSet="UTF-8" revisionsPassword="DCBA" workbookSpinCount="2" workbookAlgorithmName="SHA-512" workbookHashValue="HASH" workbookSaltValue="SALT" revisionsSpinCount="3" revisionsAlgorithmName="SHA-512" revisionsHashValue="RHASH" revisionsSaltValue="RSALT" /><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><dimension ref="A1:B2" /><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>original</t></is></c><c r="B1" t="inlineStr"><is><t>qty</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>A001</t></is></c><c r="B2"><v>5</v></c></row></sheetData><autoFilter ref="A1:B2" /><dataValidations count="1"><dataValidation type="whole" sqref="B2"><formula1>0</formula1><formula2>10</formula2></dataValidation></dataValidations><conditionalFormatting sqref="B2"><cfRule type="cellIs" priority="1" operator="greaterThan"><formula>0</formula></cfRule></conditionalFormatting><sheetProtection sheet="1" objects="1" scenarios="1" /><legacyDrawing r:id="rId3" /><drawing r:id="rId4" /><tableParts count="1"><tablePart r:id="rId1" /></tableParts></worksheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/_rels/sheet1.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/table" Target="../tables/table1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments" Target="../comments1.xml" /><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/vmlDrawing" Target="../drawings/vmlDrawing1.vml" /><Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml" /></Relationships>
""");
            WriteWorkbookPart(archive, "xl/comments1.xml", "<comments xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><authors><author>Analyst</author></authors><commentList><comment ref=\"A1\" authorId=\"0\"><text><t>note</t></text></comment></commentList></comments>");
            WriteWorkbookPart(archive, "xl/tables/table1.xml", "<table xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" id=\"1\" name=\"Sales\" displayName=\"Sales\" ref=\"A1:B2\"><autoFilter ref=\"A1:B2\" /><tableColumns count=\"2\"><tableColumn id=\"1\" name=\"sku\" /><tableColumn id=\"2\" name=\"qty\" /></tableColumns><tableStyleInfo name=\"TableStyleMedium4\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\" /></table>");
            WriteWorkbookPart(archive, "xl/drawings/drawing1.xml", drawingXml ?? "<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" />");
            WriteWorkbookPart(archive, "xl/drawings/_rels/drawing1.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart\" Target=\"../charts/chart1.xml\" /><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/image1.png\" /></Relationships>");
            WriteWorkbookPart(archive, "xl/charts/chart1.xml", "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart /></c:chartSpace>");
            WriteWorkbookPart(archive, "xl/drawings/vmlDrawing1.vml", vmlDrawingXml ?? "<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" />");
            WriteWorkbookPart(archive, "xl/media/image1.png", "not-a-real-png-but-preserved");
        }

        return stream.ToArray();
    }

    private static byte[] WorkbookWithTableOnlyBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/tables/table1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><dimension ref="A1:B2" /><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>sku</t></is></c><c r="B1" t="inlineStr"><is><t>qty</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>A001</t></is></c><c r="B2"><v>5</v></c></row></sheetData><tableParts count="1"><tablePart r:id="rId1" /></tableParts></worksheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/_rels/sheet1.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/table" Target="../tables/table1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/tables/table1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<table xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" id="1" name="Sales" displayName="Sales" ref="A1:B2"><autoFilter ref="A1:B2" /><tableColumns count="2"><tableColumn id="1" name="sku" /><tableColumn id="2" name="qty" /></tableColumns><tableStyleInfo name="TableStyleMedium4" showFirstColumn="0" showLastColumn="0" showRowStripes="1" showColumnStripes="0" /></table>
""");
        }

        return stream.ToArray();
    }

    private static byte[] Date1904WorkbookBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><workbookPr date1904="1" /><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/styles.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11" /><color theme="1" /><name val="Calibri" /><family val="2" /><scheme val="minor" /></font></fonts><fills count="2"><fill><patternFill patternType="none" /></fill><fill><patternFill patternType="gray125" /></fill></fills><borders count="1"><border><left /><right /><top /><bottom /><diagonal /></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" /></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" /><xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" /></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0" /></cellStyles></styleSheet>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" s="1"><v>1</v></c></row></sheetData></worksheet>
""");
        }

        return stream.ToArray();
    }

    private static byte[] CachedFormulaWorkbookBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:B1" /><sheetData><row r="1"><c r="A1"><v>2</v></c><c r="B1"><f>A1*3</f><v>6</v></c></row></sheetData></worksheet>
""");
        }

        return stream.ToArray();
    }

    private static byte[] SharedAndArrayFormulaWorkbookBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:C2" /><sheetData><row r="1"><c r="A1"><v>2</v></c><c r="B1"><f t="shared" ref="B1:B2" si="0">A1*2</f><v>4</v></c><c r="C1"><f t="array" ref="C1:C2">A1:A2*2</f><v>4</v></c></row><row r="2"><c r="A2"><v>3</v></c><c r="B2"><f t="shared" si="0" /><v>6</v></c><c r="C2"><v>6</v></c></row></sheetData></worksheet>
""");
        }

        return stream.ToArray();
    }

    private static byte[] RichTextWorkbookBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteWorkbookPart(
                archive,
                "[Content_Types].xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml" /></Types>
""");
            WriteWorkbookPart(
                archive,
                "_rels/.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/workbook.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet" sheetId="1" r:id="rId1" /></sheets></workbook>
""");
            WriteWorkbookPart(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml" /></Relationships>
""");
            WriteWorkbookPart(
                archive,
                "xl/sharedStrings.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="1" uniqueCount="1"><si><r><rPr><b /></rPr><t>rich</t></r><r><t xml:space="preserve"> text</t></r></si></sst>
""");
            WriteWorkbookPart(
                archive,
                "xl/worksheets/sheet1.xml",
                """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:A1" /><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c></row></sheetData></worksheet>
""");
        }

        return stream.ToArray();
    }

    private static void WriteWorkbookPart(System.IO.Compression.ZipArchive archive, string path, string text)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private static string FindOpenPyxlFixturesRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Fixtures", "openpyxl");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Fixtures/openpyxl.");
    }

    private sealed class TextOnlyHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => _inner.ReadTextUtf8Async(path, cancellationToken);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.ExistsAsync(path, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }
}
