using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: fresh hash shells own lifetime like the other factory results do.
public sealed class HashlibLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task HashCtorDiscardCompletes()
        => await AssertCompletes(
            "import hashlib\nfor i in range(50000):\n    h = hashlib.md5(b'x' * 10)\nreturn 0\n", "0");

    [Fact]
    public async Task HashNewDiscardCompletes()
        => await AssertCompletes(
            "import hashlib\nfor i in range(50000):\n    h = hashlib.new('sha256', b'x' * 10)\nreturn 0\n", "0");

    [Fact]
    public async Task HashCopyDiscardCompletes()
        => await AssertCompletes(
            "import hashlib\nh = hashlib.md5(b'x' * 10)\nfor i in range(50000):\n    c = h.copy()\nreturn 0\n", "0");

    [Fact]
    public async Task HashCopyBehaves()
        => await AssertCompletes(
            "import hashlib\nh = hashlib.md5(b'abc')\nc = h.copy()\nc.update(b'd')\nreturn h.hexdigest() + ' ' + c.hexdigest()\n", "900150983cd24fb0d6963f7d28e17f72 e2fc714c4727ee9395f324cd2e7f331f");
}
