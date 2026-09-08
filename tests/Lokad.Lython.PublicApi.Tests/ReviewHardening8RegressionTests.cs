using System.IO.Compression;
using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R15: one bounded per-load package reader with explicit ownership. Loads
/// inflate each entry at most once; writable loads transfer retention to the
/// write-preservation snapshot while read-only loads release everything, so a
/// read-only load fits budgets that a writable load of the same archive
/// exceeds, and corruption in parts no read operation consumes stays unread.
/// </summary>
public sealed class ReviewHardening8RegressionTests
{
    private const string ContentTypes = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""";
    private const string RootRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";
    private const string Workbook = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""";
    private const string WorkbookRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""";
    private const string Sheet = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1"><v>42</v></c></row></sheetData></worksheet>""";

    private static byte[] BuildXlsx(Action<ZipArchive>? extra = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypes);
            WriteEntry(archive, "_rels/.rels", RootRels);
            WriteEntry(archive, "xl/workbook.xml", Workbook);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRels);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", Sheet);
            extra?.Invoke(archive);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string LoadScript(string path, string options)
        => "import openpyxl\nwb = openpyxl.load_workbook(\"" + path + "\"" + options + ")\nreturn wb.sheetnames\n";

    [Fact]
    public void ReadOnlyLoadSkipsSnapshotRetention()
    {
        // The 2MB padding passes the declared-size gate but only a writable
        // load retains it. With a 3075000-byte budget the writable run exhausts
        // its budget on the retained snapshot while the read-only run still has
        // room for a further 1MB allocation in the same run.
        var payload = BuildXlsx(archive =>
        {
            var pad = archive.CreateEntry("xl/padding.bin", CompressionLevel.Fastest);
            using var stream = pad.Open();
            var zeros = new byte[2097152];
            stream.Write(zeros, 0, zeros.Length);
        });
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3075000 };

        var writable = new LythonEngine().Run(
            """
import openpyxl
wb = openpyxl.load_workbook("/pad.xlsx")
bulk = "x" * 1000000
return len(bulk)
""",
            SeedHost("/pad.xlsx", payload),
            options);
        Assert.False(writable.Success);
        Assert.Equal("MemoryError", writable.Failure?.ExceptionType);

        var host = SeedHost("/pad.xlsx", payload);
        var readOnly = new LythonEngine().Run(
            """
import openpyxl
wb = openpyxl.load_workbook("/pad.xlsx", read_only=True)
bulk = "x" * 1000000
return len(bulk)
""",
            host,
            options);
        Assert.True(readOnly.Success, readOnly.Failure?.Message);
        Assert.Equal(new BigInteger(1000000), readOnly.ReturnValue);
    }

    [Fact]
    public void ReadOnlyLoadStillEnforcesTransientBudgets()
    {
        var host = SeedHost("/tiny.xlsx", BuildXlsx());
        var result = new LythonEngine().Run(
            LoadScript("/tiny.xlsx", ", read_only=True"),
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void ReadOnlyLoadIgnoresCorruptUnreadParts()
    {
        var payload = BuildXlsx(archive =>
        {
            var pad = archive.CreateEntry("xl/padding.bin", CompressionLevel.NoCompression);
            using var stream = pad.Open();
            var zeros = new byte[4096];
            stream.Write(zeros, 0, zeros.Length);
        });
        // Corrupt the padding content after the central directory recorded its CRC.
        var marker = new byte[64];
        var at = payload.AsSpan().IndexOf(marker);
        Assert.True(at >= 0);
        payload[at] ^= 0xFF;

        var writable = new LythonEngine().Run(LoadScript("/bad.xlsx", string.Empty), SeedHost("/bad.xlsx", payload));
        Assert.False(writable.Success);
        Assert.Equal("InvalidFileException", writable.Failure?.ExceptionType);

        var host = SeedHost("/bad.xlsx", payload);
        var readOnly = new LythonEngine().Run(LoadScript("/bad.xlsx", ", read_only=True"), host);
        Assert.True(readOnly.Success, readOnly.Failure?.Message);
        Assert.Equal(new List<object?> { "S" }, Assert.IsType<List<object?>>(readOnly.ReturnValue));
    }

    private static MockLythonHost SeedHost(string path, byte[] payload)
    {
        var host = new MockLythonHost();
        host.SeedWorkbook(path, payload);
        return host;
    }
}

