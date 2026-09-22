using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N12 (statistics/difflib part): module-local tables honor guest __hash__/__eq__
// with first-seen ties; unhashability, protocol errors and tuple keys preserved.
public sealed class ContextualModuleKeyTests
{
    private const string Defs = "class E:\n    def __eq__(self, other):\n        return True\n    def __hash__(self):\n        return 7\n";

    private static async Task AssertBothModes(string source, string expectedOutput)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expectedOutput, sync.StandardOutput);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expectedOutput, asyncResult.StandardOutput);
    }

    [Fact]
    public async Task ModeKeepsFirstSeenTie()
        => await AssertBothModes(
            "import statistics\n" + Defs + "a = E()\nb = E()\nprint(statistics.mode([a, b, 1, 1]) is a)\n",
            "True\n");

    [Fact]
    public async Task MultimodePreservesFirstSeenOrder()
        => await AssertBothModes(
            "import statistics\n" + Defs + "a = E()\nb = E()\nm = statistics.multimode([1, a, b, 1])\nprint(len(m))\nprint(m[0] == 1)\nprint(m[1] is a)\n",
            "2\nTrue\nTrue\n");

    [Fact]
    public async Task DifflibRatioSeesEqualElements()
        => await AssertBothModes(
            "import difflib\n" + Defs + "print(difflib.SequenceMatcher(None, [E()], [E()]).ratio())\n",
            "1.0\n");

    [Fact]
    public async Task TupleKeysHashContextually()
        => await AssertBothModes(
            "import statistics\n" + Defs + "a = E()\nk1 = (a, 1)\nk2 = (E(), 1)\nprint(statistics.mode([k1, k2, (2, 3)]) is k1)\n",
            "True\n");

    [Fact]
    public async Task EqWithoutHashIsUnhashable()
    {
        const string code = "import statistics\nclass F:\n    def __eq__(self, other):\n        return True\nprint(statistics.mode([F(), F()]))\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("TypeError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("TypeError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task UnhashableSequenceFailsDifflib()
    {
        const string code = "import difflib\nm = difflib.SequenceMatcher(None, [], [])\nm.set_seq2([[1]])\nprint(\"no-error\")\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("TypeError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("TypeError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ProtocolErrorsPropagate()
        => await AssertBothModes(
            "import statistics\nclass G:\n    def __hash__(self):\n        return 7\n    def __eq__(self, other):\n        raise ValueError(\"boom\")\ntry:\n    statistics.mode([G(), G()])\nexcept ValueError:\n    print(\"caught\")\n",
            "caught\n");

    [Fact]
    public async Task SingleHashAndEqPerElement()
        => await AssertBothModes(
            "import statistics\ncalls = []\nclass H:\n    def __hash__(self):\n        calls.append(\"h\")\n        return 7\n    def __eq__(self, other):\n        calls.append(\"e\")\n        return True\na = H()\nb = H()\nprint(statistics.mode([a, b, 1, 1]) is a)\nprint(len([c for c in calls if c == \"h\"]))\nprint(len([c for c in calls if c == \"e\"]))\n",
            "True\n2\n1\n");
}
