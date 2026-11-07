using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class ExecutableJumpTargetTests
{
    [Fact]
    public void Cycles_PreserveEachEntryAndIncomingTail()
    {
        Assert.Equal([0, 1, 2, 1, 2, 0, 6, 6],
            ExecutableScript.ResolveJumpTargets([0, 2, 1, 1, 2, 0, -1, 6]));
    }

    [Fact]
    public void AllSmallGraphs_MatchIndependentPerEntryWalk()
    {
        Assert.Empty(ExecutableScript.ResolveJumpTargets([]));
        for (var size = 1; size <= 5; size++)
        {
            var successors = Enumerable.Repeat(-1, size).ToArray();
            do
            {
                var actual = ExecutableScript.ResolveJumpTargets(successors);
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
        Assert.All(ExecutableScript.ResolveJumpTargets(successors), value => Assert.Equal(count - 1, value));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    public void InvalidSuccessor_IsACompilerFailure(int successor)
    {
        Assert.Throws<IndexOutOfRangeException>(() => ExecutableScript.ResolveJumpTargets([successor]));
    }
}
