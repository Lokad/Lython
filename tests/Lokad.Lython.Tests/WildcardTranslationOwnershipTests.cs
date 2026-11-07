using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class WildcardTranslationOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(120, 5, 12, 3);

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task SeparatorScanningObservesCancellationAndFinishesBeforeRefund(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var pattern = PyString.FromString(new string('a', 4096), context.MemoryGovernor, Span);
            var separators = PyString.FromString(new string('κ', 32768), context.MemoryGovernor, Span);
            var builtin = Assert.IsAssignableFrom<PyModule>(LythonRuntime.ResolveBuiltinModule("glob", context));
            Assert.True(builtin.TryGetMember("translate", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            var before = budget.ReservedBytes;
            hook.AfterGrowth = bytes => { if (bytes >= 4096) entered.Set(); };
            var observedEntry = false;
            var controller = new Thread(() =>
            {
                observedEntry = entered.Wait(TimeSpan.FromSeconds(5));
                if (observedEntry) Thread.Sleep(20);
                cancellation.Cancel();
            }) { IsBackground = true };
            controller.Start();
            var pending = Task.Factory.StartNew(() =>
            {
                CallArgumentValue[] arguments = [CallArgumentValue.Positional(pattern), CallArgumentValue.Keyword("seps", separators)];
                return asynchronous ? callable.InvokeAsync(arguments, Span, context).AsTask().GetAwaiter().GetResult()
                    : callable.Invoke(arguments, Span, context);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                if (error is LythonRuntimeException runtime)
                    Assert.Equal(LythonFailureReason.Cancelled, runtime.Attribution?.Reason);
                else Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
                Assert.True(controller.Join(TimeSpan.FromSeconds(5)), "Cancellation controller did not finish.");
                Assert.True(observedEntry, "Translation never reached admitted construction.");
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(4096, pattern.Utf8Bytes.Length);
                Assert.Equal(65536, separators.Utf8Bytes.Length);
            }
            finally
            {
                cancellation.Cancel();
                entered.Set();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
                catch (LythonRuntimeException error) when (error.Attribution?.Reason == LythonFailureReason.Cancelled) { }
                finally
                {
                    Assert.True(controller.Join(TimeSpan.FromSeconds(5)), "Cancellation cleanup did not finish.");
                    context.State.CloseResources();
                }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("fnmatch", false, 0), InlineData("fnmatch", true, 0)]
    [InlineData("glob", false, 0), InlineData("glob", true, 0)]
    [InlineData("fnmatch", false, 1), InlineData("fnmatch", true, 1)]
    [InlineData("glob", false, 1), InlineData("glob", true, 1)]
    [InlineData("fnmatch", false, 2), InlineData("fnmatch", true, 2)]
    [InlineData("glob", false, 2), InlineData("glob", true, 2)]
    [InlineData("fnmatch", false, 3), InlineData("fnmatch", true, 3)]
    [InlineData("glob", false, 3), InlineData("glob", true, 3)]
    public async Task TranslationKeepsOwnersAndStopsDuringConstruction(string module, bool asynchronous, int failureKind)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var pattern = PyString.FromString(string.Concat(Enumerable.Repeat("é😀[α-ω]?/", 512)), context.MemoryGovernor, Span);
            var builtin = Assert.IsAssignableFrom<PyModule>(LythonRuntime.ResolveBuiltinModule(module, context));
            Assert.True(builtin.TryGetMember("translate", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            var before = budget.ReservedBytes;
            var reached = false;
            LythonRuntimeException? first = null;
            hook.AfterGrowth = bytes =>
            {
                if (bytes < 4096) return;
                hook.AfterGrowth = null;
                reached = true;
                Assert.True(budget.ReservedBytes > before);
                if (failureKind == 1) cancellation.Cancel();
                if (failureKind == 2)
                    first = context.MemoryGovernor.RefuseAdmission("controlled wildcard refusal", 777, Span);
            };
            if (failureKind == 3) hook.RefuseGrowth = bytes => { reached = true; return true; };
            try
            {
                async Task<object> Translate() => asynchronous
                    ? await callable.InvokeAsync([CallArgumentValue.Positional(pattern)], Span, context)
                    : callable.Invoke([CallArgumentValue.Positional(pattern)], Span, context);
                if (failureKind == 0)
                {
                    var translated = Assert.IsType<PyString>(await Translate());
                    Assert.StartsWith("(?s:", translated.AsString());
                    Assert.EndsWith(")\\Z", translated.AsString());
                    Assert.True(budget.ReservedBytes > before);
                    Assert.Contains("é😀[α-ω]", translated.AsString());
                }
                else
                {
                    var error = await Assert.ThrowsAnyAsync<Exception>(Translate);
                    if (failureKind == 1)
                    {
                        if (error is LythonRuntimeException runtime)
                            Assert.Equal(LythonFailureReason.Cancelled, runtime.Attribution?.Reason);
                        else Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
                    }
                    else
                    {
                        var refused = Assert.IsType<LythonRuntimeException>(error);
                        Assert.Same(context.MemoryGovernor.AdmissionFailure, refused);
                        if (failureKind == 2) Assert.Same(first, refused);
                    }
                    Assert.Equal(before, budget.ReservedBytes);
                }
                Assert.True(reached);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.StartsWith("é😀[α-ω]?/", pattern.AsString());
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(budget.LimitBytes!.Value, out var reusable));
        reusable.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
