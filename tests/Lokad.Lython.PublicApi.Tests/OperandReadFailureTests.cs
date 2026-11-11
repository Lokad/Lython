using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OperandReadFailureTests
{
    [Theory]
    [InlineData("for left in []:pass\nfor right in []:pass\nleft + right", "left", "NameError")]
    [InlineData("for left in []:pass\nfor right in []:pass\nleft[right]", "left", "NameError")]
    [InlineData("class Probe:\n def __getitem__(self,key):print('called')\nleft=Probe()\nfor missing in []:pass\nleft[missing]", "missing", "NameError")]
    [InlineData("for missing in []:pass\nmissing.value", "missing", "NameError")]
    [InlineData("for missing in []:pass\nfor item in missing:pass", "missing", "NameError")]
    [InlineData("for missing in []:pass\nif missing:print('called')", "missing", "NameError")]
    [InlineData("def fail():\n for left in []:pass\n for right in []:pass\n return left + right\nfail()", "left", "UnboundLocalError")]
    [InlineData("def fail():\n for missing in []:pass\n return missing.value\nfail()", "missing", "UnboundLocalError")]
    [InlineData("def fail():\n for missing in []:pass\n for item in missing:pass\nfail()", "missing", "UnboundLocalError")]
    [InlineData("def fail():\n for missing in []:pass\n if missing:print('called')\nfail()", "missing", "UnboundLocalError")]
    [InlineData("for value in []:pass\nfor receiver in []:pass\nfor key in []:pass\nreceiver[key]=value", "value", "NameError")]
    [InlineData("value=1\nfor receiver in []:pass\nfor key in []:pass\nreceiver[key]=value", "receiver", "NameError")]
    [InlineData("value=1\nreceiver={}\nfor key in []:pass\nreceiver[key]=value", "key", "NameError")]
    [InlineData("for value in []:pass\n-value", "value", "NameError")]
    [InlineData("def fail():\n for value in []:pass\n return value\nfail()", "value", "UnboundLocalError")]
    [InlineData("def target(*values):print('called')\ndef fail():\n for missing in []:pass\n return target(1,2,3,4,missing,6)\nfail()", "missing", "UnboundLocalError")]
    [InlineData("def target(*values):print('called')\ndef fail():\n for left in []:pass\n for right in []:pass\n return target(left,1,2,3,right,6)\nfail()", "left", "UnboundLocalError")]
    [InlineData("def outer():\n left=1\n del left\n right=2\n del right\n def fail():return left+right\n return fail()\nouter()", "left", "NameError")]
    [InlineData("def outer():\n value=1\n del value\n def fail():return -value\n return fail()\nouter()", "value", "NameError")]
    [InlineData("def outer():\n value={}\n del value\n def fail():return value[0]\n return fail()\nouter()", "value", "NameError")]
    [InlineData("def outer():\n value=[]\n del value\n def fail():\n  for item in value:print('called')\n return fail()\nouter()", "value", "NameError")]
    [InlineData("def outer():\n value=1\n del value\n def fail():\n  if value:print('called')\n return fail()\nouter()", "value", "NameError")]
    [InlineData("def outer():\n value=1\n del value\n def fail():return value\n return fail()\nouter()", "value", "NameError")]
    public async Task FailedReadsKeepTheirOriginalSpanAndPrecedeProtocols(string source, string name, string exceptionType)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                    : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
                {
                    Assert.False(result.Success);
                    var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
                    Assert.Equal(exceptionType, failure.ExceptionType);
                    Assert.Contains("'" + name + "'", failure.Message);
                    var span = Assert.IsType<LythonSourceSpan>(failure.Span);
                    Assert.Equal(source.LastIndexOf(name, StringComparison.Ordinal), span.Start);
                    Assert.Equal(name, source.Substring(span.Start, span.Length));
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
