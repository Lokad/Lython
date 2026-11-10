using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonNestedConstructionLifetimeTests
{
    [Theory]
    [InlineData("object_hook", "success")]
    [InlineData("object_hook", "cancel")]
    [InlineData("object_hook", "refuse")]
    [InlineData("parse_int", "success")]
    [InlineData("parse_int", "cancel")]
    [InlineData("parse_int", "refuse")]
    public async Task NestedContainersStayFundedThroughSuspendedCallbacks(string hook, string mode)
    {
        var document = new string('[', 8) + "{\"number\":7}" + new string(']', 8);
        var expected = new string('[', 8) + "{\"number\": \"v\"}" + new string(']', 8);
        var source = $$"""
            import json
            def convert(value):
                with open('/value') as stream:
                    text = stream.read()
                if mode == 'refuse':
                    return 'x' * 8388608
                if '{{hook}}' == 'parse_int':
                    return text
                value['number'] = text
                return value
            try:
                value = json.loads(document, {{hook}}=convert)
            except MemoryError:
                print('suppressed')
                return 'unexpected recovery'
            return json.dumps(value)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var compiled = new LythonEngine().Compile(source, budget);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost(); host.SeedFile("/value", "v");
        var entered = mode == "cancel" ? host.PauseReadUntilCancellation("/value") : null;
        var options = new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            Globals = new Dictionary<string, object?> { ["document"] = document, ["mode"] = mode }
        };
        var pending = compiled.RunAsync(host, options);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Nested callback did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (mode == "success")
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Equal(mode == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost(); fresh.SeedFile("/value", "v");
        using (var reused = await compiled.RunAsync(fresh, new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["document"] = document, ["mode"] = "success" }
        }).WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Assert.True(reused.Success, reused.Failure?.Message);
            Assert.Equal(expected, reused.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
