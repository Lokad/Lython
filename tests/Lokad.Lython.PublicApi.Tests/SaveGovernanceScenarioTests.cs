using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: workbook save is governed end to end — output capacity is reserved
/// from model sizes before serializing, the final payload is charged before
/// host publication, and the charge releases afterwards. A hyperlink-heavy
/// sheet (output beyond the flat per-cell allowance) fails under a tight
/// budget without publishing a partial file, and round-trips under a roomy
/// one in both modes.
/// </summary>
public sealed class SaveGovernanceScenarioTests
{
    private const string BuildAndSave =
        """
        import openpyxl
        wb = openpyxl.Workbook()
        ws = wb.active
        h = "x"
        i = 1
        while i <= 1500:
            ws.cell(row=i, column=1).hyperlink = h
            i = i + 1
        wb.save("/t.xlsx")
        return 0
        """;

    [Fact]
    public async Task HyperlinkHeavySaveRespectsBudget()
    {
        var script = new LythonEngine().Compile(BuildAndSave);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.False(syncHost.Exists("/t.xlsx"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.False(asyncHost.Exists("/t.xlsx"));
    }

    [Fact]
    public async Task RoomySaveRoundTripsHyperlinks()
    {
        var script = new LythonEngine().Compile(BuildAndSave);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var host = new MockLythonHost();
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        var bytes = host.ReadBytes("/t.xlsx");
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)0x50, bytes[0]);
        Assert.Equal((byte)0x4B, bytes[1]);

        var check = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.load_workbook("/t.xlsx")
            ws = wb.active
            return ws["A1500"].hyperlink.target
            """);
        Assert.True(check.IsValid);
        var loaded = check.Run(host);
        Assert.True(loaded.Success, loaded.Failure?.Message);
        Assert.Equal("x", loaded.ReturnValue);

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(asyncHost.Exists("/t.xlsx"));
    }
}