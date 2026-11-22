using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class ExecutableJumpTargetTests
{
    [Fact]
    public void Cycles_PreserveEachEntryAndIncomingTail()
    {
        Assert.Equal([0, 1, 2, 1, 2, 0, 6, 6],
            Resolve([0, 2, 1, 1, 2, 0, -1, 6]));
    }

    [Fact]
    public void AllSmallGraphs_MatchIndependentPerEntryWalk()
    {
        Assert.Empty(Resolve([]));
        for (var size = 1; size <= 5; size++)
        {
            var successors = Enumerable.Repeat(-1, size).ToArray();
            do
            {
                var actual = Resolve(successors);
                for (var start = 0; start < size; start++)
                {
                    var seen = new HashSet<int>();
                    var expected = start;
                    while (seen.Add(expected) && successors[expected] != -1)
                        expected = successors[expected];
                    Assert.Equal(expected, actual[start]);
                }
            } while (Advance(successors, size));
        }

        static bool Advance(int[] successors, int size)
        {
            for (var i = 0; i < size; i++)
            {
                if (++successors[i] < size)
                    return true;
                successors[i] = -1;
            }
            return false;
        }
    }

    [Fact]
    public void LongChain_IsResolvedWithoutRecursiveCalls()
    {
        const int count = 20_000;
        var successors = Enumerable.Range(1, count).ToArray();
        successors[^1] = -1;
        Assert.All(Resolve(successors), value => Assert.Equal(count - 1, value));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    public void InvalidSuccessor_IsACompilerFailure(int successor)
    {
        Assert.Throws<IndexOutOfRangeException>(() => Resolve([successor]));
    }

    [Fact]
    public void ProtectedEntry_RemainsAnEntryWhenItOnlyJumps()
    {
        var region = new ExecutableExceptionRegion(1, 2, 2, null, null, null);
        Assert.Equal([1, 1, 2], Resolve([1, 2, -1], [region]));
    }

    private static int[] Resolve(int[] successors, IReadOnlyList<ExecutableExceptionRegion>? regions = null)
    {
        var blocks = new List<List<ExecutableInstruction>>(successors.Length);
        var span = new LythonSourceSpan(1, 1, 1, 1);
        foreach (var successor in successors)
            blocks.Add(successor == -1 ? [] : [ExecutableInstruction.Jump(successor, span)]);
        return ExecutableScript.ResolveJumpTargets(blocks, regions ?? []);
    }
}
