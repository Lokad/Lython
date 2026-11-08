using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FunctionLookupLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Theory]
    [InlineData("reenter")]
    [InlineData("cancel")]
    [InlineData("refuse")]
    [InlineData("reenter-cancel")]
    public void FirstAdmissionPreservesReentrantBackingAndRefundsOnlyPendingStorage(string outcome)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            {
                MemoryBudget = hook, CancellationToken = cancellation.Token,
            });
            var code = Compile("def read(row):\n return f\"{row['a']}:{row['b']}\"\nreturn read");
            var function = Assert.IsType<PyExecutableFunction>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            var before = budget.ReservedBytes;
            LythonRuntime.ExecutableLookupCache[]? inner = null;
            if (outcome == "refuse") hook.RefuseGrowth = _ => true;
            else hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                if (outcome.StartsWith("reenter", StringComparison.Ordinal)) inner = function.GetLookupCaches(Span);
                if (outcome != "reenter") cancellation.Cancel();
            };
            if (outcome == "reenter")
            {
                var outer = function.GetLookupCaches(Span);
                Assert.Same(inner, outer);
                var retained = budget.ReservedBytes;
                hook.RefuseGrowth = _ => true;
                Assert.Same(outer, function.GetLookupCaches(Span));
                Assert.Equal(retained, budget.ReservedBytes);
            }
            else
            {
                var error = Assert.Throws<LythonRuntimeException>(() => function.GetLookupCaches(Span));
                Assert.Equal(outcome == "refuse" ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                    error.Attribution?.Reason);
                if (outcome == "refuse") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            }
            if (inner is null) Assert.Equal(before, budget.ReservedBytes);
            else Assert.Equal(before + LythonRuntime.LookupCacheBytes(function.CodeObject.LookupCacheCount), budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterFieldMutationAfterFormattingAdmissionUsesActualSuspendingProtocol(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var immediate = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            immediate.SeedFile("/suffix.txt", "later");
            delayed.SeedFile("/suffix.txt", "later");
            var context = new LythonRuntime.ExecutionContext(asynchronous ? delayed : immediate, new() { MemoryBudget = hook });
            var code = Compile("""
                class Later:
                    def __format__(self, spec):
                        with open('/suffix.txt') as source:
                            return source.read()
                def render(row):
                    return f"{row['a']:<2048}:{row['b']}"
                return (render, Later())
                """);
            var definitions = Assert.IsType<PyTuple>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            var render = Assert.IsType<PyExecutableFunction>(definitions[0]);
            var firstKey = PyString.FromString("a", context.MemoryGovernor, Span);
            var laterKey = PyString.FromString("b", context.MemoryGovernor, Span);
            var row = new PyDict(context.MemoryGovernor, Span);
            row.SetItem(firstKey, PyString.FromString("left", context.MemoryGovernor, Span));
            row.SetItem(laterKey, PyString.FromString("old", context.MemoryGovernor, Span));
            Assert.NotNull(row.ShareRecordKeys(context, Span));
            var first = await Invoke();
            Assert.Equal("left".PadRight(2048) + ":old", first.AsString());
            var changed = false;
            hook.AfterGrowth = bytes =>
            {
                if (bytes < 2048) return;
                hook.AfterGrowth = null;
                row.SetItem(laterKey, definitions[1]);
                changed = true;
            };
            var second = await Invoke();
            Assert.True(changed);
            Assert.Equal("left".PadRight(2048) + ":later", second.AsString());
            Assert.Equal("left".PadRight(2048) + ":old", first.AsString());
            if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);

            async ValueTask<PyString> Invoke() => Assert.IsType<PyString>(asynchronous
                ? await render.InvokeAsync([CallArgumentValue.Positional(row)], Span, context)
                : render.Invoke([CallArgumentValue.Positional(row)], Span, context));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
    }
}
