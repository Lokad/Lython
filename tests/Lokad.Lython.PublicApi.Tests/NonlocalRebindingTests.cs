using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NonlocalRebindingTests
{
    public static TheoryData<string, string, string> PythonGoldens => new()
    {
        { "deleted-and-rebound", "def outer():\n value=1\n def write(x):\n  nonlocal value\n  value=x\n def read():return value\n del value\n try:read()\n except NameError:print('missing')\n write(7)\n print(value,read())\n del value\n return write,read\nwrite,read=outer()\nwrite(19)\nprint(read())\n", "missing\n7 7\n19\n" },
        { "transitive-target", "def outer():\n value=1\n def middle():\n  def write(x):\n   nonlocal value\n   value=x\n  return write\n write=middle()\n del value\n write(7)\n print(value)\n del value\n write(11)\n print(value)\nouter()\n", "7\n11\n" },
        { "class-method-target", "def outer():\n value=1\n class Writer:\n  value=99\n  def write(self,x):\n   nonlocal value\n   value=x\n del value\n Writer().write(7)\n print(value,Writer.value)\nouter()\n", "7 99\n" },
        { "generator-target", "def outer():\n value=1\n def write():\n  nonlocal value\n  value=7\n  yield value\n g=write()\n del value\n print(next(g),value)\n g.close()\nouter()\n", "7 7\n" },
        { "handler-cleared-cell", "def outer():\n value=1\n def write():\n  nonlocal value\n  value=7\n try:raise ValueError()\n except ValueError as value:pass\n write()\n print(value)\nouter()\n", "7\n" },
        { "uninvoked-writer-runtime-error", "def outer():\n value=1\n def write():\n  nonlocal value\n  value=7\n del value\n try:print(value)\n except UnboundLocalError:print('unbound')\n try:del value\n except UnboundLocalError:print('still unbound')\nouter()\n", "unbound\nstill unbound\n" },
        { "read-only-nonlocal-still-unbound", "def outer():\n value=1\n def read():\n  nonlocal value\n  return value\n del value\n try:print(value)\n except UnboundLocalError:print('unbound')\n try:read()\n except NameError:print('free unbound')\nouter()\n", "unbound\nfree unbound\n" },
    };

    [Theory]
    [MemberData(nameof(PythonGoldens))]
    public async Task NonlocalTargetsUseRuntimeBindingAfterCallbacks(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("def outer():\n value=1\n def read():return value\n del value\n return value\n")]
    [InlineData("def outer():\n value=1\n def middle():\n  value=2\n  def write():\n   nonlocal value\n   value=3\n  return write\n del value\n return value\n")]
    [InlineData("def outer():\n value=1\n def middle():\n  global value\n  def write():\n   global value\n   value=3\n  return write\n del value\n return value\n")]
    public void ReadOnlyCapturesAndOtherTargetsKeepCertainUnboundDiagnostics(string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3146");
    }

    [Fact]
    public void AGlobalDeclarationBlocksNonlocalResolutionToAnOuterFunction()
    {
        using var script = new LythonEngine().Compile("""
            def outer():
                value = 1
                def middle():
                    global value
                    def write():
                        nonlocal value
                        value = 7
                del value
                return value
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3205");
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3146");
    }
}
