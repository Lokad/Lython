using System.Text.Json;
using System.IO.Compression;
using System.Xml.Linq;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SpreadsheetContractTests
{
    [Fact]
    public void MinimalLoadedStylesReceiveDefaultTablesBeforeCustomRecords()
    {
        var host = new MockLythonHost();
        using (var original = new LythonEngine().Run("from openpyxl import Workbook\nw=Workbook()\nw.active['A1']=1\nw.save('/minimal.xlsx')", host))
            Assert.True(original.Success, original.Failure?.Message);
        using (var buffer = new MemoryStream())
        {
            buffer.Write(host.ReadBytes("/minimal.xlsx"));
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
            {
                archive.GetEntry("xl/styles.xml")?.Delete();
                using var writer = new StreamWriter(archive.CreateEntry("xl/styles.xml").Open());
                writer.Write("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellXfs></styleSheet>");
            }
            host.SeedBytes("/minimal.xlsx", buffer.ToArray());
        }
        using var result = new LythonEngine().Run("""
            from openpyxl import load_workbook
            from openpyxl.styles import Font
            w=load_workbook('/minimal.xlsx')
            w.active['A1'].font=Font(bold=True)
            w.active['A1'].number_format='0.000'
            w.save('/custom.xlsx')
            loaded=load_workbook('/custom.xlsx')
            print(loaded.active['A1'].font.bold,loaded.active['A1'].number_format,loaded.active['A1'].value)
            """, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True 0.000 1\n", result.StandardOutput);
        using var bytes = new MemoryStream(host.ReadBytes("/custom.xlsx"));
        using var zip = new ZipArchive(bytes, ZipArchiveMode.Read);
        using var styles = zip.GetEntry("xl/styles.xml")!.Open();
        var root = XDocument.Load(styles).Root!;
        Assert.Equal(new[] { "numFmts", "fonts", "fills", "borders", "cellStyleXfs", "cellXfs" }, root.Elements().Select(element => element.Name.LocalName));
    }

    [Fact]
    public async Task AppendedGeneratorAwaitsRealHostIOAndReturnsTheBudget()
    {
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            from openpyxl import Workbook
            sheet=Workbook().active
            def cells():
                with open('/value') as source: yield source.read()
            sheet.append(cells())
            return sheet['A1'].value
            """, root))
        {
            var baseline = root.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/value", "contents");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("contents", result.ReturnValue);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StyleIdsAndLoadedEditsSurviveSaveAndReload(bool asynchronous)
    {
        const string source = """
            from openpyxl import Workbook,load_workbook
            from openpyxl.styles import Font
            w=Workbook();s=w.active
            s['A1'].font=Font(bold=True)
            first=s['A1'].style_id
            s['A2'].font=Font(italic=True)
            print(first==s['A1'].style_id,first!=s['A2'].style_id)
            s['D12'] # an unstyled blank cell is not persisted by openpyxl
            w.save('/first.xlsx')
            loaded=load_workbook('/first.xlsx');out=loaded.active
            print(out['A1'].style_id==first,out['A1'].font.bold,out['A2'].font.italic)
            print(out.max_row,out.max_column)
            out['A1'].number_format='0.000'
            changed=out['A1'].style_id
            print(changed!=first,out['A1'].font.bold)
            loaded.save('/second.xlsx')
            again=load_workbook('/second.xlsx').active
            print(again['A1'].style_id==changed,again['A1'].font.bold,again['A2'].font.italic,again['A1'].number_format)
            """;
        var root = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            var baseline = root.ReservedBytes;
            var host = new MockLythonHost();
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("True True\nTrue True True\n2 1\nTrue True\nTrue True True 0.000\n", result.StandardOutput);
            }
            using var bytes = new MemoryStream(host.ReadBytes("/second.xlsx"));
            using var archive = new ZipArchive(bytes, ZipArchiveMode.Read);
            using var styles = archive.GetEntry("xl/styles.xml")!.Open();
            var xml = XDocument.Load(styles);
            // OOXML order, also emitted by the independent openpyxl stylesheet.
            Assert.Equal("numFmts", xml.Root!.Elements().First().Name.LocalName);
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public async Task PendingSaveKeepsPayloadFundedUntilCancellationAndCleanup()
    {
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("from openpyxl import Workbook\nw=Workbook()\nw.active['A1']=7\nw.save('/pending.xlsx')", root))
        {
            var baseline = root.ReservedBytes;
            var host = new DelayedLythonHost();
            var entered = host.PauseWriteUntilCancellation("/pending.xlsx");
            using var cancellation = new CancellationTokenSource();
            var run = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(run.IsCompleted);
                Assert.True(root.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData("s.insert_rows(1,2147483647)")]
    [InlineData("s.delete_rows(2,2147483647)")]
    [InlineData("s.insert_cols(1,2147483647)")]
    public async Task LargeStructuralArgumentsDoNotEscapeAsClrOverflow(string operation)
    {
        using var script = new LythonEngine().Compile("from openpyxl import Workbook\ns=Workbook().active\ns['A1']=1\n" + operation);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            if (!result.Success) Assert.Equal("ValueError", result.Failure?.ExceptionType);
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SpreadsheetContracts", "cases.json");
        foreach (var item in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            foreach (var asynchronous in new[] { false, true })
                yield return [item.GetProperty("name").GetString()!, item.GetProperty("source").GetString()!, item.GetProperty("stdout").GetString()!, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesIndependentOpenPyxl315Reference(string name, string source, string expected, bool asynchronous)
    {
        // CPython 3.13.2 / openpyxl 3.1.5, captured independently of either Lython API.
        _ = name;
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
