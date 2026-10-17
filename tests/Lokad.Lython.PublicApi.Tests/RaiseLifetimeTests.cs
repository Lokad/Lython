using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RaiseLifetimeTests
{
    private const string CallbackSource = """
def value():
 with open('value.txt','a') as target:target.write('value')
 return ValueError('payload')
def cause():
 with open('cause.txt','a') as target:target.write('cause')
 return KeyError('origin')
print('before')
try:raise value() from cause()
except ValueError as error:print(error.args,error.__cause__.args)
""";

    [Theory]
    [InlineData(null)]
    [InlineData("value")]
    [InlineData("cause")]
    public async Task RaiseExpressionsAwaitOrDrainBeforeRefund(string? phase)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(CallbackSource, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "before");
        host.SeedFile("/cause.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = phase is null ? null : host.PauseWriteUntilCancellation("/" + phase + ".txt");
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Raise finished before its expression suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(phase is null, result.Success);
            Assert.Equal(phase is null ? "before\n('payload',) ('origin',)\n" : "before\n", result.StandardOutput);
            Assert.Equal(phase is null or "cause" ? "beforevalue" : "before", host.ReadText("/value.txt"));
            Assert.Equal(phase is null ? "beforecause" : "before", host.ReadText("/cause.txt"));
            if (phase is not null) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RaiseExpressionRefusalRemainsTerminal(bool cause)
    {
        const string common = """
def refuse():
 print('before')
 try:unused=[0]*8192
 except BaseException:print('caught')
 print('after')
 return ValueError('ignored')
""";
        var source = common + "\ntry:raise " + (cause ? "ValueError('payload') from refuse()" : "refuse()") +
            "\nexcept BaseException:print('caught raise')\nprint('after raise')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 64 * 1024 };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
                Assert.Equal("before\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedCauseArgumentRemainsFundedAcrossExecutions()
    {
        const string source = """
def cause():
 with open('cause.txt','a') as target:target.write('cause')
 return KeyError('é'*4096)
try:raise ValueError('payload') from cause()
except ValueError as error:return error.__cause__.args[0]
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var mock = new MockLythonHost();
        mock.SeedFile("/cause.txt", "before");
        using (var first = script.Run(mock))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = new string('é', 4096);
            Assert.Equal(expected, first.ReturnValue);
            Assert.Equal("beforecause", mock.ReadText("/cause.txt"));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/cause.txt", "before");
            using (var second = await script.RunAsync(delayed))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, second.ReturnValue);
                Assert.Equal("beforecause", delayed.ReadText("/cause.txt"));
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompleteJsonValidationPublishesIndependentReport()
    {
        const string source = """
import json
def require(configuration):
 if 'name' not in configuration:raise ValueError('missing name')
 return configuration['name']
names=[]
errors=[]
for path in ['missing.json','valid.json']:
 with open(path) as source:configuration=json.load(source)
 try:names.append(require(configuration))
 except ValueError as error:errors.append(str(error))
with open('report.json','w') as target:json.dump({'names':names,'errors':errors},target,sort_keys=True)
print(len(names),len(errors))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) { delayed.SeedFile("/missing.json", "{\"limit\":7}"); delayed.SeedFile("/valid.json", "{\"name\":\"job\"}"); }
            else { mock!.SeedFile("/missing.json", "{\"limit\":7}"); mock.SeedFile("/valid.json", "{\"name\":\"job\"}"); }
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("1 1\n", result.StandardOutput);
                Assert.Equal("{\"errors\": [\"missing name\"], \"names\": [\"job\"]}", delayed?.ReadText("/report.json") ?? mock!.ReadText("/report.json"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RaiseExpressionKeepsManagedRecursionProtection()
    {
        using var script = new LythonEngine().Compile("def value():return value()\nraise value()");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxRecursionDepth = 16 };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.Contains(result.FailureAttribution?.Reason, new LythonFailureReason?[]
                { LythonFailureReason.RecursionDepthLimit, LythonFailureReason.InterpreterStackLimit });
        }
    }
}
