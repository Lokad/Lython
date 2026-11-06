using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OpenPyxlNumberFormatTests
{
    // Numeric types/values and is_date goldens checked with openpyxl 3.1.5.
    private static readonly string[] NumericFormats =
    [
        "0.00", "0.00;[Red]-0.00",
        "[Black]0.00", "[Blue]0.00", "[Cyan]0.00", "[Green]0.00",
        "[Magenta]0.00", "[Red]0.00", "[White]0.00", "[Yellow]0.00",
        "[>=1][Red]0.00;[Blue]-0.00", "[$USD-409]0.00",
        "0.00\"days hours months\"", "0.00\\d\\h\\m\\s\\y", "0.00_d_h_m_s_y",
        "0.00;yyyy-mm-dd", "0.00;[hh]:mm:ss", "0.00\"[hh]\"",
        "[Magenta]0.00;[h]:mm", "[Color10]0.00",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumericFormatsPreserveNumbersAndFormulaCaches(bool asynchronous)
    {
        var host = new MockLythonHost();
        var engine = new LythonEngine(); // Format detection needs no regex provider.
        using (var created = engine.Run(
            $$"""
            from openpyxl import Workbook
            book = Workbook()
            sheet = book.active
            row = 0
            for fmt in {{JsonSerializer.Serialize(NumericFormats)}}:
                for value in [1.56, -1.56, 0]:
                    row += 1
                    sheet.cell(row, 1, value).number_format = fmt
                    sheet.cell(row, 2, "=A" + str(row)).number_format = fmt
                    assert not sheet.cell(row, 1).is_date, fmt
            book.save("/formats.xlsx")
            """, host))
            Assert.True(created.Success, created.Failure?.Message);

        // Spreadsheet producers supply cached results; Lython does not calculate
        // formulas. Put the corresponding numeric value into each formula cache.
        host.SeedWorkbook("/formats.xlsx", AddFormulaCaches(host.ReadWorkbook("/formats.xlsx")));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = engine.Compile(
            $$"""
            from openpyxl import load_workbook
            book = load_workbook("/formats.xlsx", data_only=True)
            formulas = load_workbook("/formats.xlsx", data_only=False)
            row = 0
            for fmt in {{JsonSerializer.Serialize(NumericFormats)}}:
                for value in [1.56, -1.56, 0]:
                    row += 1
                    for column in [1, 2]:
                        cell = book.active.cell(row, column)
                        assert cell.value == value, fmt
                        assert type(cell.value) is type(value), fmt
                        assert cell.number_format == fmt, fmt
                        assert not cell.is_date, fmt
                        assert cell.data_type == "n", fmt
                    assert formulas.active.cell(row, 2).value == "=A" + str(row)
            return row
            """);
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(NumericFormats.Length * 3), result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactElapsedTokensAndRealDateTimeFormatsRemainRecognized(bool asynchronous)
    {
        var host = new MockLythonHost();
        using var script = new LythonEngine().Compile(
            """
            from openpyxl import Workbook, load_workbook
            from datetime import timedelta, date, time, datetime
            formats = ["[h]", "[hh]:mm:ss", "[m]", "[mm]:ss", "[s]", "[ss].000",
                       "[Red]yyyy-mm-dd", "[Blue]h:mm:ss", "[Yellow]yyyy-mm-dd h:mm:ss"]
            book = Workbook()
            for row, fmt in enumerate(formats, 1):
                cell = book.active.cell(row, 1, 1.5)
                cell.number_format = fmt
                assert cell.is_date, fmt
            book.save("/dates.xlsx")
            loaded = load_workbook("/dates.xlsx")
            for row in range(1, 7):
                cell = loaded.active.cell(row, 1)
                assert isinstance(cell.value, timedelta)
                assert cell.value.total_seconds() == 129600
            assert isinstance(loaded.active.cell(7, 1).value, date)
            assert loaded.active.cell(8, 1).value == time(12, 0)
            assert isinstance(loaded.active.cell(9, 1).value, datetime)
            return True
            """);
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
    }

    private static byte[] AddFormulaCaches(byte[] workbook)
    {
        using var stream = new MemoryStream();
        stream.Write(workbook);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
            XDocument document;
            using (var content = entry.Open()) document = XDocument.Load(content);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var row in document.Descendants(ns + "row"))
            {
                var cells = row.Elements(ns + "c").ToArray();
                cells[1].SetElementValue(ns + "v", cells[0].Element(ns + "v")!.Value);
            }
            entry.Delete();
            using var replacement = archive.CreateEntry("xl/worksheets/sheet1.xml").Open();
            document.Save(replacement);
        }
        return stream.ToArray();
    }
}
