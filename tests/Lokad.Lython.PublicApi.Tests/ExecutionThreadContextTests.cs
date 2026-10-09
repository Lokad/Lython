using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutionThreadContextTests
{
    private static readonly AsyncLocal<object?> Ambient = new();
    private sealed record Observation(object? AmbientValue, string Culture, string UiCulture, string? Principal, bool NoSyncContext);
    private static Observation Observe() => new(Ambient.Value, CultureInfo.CurrentCulture.Name,
        CultureInfo.CurrentUICulture.Name, Thread.CurrentPrincipal?.Identity?.Name, SynchronizationContext.Current is null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerContextMatchesFreshThreadBoundary(bool suppressFlow)
    {
        var original = Observe();
        var principal = Thread.CurrentPrincipal;
        var syncContext = SynchronizationContext.Current;
        try
        {
            Ambient.Value = new object();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("caller"), []);
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            var caller = Observe();
            if (suppressFlow)
            {
                using (System.Threading.ExecutionContext.SuppressFlow()) CheckBoundary();
            }
            else CheckBoundary();
            Assert.Equal(caller, Observe());
        }
        finally
        {
            Ambient.Value = original.AmbientValue;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(original.Culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(original.UiCulture);
            Thread.CurrentPrincipal = principal;
            SynchronizationContext.SetSynchronizationContext(syncContext);
        }
    }

    private static void CheckBoundary()
    {
        Observation? expected = null;
        var thread = new Thread(() => expected = Observe(), 16 * 1024 * 1024) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        var host = new MockLythonHost();
        Observation? actual = null;
        var captured = false;
        host.OnStandardOutputWrite = () =>
        {
            if (captured) return;
            captured = true;
            actual = Observe();
            Ambient.Value = new object();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("host-mutation"), []);
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        };
        var result = new LythonEngine().Compile("print('capture')").Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConsecutiveCallersAndSuppressedFlowDoNotInheritHostMutations()
    {
        var original = Ambient.Value;
        try
        {
            Ambient.Value = "first";
            CheckBoundary();
            Assert.Equal("first", Ambient.Value);
            Ambient.Value = "second";
            CheckBoundary();
            Assert.Equal("second", Ambient.Value);
            using (System.Threading.ExecutionContext.SuppressFlow()) CheckBoundary();
            Assert.Equal("second", Ambient.Value);
        }
        finally { Ambient.Value = original; }
    }

    [Fact]
    public async Task ConcurrentCallersKeepContextsSeparate()
    {
        using var barrier = new Barrier(4);
        var script = new LythonEngine().Compile("print('capture')");
        var tasks = Enumerable.Range(0, 4).Select(index => Task.Run(() =>
        {
            Ambient.Value = index;
            var host = new MockLythonHost();
            var captured = false;
            host.OnStandardOutputWrite = () =>
            {
                if (captured) return;
                captured = true;
                Assert.Equal(index, Ambient.Value);
                Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                Ambient.Value = "host-mutation";
            };
            var result = script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(index, Ambient.Value);
        }));
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task HostCanInvokeAnotherScriptSynchronously()
    {
        var child = new LythonEngine().Compile("return 7");
        var parent = new LythonEngine().Compile("print('nested')\nreturn 9");
        var host = new MockLythonHost();
        host.OnStandardOutputWrite = () => Assert.Equal(new System.Numerics.BigInteger(7), child.Run(new MockLythonHost()).ReturnValue);
        var result = await Task.Run(() => parent.Run(host)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(9), result.ReturnValue);
    }

    [Fact]
    public void FinishedRunsDoNotPinHostScriptResultOrCapturedContext()
    {
        var references = RunAndDrop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] RunAndDrop()
    {
        var previous = Ambient.Value;
        var payload = new object();
        var host = new MockLythonHost();
        var script = new LythonEngine().Compile("print('finished')\nreturn [1, 2, 3]");
        Ambient.Value = payload;
        try
        {
            var result = script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            return [new(host), new(script), new(result), new(payload)];
        }
        finally { Ambient.Value = previous; }
    }
}
