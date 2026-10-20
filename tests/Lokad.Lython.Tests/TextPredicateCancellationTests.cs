using System.Reflection;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TextPredicateCancellationTests
{
    private static readonly LythonSourceSpan Span = new(10, 4, 2, 3);

    [Fact]
    public void CancellationRequestedByPredicateStopsTheActiveScan()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(128 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { CancellationToken = cancellation.Token, MemoryBudget = owner });
            var value = PyString.FromString(new string('a', 1024), context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            var scan = typeof(PyStringOps).GetMethod("CheckAllRunes", BindingFlags.Static | BindingFlags.NonPublic)!
                .CreateDelegate<Func<PyString, Func<Rune, bool>, LythonRuntime.ExecutionContext?, LythonSourceSpan?, bool>>();
            bool Predicate(Rune rune)
            {
                cancellation.Cancel();
                return Rune.IsLetter(rune);
            }
            Assert.Null(PyStructuralGuard.AmbientContext);
            var failure = Assert.Throws<LythonRuntimeException>(() => scan(value, Predicate, context, Span));
            Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            Assert.Equal(Span, failure.Span);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(new string('a', 1024), value.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("islower", 'a')]
    [InlineData("isupper", 'A')]
    [InlineData("isalpha", 'a')]
    [InlineData("isalnum", 'a')]
    [InlineData("isspace", ' ')]
    [InlineData("isascii", 'a')]
    [InlineData("isdecimal", '1')]
    [InlineData("isdigit", '1')]
    [InlineData("isnumeric", '1')]
    [InlineData("isprintable", 'a')]
    [InlineData("isidentifier", 'a')]
    [InlineData("istitle", 'A')]
    public void RuntimePredicateUsesItsCurrentContextWithoutAmbientState(string method, char scalar)
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(128 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { CancellationToken = cancellation.Token, MemoryBudget = owner });
            var value = PyString.FromString(new string(scalar, 1024), context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            var operation = LythonRuntime.StringMembers.GetNoArgumentsOperation(method)!;
            Assert.Null(PyStructuralGuard.AmbientContext);
            cancellation.Cancel();
            var failure = Assert.Throws<LythonRuntimeException>(() => operation(value, Span, context));
            Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            Assert.Equal(Span, failure.Span);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
