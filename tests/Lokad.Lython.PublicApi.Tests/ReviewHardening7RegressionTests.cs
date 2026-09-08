using System.IO.Compression;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R10 (second box): XLSX save output carries explicit host-clock entry
/// metadata and preserves original part content. These tests assert bytes and
/// entry metadata through an independent reader: saves are byte-deterministic
/// under a fixed host clock, every entry (including verbatim-copied preserved
/// parts) is stamped with the host local time rather than ambient machine
/// time or input timestamps, and foreign parts plus their content-type
/// overrides survive a load/edit/save round trip with identical content.
/// </summary>
public sealed class ReviewHardening7RegressionTests
{
    private static readonly DateTimeOffset HostTime = new(2024, 5, 6, 7, 8, 10, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset OtherHostTime = new(2025, 6, 7, 8, 9, 12, TimeSpan.FromHours(2));

    private const string ContentTypes = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/><Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/></Types>""";
    private const string RootRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";
    private const string Workbook = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""";
    private const string WorkbookRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""";
    private const string Sheet = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1"><v>42</v></c></row></sheetData></worksheet>""";
    private const string CoreProps = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><coreProperties xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"><title>Seed title</title></coreProperties>""";
    private const string AppProps = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"><Application>Lython seed</Application></Properties>""";

    private static readonly DateTimeOffset SeedTime = new(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private static byte[] BuildSeedXlsx()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteSeedEntry(archive, "[Content_Types].xml", ContentTypes);
            WriteSeedEntry(archive, "_rels/.rels", RootRels);
            WriteSeedEntry(archive, "xl/workbook.xml", Workbook);
            WriteSeedEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRels);
            WriteSeedEntry(archive, "xl/worksheets/sheet1.xml", Sheet);
            WriteSeedEntry(archive, "docProps/core.xml", CoreProps);
            WriteSeedEntry(archive, "docProps/app.xml", AppProps);
        }

        return stream.ToArray();
    }

    private static void WriteSeedEntry(ZipArchive archive, string name, string text)
    {
        // Deliberately distinct input metadata: STORED with an old timestamp,
        // so output assertions prove the save clock governs, not the input.
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = SeedTime;
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] SaveCreatedWorkbook(MockLythonHost host)
    {
        var result = new LythonEngine().Run(
            """
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
ws.cell(row=2, column=3, value="v")
wb.save("/out.xlsx")
return ws.max_row
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        return host.ReadBytes("/out.xlsx");
    }

    private static void AssertStampedWithHostTime(ZipArchiveEntry entry, DateTimeOffset expected)
    {
        Assert.Equal(expected.Year, entry.LastWriteTime.Year);
        Assert.Equal(expected.Month, entry.LastWriteTime.Month);
        Assert.Equal(expected.Day, entry.LastWriteTime.Day);
        Assert.Equal(expected.Hour, entry.LastWriteTime.Hour);
        Assert.Equal(expected.Minute, entry.LastWriteTime.Minute);
        Assert.Equal(expected.Second, entry.LastWriteTime.Second);
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        using var sink = new MemoryStream();
        stream.CopyTo(sink);
        return sink.ToArray();
    }

    [Fact]
    public void SaveIsByteDeterministicUnderFixedHostTime()
    {
        var firstHost = new MockLythonHost();
        firstHost.LocalNow = HostTime;
        var secondHost = new MockLythonHost();
        secondHost.LocalNow = HostTime;
        Assert.Equal(SaveCreatedWorkbook(firstHost), SaveCreatedWorkbook(secondHost));
    }

    [Fact]
    public void SaveStampsEveryEntryWithHostLocalTime()
    {
        var host = new MockLythonHost();
        host.LocalNow = HostTime;
        using var archive = new ZipArchive(new MemoryStream(SaveCreatedWorkbook(host)), ZipArchiveMode.Read);
        Assert.True(archive.Entries.Count > 0);
        foreach (var entry in archive.Entries)
        {
            AssertStampedWithHostTime(entry, HostTime);
        }
    }

    [Fact]
    public void SaveFollowsHostTimeChanges()
    {
        var firstHost = new MockLythonHost();
        firstHost.LocalNow = HostTime;
        var first = SaveCreatedWorkbook(firstHost);
        var secondHost = new MockLythonHost();
        secondHost.LocalNow = OtherHostTime;
        var second = SaveCreatedWorkbook(secondHost);
        Assert.NotEqual(first, second);
        using var archive = new ZipArchive(new MemoryStream(second), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            AssertStampedWithHostTime(entry, OtherHostTime);
        }
    }

    [Fact]
    public void PreservedPartsSurviveRoundTripWithContentIntact()
    {
        var seed = BuildSeedXlsx();
        var host = new MockLythonHost();
        host.LocalNow = HostTime;
        host.SeedWorkbook("/in.xlsx", seed);
        var result = new LythonEngine().Run(
            """
import openpyxl
wb = openpyxl.load_workbook("/in.xlsx")
ws = wb.active
ws.cell(row=1, column=1, value="edited")
wb.save("/out.xlsx")
return ws.max_row
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);

        using var archive = new ZipArchive(new MemoryStream(host.ReadBytes("/out.xlsx")), ZipArchiveMode.Read);
        // Foreign parts survive with byte-identical content even though their
        // entry metadata is normalized to the save host clock.
        Assert.Equal(Encoding.UTF8.GetBytes(CoreProps), ReadEntry(archive, "docProps/core.xml"));
        Assert.Equal(Encoding.UTF8.GetBytes(AppProps), ReadEntry(archive, "docProps/app.xml"));
        var coreEntry = archive.GetEntry("docProps/core.xml");
        Assert.NotNull(coreEntry);
        AssertStampedWithHostTime(coreEntry, HostTime);
        // Foreign content-type overrides seed through into regenerated output.
        var types = Encoding.UTF8.GetString(ReadEntry(archive, "[Content_Types].xml"));
        Assert.Contains("/docProps/core.xml", types, StringComparison.Ordinal);
        Assert.Contains("/docProps/app.xml", types, StringComparison.Ordinal);
        // Regenerated parts reflect the model edit, not the seed bytes.
        var sheet = Encoding.UTF8.GetString(ReadEntry(archive, "xl/worksheets/sheet1.xml"));
        Assert.Contains("edited", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain(">42<", sheet, StringComparison.Ordinal);
    }
}

