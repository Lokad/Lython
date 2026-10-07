using System.Numerics;
using System.IO.Compression;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R02: directory gates fire before BCL materialization, small sheets pay their
/// cell tail, and loads stay within budget in both execution modes.
/// </summary>
public sealed class OpenPyxlAccountingScenarioTests
{
    [Fact]
    public async Task ManyTinyEntriesGateBeforeBclMaterialization()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run(
            """
            import zipfile
            with zipfile.ZipFile("/t.zip", "w") as archive:
                for i in range(2000):
                    archive.writestr("f" + str(i), b"")
            return 1
            """,
            seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.zip");

        var script = new LythonEngine().Compile(
            """
            import openpyxl
            return openpyxl.load_workbook("/t.xlsx")
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxCollectionSize = 100 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.xlsx", archive);
        var sync = script.Run(syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.xlsx", archive);
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmallSheetTailCharge()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            i = 1
            while i <= 63:
                ws.cell(row=i, column=1, value=i)
                i = i + 1
            wb.save("/t.xlsx")
            return 1
            """,
            seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.xlsx");

        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.load_workbook("/t.xlsx")
            ws = wb.active
            return [ws["A1"].value, ws.cell(row=63, column=1).value, ws.max_row]
            """);
        Assert.True(script.IsValid);
        // Calibration: the flip point for this 63-cell file sits just above
        // 48KiB (fails) and at or below 56KiB (passes); the 32KiB tail is what
        // pushes the new peak over, while the old peak stays ~32KiB lower.
        var tight = new LythonRunOptions { MaxExecutionMemoryBytes = 49152 };
        var tightHost = new MockLythonHost();
        tightHost.SeedBytes("/t.xlsx", archive);
        var denied = script.Run(tightHost, tight);
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);

        var roomy = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var roomyHost = new MockLythonHost();
        roomyHost.SeedBytes("/t.xlsx", archive);
        var sync = script.Run(roomyHost, roomy);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new BigInteger(63), new BigInteger(63) },
            Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.xlsx", archive);
        var asyncResult = await script.RunAsync(asyncHost, roomy);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new BigInteger(63), new BigInteger(63) },
            Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task DeepXmlNestingIsRejectedOnTheSingleParse()
    {
        // R02: depth validation rides the same reader that builds the DOM, so
        // a hostile part fails during the one parse instead of a pre-scan.
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            wb.save("/t.xlsx")
            return 1
            """,
            seed);
        Assert.True(built.Success, built.Failure?.Message);
        var mangled = ReplacePackagePart(seed.ReadBytes("/t.xlsx"), "xl/workbook.xml", DeepDocument(1100));

        var script = new LythonEngine().Compile(
            """
            import openpyxl
            return openpyxl.load_workbook("/deep.xlsx")
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/deep.xlsx", mangled);
        var sync = script.Run(syncHost);
        Assert.False(sync.Success);
        Assert.Equal("InvalidFileException", sync.Failure?.ExceptionType);
        Assert.Contains("nesting", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/deep.xlsx", mangled);
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.False(asyncResult.Success);
        Assert.Equal("InvalidFileException", asyncResult.Failure?.ExceptionType);
        Assert.Contains("nesting", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("<?xml version=\"1.0\"?>", false)]
    [InlineData("<?xml version=\"1.0\"?>", true)]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", false)]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", true)]
    [InlineData("<?xml version=\"1.0\" standalone=\"yes\"?>", false)]
    [InlineData("<?xml version=\"1.0\" standalone=\"yes\"?>", true)]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>", false)]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>", true)]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>", false)]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>", true)]
    public async Task OptionalXmlDeclarationAttributesLoad(string declaration, bool readOnly)
    {
        var bytes = XmlDeclarationWorkbook(declaration);
        var script = new LythonEngine().Compile(
            "import openpyxl\n" +
            "wb = openpyxl.load_workbook('/declaration.xlsx', data_only=True, read_only=" +
            (readOnly ? "True" : "False") + ")\nprint(list(wb.active.values))");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        const string expected = "[('name', 'value'), ('alpha', 2)]\n";

        var host = new MockLythonHost();
        host.SeedBytes("/declaration.xlsx", bytes);
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.StandardOutput);

        var delayed = new DelayedLythonHost();
        delayed.SeedBytes("/declaration.xlsx", bytes);
        var asyncResult = await script.RunAsync(delayed, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("<?xml version=\"1.0\"")]
    [InlineData("<!DOCTYPE Relationships SYSTEM \"file:///unavailable.dtd\">")]
    public async Task InvalidOrProhibitedPackageXmlFailsExplicitly(string prefix)
    {
        var bytes = XmlDeclarationWorkbook(prefix);
        var script = new LythonEngine().Compile(
            "import openpyxl\nopenpyxl.load_workbook('/invalid.xlsx', read_only=True)");
        Assert.True(script.IsValid);
        var host = new MockLythonHost();
        host.SeedBytes("/invalid.xlsx", bytes);
        var delayed = new DelayedLythonHost();
        delayed.SeedBytes("/invalid.xlsx", bytes);

        foreach (var result in new[] { script.Run(host), await script.RunAsync(delayed) })
        {
            Assert.False(result.Success);
            Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
            Assert.Contains("Invalid .xlsx workbook:", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("internal Lython invariant", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        }
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    // A public, synthetic two-row package isolates declaration handling from
    // workbook authoring tools. Every XML part uses the selected declaration.
    private static byte[] XmlDeclarationWorkbook(string declaration)
    {
        const string spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string documentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var parts = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] =
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "</Types>",
            ["_rels/.rels"] =
                $"<Relationships xmlns=\"{packageRelationships}\">" +
                $"<Relationship Id=\"rId1\" Type=\"{documentRelationships}/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>",
            ["xl/workbook.xml"] =
                $"<workbook xmlns=\"{spreadsheet}\" xmlns:r=\"{documentRelationships}\">" +
                "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] =
                $"<Relationships xmlns=\"{packageRelationships}\">" +
                $"<Relationship Id=\"rId1\" Type=\"{documentRelationships}/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "</Relationships>",
            ["xl/worksheets/sheet1.xml"] =
                $"<worksheet xmlns=\"{spreadsheet}\"><dimension ref=\"A1:B2\"/><sheetData>" +
                "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>name</t></is></c>" +
                "<c r=\"B1\" t=\"inlineStr\"><is><t>value</t></is></c></row>" +
                "<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>alpha</t></is></c>" +
                "<c r=\"B2\"><v>2</v></c></row></sheetData></worksheet>",
        };
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in parts)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(declaration);
                writer.Write(text);
            }
        }
        return output.ToArray();
    }

    private static string DeepDocument(int depth)
    {
        var builder = new StringBuilder("<root>");
        for (var i = 0; i < depth; i++)
        {
            builder.Append("<x>");
        }

        for (var i = 0; i < depth; i++)
        {
            builder.Append("</x>");
        }

        builder.Append("</root>");
        return builder.ToString();
    }

    private static byte[] ReplacePackagePart(byte[] package, string path, string content)
    {
        using var input = new MemoryStream(package, writable: false);
        using var output = new MemoryStream();
        using (var reader = new ZipArchive(input, ZipArchiveMode.Read))
        using (var writer = new ZipArchive(output, ZipArchiveMode.Create))
        {
            foreach (var entry in reader.Entries)
            {
                var target = writer.CreateEntry(entry.FullName);
                using var destination = target.Open();
                if (entry.FullName == path)
                {
                    var replacement = Encoding.UTF8.GetBytes(content);
                    destination.Write(replacement, 0, replacement.Length);
                }
                else
                {
                    using var source = entry.Open();
                    source.CopyTo(destination);
                }
            }
        }

        return output.ToArray();
    }


    [Fact]
    public async Task CancelledLoadFailsExplicitly()
    {
        // R02: a cancelled load honors cancellation instead of running to
        // completion or failing with an unrelated error.
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            wb.save("/t.xlsx")
            return 1
            """,
            seed);
        Assert.True(built.Success, built.Failure?.Message);

        var script = new LythonEngine().Compile(
            """
            import openpyxl
            return openpyxl.load_workbook("/t.xlsx")
            """);
        Assert.True(script.IsValid);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var options = new LythonRunOptions { CancellationToken = cancellation.Token };

        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.xlsx", seed.ReadBytes("/t.xlsx"));
        var sync = script.Run(syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("execution canceled", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.xlsx", seed.ReadBytes("/t.xlsx"));
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("execution canceled", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

}
