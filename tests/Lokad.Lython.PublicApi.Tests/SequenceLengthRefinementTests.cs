using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceLengthRefinementTests
{
    [Fact]
    public void FalseLengthBranch_SuppressesImpossibleLiteralIndexDiagnostics()
    {
        var compiled = new LythonEngine().Compile(
            """
heap = []
limit = 1
if len(heap) < limit:
    marker = "below"
else:
    marker = heap[0]

text = ""
if 0 < len(text):
    marker = text[0]

payload = b""
if len(payload) >= 1:
    marker = payload[-1]

values = ()
if len(values) != 0:
    marker = values[-1]
""");

        Assert.True(compiled.IsValid, Describe(compiled));
    }

    [Fact]
    public void ZeroLengthBranches_ProvePositiveAndNegativeIndexesInvalid()
    {
        var compiled = new LythonEngine().Compile(
            """
direct = [value for value in range(3)]
if len(direct) == 0:
    invalid_direct = direct[0]

reversed_items = [value for value in range(3)]
if 0 >= len(reversed_items):
    invalid_reversed = reversed_items[-1]

negated = [value for value in range(3)]
if not (len(negated) > 0):
    invalid_negated = negated[0]

from collections import deque
queued = deque([1])
if len(queued) == 0:
    invalid_deque = queued[-1]
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(4, compiled.Diagnostics.Count(d => d.Code == "LA3117"));
    }

    [Fact]
    public void MutationThroughAnAlias_WidensStaleLiteralLengths()
    {
        var compiled = new LythonEngine().Compile(
            """
items = [1]
alias = items
items.clear()
first = alias[0]

other = [1]
other_alias = other
other[0] = 2
unknown = other_alias[5]
""");

        Assert.True(compiled.IsValid, Describe(compiled));
    }

    [Fact]
    public void FalseChainedComparison_DoesNotDiscardAnUnknownFailurePath()
    {
        var compiled = new LythonEngine().Compile(
            """
def inspect(flag):
    items = [1, 2] if flag else [3]
    if flag < len(items) > 0:
        return None
    return items[0]
""");

        Assert.True(compiled.IsValid, Describe(compiled));
    }

    [Fact]
    public void LiteralOutOfRangeDiagnostics_IncludeNegativeIndexes()
    {
        var compiled = new LythonEngine().Compile(
            """
[][0]
()[-1]
""[-1]
b""[-1]
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(4, compiled.Diagnostics.Count(d => d.Code == "LA3117"));
    }

    [Fact]
    public void RuntimeLengthGuards_WorkForPositiveAndNegativeIndexes()
    {
        const string source = """
def edge(items):
    if len(items) < 1:
        return "empty"
    return str(items[0]) + ":" + str(items[-1])

return edge([]) + "|" + edge([2, 3])
""";

        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, Describe(compiled));

        var result = compiled.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("empty|2:3", result.ReturnValue);
    }

    private static string Describe(LythonCompiledScript compiled)
        => string.Join(" | ", compiled.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
}

