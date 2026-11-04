using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CopyCallerContextTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("{key: 1}")]
    [InlineData("{key}")]
    [InlineData("defaultdict(None, {key: 1})")]
    [InlineData("Counter({key: 1})")]
    public void DeepCopyUsesTheCallerAllowanceForProtocolKeys(string expression)
    {
        var source = "import copy\nfrom collections import defaultdict,Counter\nclass Key:\n def __hash__(self):return 1\n def __deepcopy__(self,memo):return self\nkey=Key()\nsource=" + expression + "\ncopier=copy.deepcopy";
        WithExpiredAmbient(source, context =>
        {
            var copier = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["copier"]);
            var original = context.Variables["source"];
            var copied = copier.Invoke([CallArgumentValue.Positional(original)], Span, context);
            Assert.NotSame(original, copied);
            var key = context.Variables["key"];
            Assert.Same(key, copied switch
            {
                PyDict dict => Assert.Single(dict).Key,
                PySet set => Assert.Single(set),
                PyDefaultDict dict => Assert.Single(dict.Items).Key,
                PyCounter counter => Assert.Single(counter.Items).Key,
                _ => throw new InvalidOperationException("Expected a copied protocol container.")
            });
        });
    }

    [Fact]
    public void ExternalMemoProtocolComparisonsUseTheCallerAllowance()
    {
        const string source = "import copy\nsource=[1]\nclass Key:\n def __hash__(self):return hash(id(source))\n def __eq__(self,other):return False\nmemo={Key():None}\ncopier=copy.deepcopy";
        WithExpiredAmbient(source, context =>
        {
            var copier = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["copier"]);
            var original = Assert.IsType<PyList>(context.Variables["source"]);
            var copied = Assert.IsType<PyList>(copier.Invoke([
                CallArgumentValue.Positional(original), CallArgumentValue.Positional(context.Variables["memo"])
            ], Span, context));
            Assert.NotSame(original, copied);
            Assert.Equal(original[0], Assert.Single(copied));
        });
    }

    private static void WithExpiredAmbient(string source, Action<LythonRuntime.ExecutionContext> operation)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var frontend = LythonFrontend.Compile(source);
        Assert.NotNull(frontend.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script)).EntryPoint;
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

        var expiredBudget = new LythonMemoryBudget(1024 * 1024);
        using var expired = expiredBudget.CreateScope();
        var unrelated = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = expired });
        expired.Dispose();
        // Context-free native operations can encounter provenance left by an
        // unrelated caller. The explicit operation context must take precedence.
        // This scope is synchronous and restored on the same thread.
        using (PyStructuralGuard.PushAmbient(unrelated, Span)) operation(context);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, expiredBudget.ReservedBytes);
    }
}
