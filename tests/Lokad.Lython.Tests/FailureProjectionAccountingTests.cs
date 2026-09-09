using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG23: failure details ride the projection budget. Frame records alias
/// engine state, so only the frames array plus the message are charged; both
/// check fit first, so a failed projection never pushes the reported peak
/// past the budget.
/// </summary>
public sealed class FailureProjectionAccountingTests
{
    [Fact]
    public void ManyFramesOverrunWithoutPollutingPeak()
    {
        var budget = new ProjectionBudget(4096);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var exception = new LythonRuntimeException("ValueError", "deep", span);
        for (var i = 0; i < 500; i++)
        {
            exception.AddFrame("f", span);
        }

        // 32 + 16 x 500 exceeds the budget before the message is even read.
        Assert.Throws<ProjectionException>(() => RuntimeFailureProjection.ToPublicFailure(exception, budget));
        Assert.True(budget.CurrentBytes <= 4096);
    }

    [Fact]
    public void FewFramesFitWithExactPeak()
    {
        var budget = new ProjectionBudget(4096);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var exception = new LythonRuntimeException("ValueError", "abc", span);
        for (var i = 0; i < 3; i++)
        {
            exception.AddFrame("f", span);
        }

        var failure = RuntimeFailureProjection.ToPublicFailure(exception, budget);
        Assert.Equal("ValueError", failure.ExceptionType);
        Assert.Equal("abc", failure.Message);
        Assert.Equal(3, failure.StackTrace.Count);
        // Frames array 32 + 16 x 3 plus message 32 + 2 x 3.
        Assert.Equal(118L, budget.CurrentBytes);
    }
}
