using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG03: hashlib digest objects own their fixed 96B state at construction and
/// copy, stream input without retaining it, and own their name strings, so
/// retained digests and names accumulate instead of riding the source
/// budget-free. Per-algorithm state does not scale with block size.
/// </summary>
public sealed class HashlibAccountingScenarioTests
{
    // 20k retained digests own 96B plus a 16B list slot each (2.4MB), so they
    // trip 2MB in both modes, locking the factory rate. Input bytes stream
    // through the hasher instead of accumulating.
    private const long DigestBudgetBytes = 2097152;
    // 20k retained names own ~130B each on top of factory transients, so they
    // fit 3.5MB pre-fix and trip post-fix.
    private const long NameBudgetBytes = 3670016;

    [Fact]
    public async Task ManyRetainedDigestsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import hashlib
            objs = []
            i = 0
            while i < 20000:
                objs.append(hashlib.md5())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DigestBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= DigestBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= DigestBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedSizedDigestsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import hashlib
            objs = []
            i = 0
            while i < 20000:
                objs.append(hashlib.md5(b"y"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DigestBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= DigestBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= DigestBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedNamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import hashlib
            objs = []
            i = 0
            while i < 20000:
                objs.append(hashlib.md5().name)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = NameBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= NameBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= NameBudgetBytes);
    }

    [Fact]
    public async Task HashlibValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            import hashlib
            h = hashlib.md5(b"abc")
            g = hashlib.sha256()
            return [h.name, h.hexdigest(), g.name, g.digest_size]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "md5", "900150983cd24fb0d6963f7d28e17f72", "sha256", new System.Numerics.BigInteger(32) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
