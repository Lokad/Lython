using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeSliceSuspensionTests
{
    public static IEnumerable<object?[]> Cases()
    {
        foreach (var operation in new[] { "read", "assign", "delete", "indices" })
        foreach (var pause in new[] { null, "bind-step", "call-step", "call-start", "call-stop" })
            yield return [operation, pause];
        yield return ["assign", "iter"];
        yield return ["assign", "next-1"];
        yield return ["indices", "bind-length"];
        yield return ["indices", "call-length"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeCallbacksSuspendAndCancelInOrder(string operation, string? pause)
    {
        const string definitions = """
            def mark(name):
             with open(name+'.txt','a') as output: output.write('x')
            class Bound:
             def __init__(self,name,value): self.name=name; self.value=value
             @property
             def __index__(self):
              mark('bind-'+self.name)
              def convert():
               mark('call-'+self.name)
               return self.value
              return convert
            class Replacement:
             def __init__(self): self.position=0
             def __iter__(self):
              mark('iter')
              return self
             def __next__(self):
              if self.position==2: raise StopIteration
              mark('next-'+str(self.position))
              self.position+=1
              return 6+self.position
            target=[0,1,2,3]
            start=Bound('start',0)
            stop=Bound('stop',4)
            step=Bound('step',2)
            """;
        var action = operation switch
        {
            "read" => "output=target[start:stop:step]",
            "assign" => "target[start:stop:step]=Replacement()\noutput=target",
            "delete" => "del target[start:stop:step]\noutput=target",
            _ => "output=slice(start,stop,step).indices(Bound('length',4))"
        };
        var source = definitions + "\n" + action + "\nwith open('result.txt','w') as result: result.write(str(output))";
        var expected = operation switch { "read" => "[0, 2]", "assign" => "[7, 1, 8, 3]", "delete" => "[1, 3]", _ => "(0, 4, 2)" };
        var names = new List<string>();
        if (operation == "indices") names.AddRange(["bind-length", "call-length"]);
        names.AddRange(["bind-step", "call-step", "bind-start", "call-start", "bind-stop", "call-stop"]);
        if (operation == "assign") names.AddRange(["iter", "next-0", "next-1"]);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var name in names) host.SeedFile("/" + name + ".txt", "before");
        host.SeedFile("/result.txt", "original");
        var entered = pause is null ? null : host.PauseWriteUntilCancellation("/" + pause + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Native slice finished before the controlled callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? expected : "original", host.ReadText("/result.txt"));
            var completed = pause is null ? names.Count : names.IndexOf(pause);
            Assert.True(completed >= 0);
            for (var i = 0; i < names.Count; i++)
                Assert.Equal("before" + (i < completed ? "x" : ""), host.ReadText("/" + names[i] + ".txt"));
            if (entered is null) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
