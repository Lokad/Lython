using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormatMappingTests
{
    // Frozen with CPython 3.13.2, independent of Lython's formatter.
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FormatMapping", "cases.json");
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task MappingFieldsFollowPythonLookupAndErrorOrder(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.Empty(result.StandardError);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    private const string HostedSource = """
class Mapping:
 def __getitem__(self,key):
  with open('lookup.txt','a') as target:target.write(key)
  return 'done'
return 'prefix:{x}:{x}'.format_map(Mapping())
""";

    [Fact]
    public async Task LookupAwaitsHostWorkAndRetainsOnlyFundedResults()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(HostedSource, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/lookup.txt", "");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/lookup.txt", "");
            using (var result = mode == "sync" ? script.Run(immediate)
                : await script.RunAsync(mode == "delayed" ? delayed : immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("prefix:done:done", result.ReturnValue);
                Assert.Equal("xx", mode == "delayed" ? delayed.ReadText("/lookup.txt") : immediate.ReadText("/lookup.txt"));
                Assert.True(budget.ReservedBytes > baseline);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CancellationDuringLookupDrainsBeforeRefund()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(HostedSource, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/lookup.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/lookup.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("before", host.ReadText("/lookup.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("{x.value}", "attribute")]
    [InlineData("{x[leaf]}", "leaf")]
    public async Task FieldSuffixAwaitsTheExistingAttributeAndItemProtocols(string template, string effect)
    {
        const string source = """
class Node:
 @property
 def value(self):
  with open('lookup.txt','a') as target:target.write('attribute')
  return 'done'
 def __getitem__(self,key):
  with open('lookup.txt','a') as target:target.write(key)
  return 'done'
return TEMPLATE.format_map({'x':Node()})
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source.Replace("TEMPLATE", "'" + template + "'"), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/lookup.txt", "");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("done", result.ReturnValue);
            Assert.Equal(effect, host.ReadText("/lookup.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task RefusedLookupCannotPublishOrContinue()
    {
        const string source = """
class Mapping:
 def __getitem__(self,key):
  print('before')
  try:unused=[0]*8192
  except BaseException:pass
  return 'value'
try:print('{x}:{y}'.format_map(Mapping()))
except BaseException:print('caught')
print('after')
""";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(64 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("before\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
