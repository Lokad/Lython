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
        while i <= 4:
            ws.cell(row=i, column=1).hyperlink = h
            i = i + 1
        print("ready")
        wb.save("/t.xlsx")
        return 0
        """;

    [Fact]
    public async Task HyperlinkHeavySaveRespectsBudget()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildAndSave);
        Assert.True(script.IsValid);
        var root = new LythonMemoryBudget(131072);
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonMemoryReservation? hold = null;
            var host = new MockLythonHost();
            host.OnStandardOutputWrite = () =>
            {
                if (hold is null) Assert.True(root.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - 4096, out hold));
            };
            try
            {
                using var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = root })
                    : script.Run(host, new LythonRunOptions { MemoryBudget = root });
                Assert.NotNull(hold); // construction completed; denial belongs to save
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.False(host.Exists("/t.xlsx"));
            }
            finally { hold?.Dispose(); }
            Assert.Equal(0, root.ReservedBytes);
        }
    }

    [Fact]
    public async Task RoomySaveRoundTripsHyperlinks()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildAndSave);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(524288) };
        var host = new MockLythonHost();
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        var bytes = host.ReadBytes("/t.xlsx");
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)0x50, bytes[0]);
        Assert.Equal((byte)0x4B, bytes[1]);

        var check = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import openpyxl
            wb = openpyxl.load_workbook("/t.xlsx")
            ws = wb.active
            return ws["A4"].hyperlink.target
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
    [Fact, Trait("Category", "Stress")]
    public async Task OriginalHyperlinkScaleRemainsCovered()
    {
        using var script = new LythonEngine().Compile(BuildAndSave.Replace("i <= 4", "i <= 1500", StringComparison.Ordinal));
        var root = new LythonMemoryBudget(8 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = root })
                : script.Run(host, new LythonRunOptions { MemoryBudget = root }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.True(host.Exists("/t.xlsx"));
            }
            Assert.Equal(0, root.ReservedBytes);
        }
    }

}