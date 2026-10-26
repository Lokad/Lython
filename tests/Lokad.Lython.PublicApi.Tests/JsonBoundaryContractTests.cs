using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonBoundaryContractTests
{
    // Independent CPython 3.13.2 goldens; normal CI does not launch Python.
    public sealed record Case(string Name, string Source, string Output);
    private static Case[] Load(string name) => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "JsonBoundary", name)))!;
    public static IEnumerable<object[]> PureCases() => Load("cases.json").SelectMany(c =>
        new[] { new object[] { c.Name, c.Source, c.Output, false }, new object[] { c.Name, c.Source, c.Output, true } });
    public static IEnumerable<object[]> HostedCases() => Load("hosted.json").SelectMany(c =>
        Enumerable.Range(0, 3).Select(mode => new object[] { c.Name, c.Source, c.Output, mode }));

    [Theory]
    [MemberData(nameof(PureCases))]
    public async Task ValuesAndProtocolsFollowPython(string name, string source, string output, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(output, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(HostedCases))]
    public async Task GuestProtocolsAwaitActualHostIo(string name, string source, string output, int mode)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var immediate = new MockLythonHost(); immediate.SeedFile("/value", "1");
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/value", "1");
        ILythonHost host = mode == 2 ? delayed : immediate;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = mode == 0 ? script.Run(host, options) : await script.RunAsync(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(output, result.StandardOutput);
            if (mode == 2) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AcceptedHostIntegerWidthsAgreeAcrossWriters(bool asynchronous)
    {
        object[] values = { (sbyte)1, (byte)1, (short)1, (ushort)1, 1, (uint)1, (long)1, (ulong)1 };
        using var script = new LythonEngine().Compile("import json\nprint(json.dumps(value))\nprint(json.JSONEncoder().encode(value))\nprint(''.join(json.JSONEncoder().iterencode(value)))");
        var budget = new LythonMemoryBudget(1024 * 1024);
        foreach (var value in values)
        {
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["value"] = value } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("1\n1\n1\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
