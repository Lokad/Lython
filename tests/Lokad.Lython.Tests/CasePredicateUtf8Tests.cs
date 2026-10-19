using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CasePredicateUtf8Tests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData("islower", false)]
    [InlineData("islower", true)]
    [InlineData("isupper", false)]
    [InlineData("isupper", true)]
    [InlineData("istitle", false)]
    [InlineData("istitle", true)]
    public void PredicateDoesNotMaterializeUtf16Input(string method, bool earlyFailure)
    {
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var text = method == "istitle" ? string.Concat(Enumerable.Repeat("Λλ ", 1024))
                : new string(method == "islower" ? 'λ' : 'Λ', 2048);
            if (earlyFailure) text = (method == "islower" ? "Λ" : "λ") + text;
            var value = PyString.FromString(text, context.MemoryGovernor, Span);
            Assert.True(PyMemberAccess.TryResolve(value, method, context, Span, out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            var baseline = budget.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = callable.Invoke([], Span, context);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(!earlyFailure, Assert.IsType<bool>(result));
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (measured) Assert.True(allocated < 2048, $"{method} allocated {allocated} bytes.");
        }
    }

    [Theory]
    [InlineData("islower", false, false)]
    [InlineData("islower", false, true)]
    [InlineData("isupper", false, false)]
    [InlineData("isupper", false, true)]
    [InlineData("istitle", true, false)]
    [InlineData("istitle", true, true)]
    [InlineData("isalpha", true, false)]
    [InlineData("isalpha", true, true)]
    [InlineData("isdigit", false, false)]
    [InlineData("isdigit", false, true)]
    [InlineData("isalnum", true, false)]
    [InlineData("isalnum", true, true)]
    [InlineData("isascii", false, false)]
    [InlineData("isascii", false, true)]
    [InlineData("isdecimal", false, false)]
    [InlineData("isdecimal", false, true)]
    [InlineData("isidentifier", true, false)]
    [InlineData("isidentifier", true, true)]
    [InlineData("isnumeric", false, false)]
    [InlineData("isnumeric", false, true)]
    [InlineData("isprintable", true, false)]
    [InlineData("isprintable", true, true)]
    [InlineData("isspace", false, false)]
    [InlineData("isspace", false, true)]
    public async Task NativePredicateCallPreservesResultsAndCleanup(string method, bool expected, bool asynchronous)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(
            LythonFrontend.Compile("return value." + method + "()").Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = Prepare(owner);
            Assert.Equal(expected, Assert.IsType<bool>(await Execute(context)));
            Unwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var member = $"7,{6 + method.Length},1,8";
        var call = $"7,{8 + method.Length},1,8";

        LythonRuntime.ExecutionContext Prepare(ILythonMemoryBudget owner)
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner});
            context.Variables["value"] = PyString.FromString("Λλ", context.MemoryGovernor, Span);
            return context;
        }
        async ValueTask<object?> Execute(LythonRuntime.ExecutionContext context)
            => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
    }

    [Theory]
    [InlineData("islower")]
    [InlineData("isupper")]
    [InlineData("istitle")]
    public void CancelledInvocationKeepsInputsFundedAndUnwinds(string method)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var value = PyString.FromString("Λλ", context.MemoryGovernor, Span);
        Assert.True(PyMemberAccess.TryResolve(value, method, context, Span, out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var baseline = budget.ReservedBytes;
        cancellation.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([], Span, context));
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Same(Span, error.Span);
        Assert.Equal(baseline, budget.ReservedBytes);
        Unwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static void Unwound(LythonRuntime.ExecutionContext context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
