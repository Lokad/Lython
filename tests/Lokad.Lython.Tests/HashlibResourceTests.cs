using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class HashlibResourceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OriginalsAndCopiesAreDisposedAtTheExecutionBoundary(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("import hashlib\nh=hashlib.sha256(b'abc')\nreturn [h,h.copy()]");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(32768);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var value = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var hashes = Assert.IsType<PyList>(value);
        var span = new LythonSourceSpan(0, 0, 1, 1);
        var digests = new List<LythonRuntime.ICallable>();
        try
        {
            for (var i = 0; i < hashes.Count; i++)
            {
                var hash = Assert.IsAssignableFrom<IPyDynamicAttributes>(hashes.GetIndex(i));
                Assert.True(hash.TryGetMember("digest", out var member));
                var digest = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
                Assert.Equal(32, Assert.IsType<PyBytes>(digest.Invoke([], span, context)).Length);
                digests.Add(digest);
            }
        }
        finally { context.State.CloseHashes(); }
        foreach (var digest in digests) Assert.Throws<ObjectDisposedException>(() => digest.Invoke([], span, context));
        // Disposal does not arbitrarily refund still-retained batch storage.
        Assert.True(budget.ReservedBytes > 0);
        context.State.CloseHashes();
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
