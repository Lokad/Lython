using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ConstructorLifetimeTests
{
    [Theory]
    [InlineData(false, "success")] [InlineData(true, "success")]
    [InlineData(false, "refusal")] [InlineData(true, "refusal")]
    [InlineData(true, "cancellation")]
    public async Task DefaultAllocatorOwnsNamedInputsThroughSuspendingDefaults(bool asynchronous, string outcome)
    {
        const string source = """
            class Key:
             def __hash__(self):return hash('x')
             def __eq__(self,other):
              global payload
              payload=None
              C.__init__.__kwdefaults__={'x':99,'y':100}
              with open('/bind') as source:source.read()
              if fail:
               try:unused='z'*2000000
               except MemoryError:print('caught-default')
               finally:return True
              return other=='x'
            class C:
             def __init__(self,value,*,x,y):
              print(len(value[0]),x,y)
              self.value=value
            C.__init__.__kwdefaults__={Key():7,'y':8}
            payload=['é'*8192]
            try:result=C(value=payload)
            except BaseException:print('caught-caller')
            finally:
             if fail:return 0
            return result.value
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) delayed.SeedFile("/bind", "ready");
            else mock!.SeedFile("/bind", "ready");
            var entered = outcome == "cancellation" ? delayed!.PauseReadUntilCancellation("/bind") : null;
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions
            {
                CancellationToken = cancellation.Token,
                Globals = new Dictionary<string, object?> { ["fail"] = outcome == "refusal" },
            };
            LythonExecutionResult? result = null;
            var pending = asynchronous ? script.RunAsync(delayed!, options) : Task.FromResult(script.Run(mock!, options));
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline + 16384);
                    cancellation.Cancel();
                }
                result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(outcome == "success", result.Success);
                if (outcome == "success")
                {
                    Assert.Equal("8192 7 100\n", result.StandardOutput);
                    var values = Assert.IsType<List<object?>>(result.ReturnValue);
                    Assert.Equal(new string('é', 8192), Assert.Single(values));
                    Assert.True(budget.ReservedBytes > baseline + 16384);
                    if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                else
                {
                    Assert.Equal(outcome == "refusal" ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                        result.FailureAttribution?.Reason);
                    Assert.Empty(result.StandardOutput);
                }
            }
            finally
            {
                cancellation.Cancel();
                result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
                result.Dispose();
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("allocator", false)] [InlineData("allocator", true)]
    [InlineData("initializer", false)] [InlineData("initializer", true)]
    [InlineData("descriptor", false)] [InlineData("descriptor", true)]
    public async Task PendingConstructionRetainsArgumentsAndDrainsCleanup(string route, bool cancel)
    {
        var source = Prelude + "\nclass C:\n def __new__(cls,value):\n  global payload\n  payload=[]\n  print('new')\n  with open('/new') as source:source.read()\n  return object.__new__(cls)\n" +
            (route == "descriptor" ? " __init__=D()\n" : " def __init__(self,value):initialize(self,value)\n") +
            "\nreturn C(payload).value\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            foreach (var path in new[] { "/new", "/init", "/bind" }) host.SeedFile(path, "ready");
            var entered = cancel ? host.PauseReadUntilCancellation(route switch
            { "allocator" => "/new", "descriptor" => "/bind", _ => "/init" }) : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline + 8192);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel)
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.Equal(route switch
                    { "allocator" => "new\n", "descriptor" => "new\nbind\n", _ => "new\ninit\n" }, result.StandardOutput);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(route == "descriptor" ? "new\nbind\ninit\n" : "new\ninit\n", result.StandardOutput);
                    var values = Assert.IsType<List<object?>>(result.ReturnValue);
                    Assert.Equal("kept", values[0]);
                    Assert.Equal(65537, Assert.IsType<BigInteger>(values[1]).GetBitLength());
                    Assert.True(budget.ReservedBytes > baseline + 8192);
                    Assert.True(host.CompletedAsynchronously > 0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string Prelude = """
        payload=['kept',1<<65536]
        def initialize(instance,value):
         print('init')
         assert value[0]=='kept' and value[1].bit_length()==65537 and payload==[]
         with open('/init') as source:source.read()
         instance.value=value
        class D:
         def __get__(self,instance,owner):
          assert instance is not None
          print('bind')
          with open('/bind') as source:source.read()
          def bind(value):initialize(instance,value)
          return bind
        """;
}
