using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryReadPrefixTests
{
    [Theory]
    [InlineData("direct", false)] [InlineData("direct", true)]
    [InlineData("bound", false)] [InlineData("bound", true)]
    [InlineData("subscript", false)] [InlineData("subscript", true)]
    public async Task ArgumentMutationInvalidatesNativeReadAndRetainsSelectedReceiver(string route, bool cancel)
    {
        var expression = route switch
        {
            "direct" => "mapping.get('target', prepare())",
            "bound" => "read('target', prepare())",
            _ => "mapping[prepare_key()]"
        };
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source + "\nresult=" + expression + "\nprint(result,len(mapping),len(retained))", budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/eq", "data");
            var entered = cancel ? host.PauseReadUntilCancellation("/eq") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel)
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.Equal("eq\n", result.StandardOutput);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("eq\nkept 0 1\n", result.StandardOutput);
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

    private const string Source = """
        mapping={}
        retained=mapping
        class Stored:
         def __hash__(self):return hash('target')
         def __eq__(self,other):
          print('eq')
          with open('/eq') as marker:marker.read()
          return other=='target'
        def prepare():
         global mapping
         retained[Stored()]='kept'
         mapping={}
         return 'fallback'
        def prepare_key():
         prepare()
         return 'target'
        read=mapping.get
        """;
}
