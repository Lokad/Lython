using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DeleteLifetimeTests
{
    private static string ProtocolSource(string kind)
    {
        const string common = """
events=[]
last={0:9}
def mark():
 with open('delete.txt','a') as target:target.write('deleted')
 events.append('done')
def tail():
 with open('tail.txt','w') as target:target.write('tail')
 return last
""";
        var definition = kind switch
        {
            "property" => "class Box:\n @property\n def value(self):return 7\n @value.deleter\n def value(self):mark()",
            "member" => "class Box:\n def __delattr__(self,name):mark()",
            _ => "class Box:\n def __delitem__(self,key):mark()",
        };
        var target = kind switch { "item" => "box[7]", "slice" => "box[1:3]", _ => "box.value" };
        return common + "\n" + definition + "\nbox=Box()\ndef clear():\n untouched=7\n del " + target +
            ",tail()[0]\n return untouched\nprint('before')\nprint(clear(),events,last)";
    }

    [Theory]
    [InlineData("item", false)]
    [InlineData("member", false)]
    [InlineData("property", false)]
    [InlineData("slice", false)]
    [InlineData("item", true)]
    [InlineData("member", true)]
    [InlineData("property", true)]
    [InlineData("slice", true)]
    public async Task ProtocolsFinishBeforeFollowingTargetOrRefund(string kind, bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(ProtocolSource(kind), budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/delete.txt", "before");
        host.SeedFile("/tail.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/delete.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Deletion finished before its callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "before\n" : "before\n7 ['done'] {}\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforedeleted", host.ReadText("/delete.txt"));
            Assert.Equal(cancel ? "before" : "tail", host.ReadText("/tail.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
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
    [InlineData("item")]
    [InlineData("member")]
    [InlineData("slice")]
    public async Task ProtocolAdmissionFailureStopsBeforeMutationAndLaterTargets(string kind)
    {
        const string common = """
def refuse():
 print('before')
 try:unused=[0]*8192
 except BaseException:print('caught')
 print('mutated')
""";
        var definition = kind == "member" ? "class Box:\n def __delattr__(self,name):refuse()" : "class Box:\n def __delitem__(self,key):refuse()";
        var target = kind switch { "item" => "box[7]", "slice" => "box[1:3]", _ => "box.value" };
        var source = common + "\n" + definition + "\nbox=Box()\ntry:del " + target + ",box[9]\nexcept BaseException:print('caught deletion')\nprint('after')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("before\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task DeletedBindingDoesNotRefundItsRetainedAlias()
    {
        const string source = "values={'drop':'é'*4096}\nkept=values['drop']\ndel values['drop']\nreturn kept";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = new string('é', 4096);
            Assert.Equal(expected, first.ReturnValue);
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, second.ReturnValue);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompleteJsonFileEditPublishesIndependentOutput()
    {
        const string source = """
import json
with open('config.json') as source:configuration=json.load(source)
del configuration['debug']
configuration['limit']+=1
with open('result.json','w') as target:json.dump(configuration,target,sort_keys=True)
print(configuration['name'],configuration['limit'])
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "{\"name\":\"job\",\"debug\":true,\"limit\":7}";
            if (delayed is not null) delayed.SeedFile("/config.json", input);
            else mock!.SeedFile("/config.json", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("job 8\n", result.StandardOutput);
                Assert.Equal("{\"limit\": 8, \"name\": \"job\"}", delayed?.ReadText("/result.json") ?? mock!.ReadText("/result.json"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task DeletionKeepsManagedRecursionProtection()
    {
        using var script = new LythonEngine().Compile("class Box:\n def __delitem__(self,key):del self[key]\ndel Box()[0]");
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
