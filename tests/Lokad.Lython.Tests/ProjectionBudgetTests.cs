using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// I01: projection reservations are atomic like execution reservations: a
// denial throws without moving the counter, so the reported peak only ever
// reflects accepted charges.
public sealed class ProjectionBudgetTests
{
    [Fact]
    public void DeniedReserveLeavesCounterUnchanged()
    {
        var budget = new ProjectionBudget(100);
        budget.Reserve(60);
        Assert.Equal(60, budget.CurrentBytes);
        Assert.Throws<ProjectionException>(() => budget.Reserve(50));
        Assert.Equal(60, budget.CurrentBytes);
        budget.Reserve(40);
        Assert.Equal(100, budget.CurrentBytes);
        Assert.Throws<ProjectionException>(() => budget.Reserve(1));
        Assert.Equal(100, budget.CurrentBytes);
    }

    [Fact]
    public void TinyBudgetsDenyFirstCharge()
    {
        var zero = new ProjectionBudget(0);
        Assert.Throws<ProjectionException>(() => zero.Reserve(1));
        Assert.Equal(0, zero.CurrentBytes);

        var one = new ProjectionBudget(1);
        one.Reserve(1);
        Assert.Equal(1, one.CurrentBytes);
        Assert.Throws<ProjectionException>(() => one.Reserve(1));
        Assert.Equal(1, one.CurrentBytes);
    }

    [Fact]
    public void OversizedRequestDeniedWithoutOverflow()
    {
        var bounded = new ProjectionBudget(100);
        Assert.Throws<ProjectionException>(() => bounded.Reserve(long.MaxValue));
        Assert.Equal(0, bounded.CurrentBytes);

        var unbounded = new ProjectionBudget(null);
        unbounded.Reserve(long.MaxValue);
        Assert.Equal(long.MaxValue, unbounded.CurrentBytes);
        Assert.Throws<ProjectionException>(() => unbounded.Reserve(1));
        Assert.Equal(long.MaxValue, unbounded.CurrentBytes);
    }

    [Fact]
    public void NonPositiveReservesAreNoOps()
    {
        var budget = new ProjectionBudget(0);
        budget.Reserve(0);
        budget.Reserve(-5);
        Assert.Equal(0, budget.CurrentBytes);
    }
}