using System.Numerics;
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
}
