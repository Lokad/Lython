using System.IO.Compression;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening6RegressionTests
{
    private const string ContentTypes = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""";
    private const string RootRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";
    private const string Workbook = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""";
    private const string WorkbookRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""";
    private const string SheetPrefix = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1"><v>42</v></c></row></sheetData></worksheet>""";

    private static byte[] BuildXlsx(string sheetXml, Action<ZipArchive>? extra = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypes);
            WriteEntry(archive, "_rels/.rels", RootRels);
            WriteEntry(archive, "xl/workbook.xml", Workbook);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRels);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
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

    private static string LoadScript(string path) => $"""
import openpyxl
wb = openpyxl.load_workbook("{path}")
return wb.sheetnames
""";
    private sealed record RawPart(string Name, byte[] Data, long? DeclaredLength, bool CorruptCrc);

    private static byte[] BuildRawArchive(params RawPart[] parts)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            var offsets = new List<long>();
            foreach (var part in parts)
            {
                offsets.Add(stream.Position);
                var nameBytes = Encoding.UTF8.GetBytes(part.Name);
                var declared = part.DeclaredLength ?? part.Data.Length;
                var crc = part.CorruptCrc ? 0u : ComputeCrc32(part.Data);
                writer.Write(0x04034b50u);
                writer.Write((ushort)20);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0x21);
                writer.Write(crc);
                writer.Write((uint)part.Data.Length);
                writer.Write((uint)declared);
                writer.Write((ushort)nameBytes.Length);
                writer.Write((ushort)0);
                writer.Write(nameBytes);
                writer.Write(part.Data);
            }

            var directoryOffset = stream.Position;
            for (var i = 0; i < parts.Length; i++)
            {
                var nameBytes = Encoding.UTF8.GetBytes(parts[i].Name);
                var declared = parts[i].DeclaredLength ?? parts[i].Data.Length;
                var crc = parts[i].CorruptCrc ? 0u : ComputeCrc32(parts[i].Data);
                writer.Write(0x02014b50u);
                writer.Write((ushort)20);
                writer.Write((ushort)20);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0x21);
                writer.Write(crc);
                writer.Write((uint)parts[i].Data.Length);
                writer.Write((uint)declared);
                writer.Write((ushort)nameBytes.Length);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write(0u);
                writer.Write((uint)offsets[i]);
                writer.Write(nameBytes);
            }

            var directorySize = stream.Position - directoryOffset;
            writer.Write(0x06054b50u);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)parts.Length);
            writer.Write((ushort)parts.Length);
            writer.Write((uint)directorySize);
            writer.Write((uint)directoryOffset);
            writer.Write((ushort)0);
        }
        return stream.ToArray();
    }


    private static uint ComputeCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
    [Fact]
    public void RawMinimalArchiveLoads()
    {
        var fixed_ = new UTF8Encoding(false);
        var parts = new RawPart[]
        {
            new("[Content_Types].xml", fixed_.GetBytes(ContentTypes), null, false),
            new("_rels/.rels", fixed_.GetBytes(RootRels), null, false),
            new("xl/workbook.xml", fixed_.GetBytes(Workbook), null, false),
            new("xl/_rels/workbook.xml.rels", fixed_.GetBytes(WorkbookRels), null, false),
            new("xl/worksheets/sheet1.xml", fixed_.GetBytes(SheetPrefix), null, false),
        };
        var host = new MockLythonHost();
        host.SeedWorkbook("/raw.xlsx", BuildRawArchive(parts));
        var result = new LythonEngine().Run(LoadScript("/raw.xlsx"), host);
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void PaddingBombFailsUnderSmallMemoryBudget()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/pad.xlsx", BuildXlsx(SheetPrefix, archive =>
        {
            var pad = archive.CreateEntry("xl/padding.bin", CompressionLevel.Fastest);
            using var s = pad.Open();
            var zeros = new byte[2097152];
            s.Write(zeros, 0, zeros.Length);
        }));
        var result = new LythonEngine().Run(
            LoadScript("/pad.xlsx"),
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }
    [Fact]
    public async Task PaddingBombFailsUnderSmallMemoryBudgetAsync()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/pad.xlsx", BuildXlsx(SheetPrefix, archive =>
        {
            var pad = archive.CreateEntry("xl/padding.bin", CompressionLevel.Fastest);
            using var s = pad.Open();
            var zeros = new byte[2097152];
            s.Write(zeros, 0, zeros.Length);
        }));
        var result = await new LythonEngine().RunAsync(
            LoadScript("/pad.xlsx"),
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void ManyTinyEntriesRespectCollectionLimit()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/many.xlsx", BuildXlsx(SheetPrefix, archive =>
        {
            for (var i = 0; i < 2000; i++)
            {
                WriteEntry(archive, "xl/pad" + i.ToString() + ".bin", "x");
            }
        }));
        var result = new LythonEngine().Run(
            LoadScript("/many.xlsx"),
            host,
            new LythonRunOptions { MaxCollectionSize = 100 });
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManyTinyEntriesLoadCleanly()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/many.xlsx", BuildXlsx(SheetPrefix, archive =>
        {
            for (var i = 0; i < 200; i++)
            {
                WriteEntry(archive, "xl/pad" + i.ToString() + ".bin", "x");
            }
        }));
        var result = new LythonEngine().Run(LoadScript("/many.xlsx"), host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "S" }, Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void TruncatedArchiveFailsAsInvalidFile()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/bogus", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01 });
        var result = new LythonEngine().Run(LoadScript("/bogus"), host);
        Assert.False(result.Success);
        Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
    }

    [Fact]
    public void CorruptCentralDirectoryFailsAsInvalidFile()
    {
        var payload = BuildXlsx(SheetPrefix);
        for (var i = payload.Length - 10; i < payload.Length; i++)
        {
            payload[i] ^= 0xFF;
        }
        var host = new MockLythonHost();
        host.SeedWorkbook("/corrupt.xlsx", payload);
        var result = new LythonEngine().Run(LoadScript("/corrupt.xlsx"), host);
        Assert.False(result.Success);
        Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
    }

    [Fact]
    public void CorruptEntryFailsAsInvalidFile()
    {
        var codec = new UTF8Encoding(false);
        var parts = new RawPart[]
        {
            new("[Content_Types].xml", codec.GetBytes(ContentTypes), null, false),
            new("_rels/.rels", codec.GetBytes(RootRels), null, false),
            new("xl/workbook.xml", codec.GetBytes(Workbook), null, false),
            new("xl/_rels/workbook.xml.rels", codec.GetBytes(WorkbookRels), null, false),
            new("xl/worksheets/sheet1.xml", codec.GetBytes(SheetPrefix), null, false),
        };
        var host = new MockLythonHost();
        var payload = BuildRawArchive(parts);
        var marker = Encoding.UTF8.GetBytes("<v>42</v>");
        var at = payload.AsSpan().IndexOf(marker);
        Assert.True(at >= 0);
        payload[at + 3] = (byte)'5';
        host.SeedWorkbook("/bad.xlsx", payload);
        var result = new LythonEngine().Run(LoadScript("/bad.xlsx"), host);
        Assert.False(result.Success);
        Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
    }



    [Fact]
    public void DeclaredLongerThanActualFails()
    {
        var codec = new UTF8Encoding(false);
        var sheet = codec.GetBytes(SheetPrefix);
        var parts = new RawPart[]
        {
            new("[Content_Types].xml", codec.GetBytes(ContentTypes), null, false),
            new("_rels/.rels", codec.GetBytes(RootRels), null, false),
            new("xl/workbook.xml", codec.GetBytes(Workbook), null, false),
            new("xl/_rels/workbook.xml.rels", codec.GetBytes(WorkbookRels), null, false),
            new("xl/worksheets/sheet1.xml", sheet, sheet.Length + 100, false),
        };
        var host = new MockLythonHost();
        host.SeedWorkbook("/declared.xlsx", BuildRawArchive(parts));
        var result = new LythonEngine().Run(LoadScript("/declared.xlsx"), host);
        Assert.False(result.Success);
        Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
    }

    [Fact]
    public void DeclaredShorterThanActualFails()
    {
        var codec = new UTF8Encoding(false);
        var sheet = codec.GetBytes(SheetPrefix);
        var parts = new RawPart[]
        {
            new("[Content_Types].xml", codec.GetBytes(ContentTypes), null, false),
            new("_rels/.rels", codec.GetBytes(RootRels), null, false),
            new("xl/workbook.xml", codec.GetBytes(Workbook), null, false),
            new("xl/_rels/workbook.xml.rels", codec.GetBytes(WorkbookRels), null, false),
            new("xl/worksheets/sheet1.xml", sheet, sheet.Length - 10, false),
        };
        var host = new MockLythonHost();
        host.SeedWorkbook("/declared.xlsx", BuildRawArchive(parts));
        var result = new LythonEngine().Run(LoadScript("/declared.xlsx"), host);
        Assert.False(result.Success);
        Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
    }

    [Fact]
    public void DeepXmlIsRejectedQuickly()
    {
        var sheet = SheetPrefix.Replace("<sheetData>", "<sheetData>" + string.Concat(Enumerable.Repeat("<x>", 20000)));
        var host = new MockLythonHost();
        host.SeedWorkbook("/deep.xlsx", BuildXlsx(sheet));
        var result = new LythonEngine().Run(LoadScript("/deep.xlsx"), host);
        Assert.False(result.Success);
        Assert.Equal("InvalidFileException", result.Failure?.ExceptionType);
        Assert.Contains("nesting", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ModerateDepthXmlLoads()
    {
        var sheet = SheetPrefix.Replace("<sheetData>", "<sheetData>" + string.Concat(Enumerable.Repeat("<x>", 500)) + string.Concat(Enumerable.Repeat("</x>", 500)));
        var host = new MockLythonHost();
        host.SeedWorkbook("/medium.xlsx", BuildXlsx(sheet));
        var result = new LythonEngine().Run(LoadScript("/medium.xlsx"), host);
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void OutputGrowthRespectsMemoryBudget()
    {
        var setup = """
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
for i in range(1, 3001):
    ws.cell(row=i, column=1, value="v")
return ws.max_row
""";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var baseline = new LythonEngine().Run(setup, new MockLythonHost(), options);
        Assert.True(baseline.Success, baseline.Failure?.Message);

        var withSave = """
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
for i in range(1, 3001):
    ws.cell(row=i, column=1, value="v")
wb.save("/out.xlsx")
return ws.max_row
""";
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(withSave, host, options);
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

}


