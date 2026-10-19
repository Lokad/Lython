using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutableLocalImportTests
{
    private static readonly (string Module, string Entry, string Expected)[] Cases =
    [
        ("""
        value=3
        def factory(seed=2):
            def inner(n):return value+seed+n
            return inner
        escaped=factory()
        value=7
        declared:int=1
        """, """
        import selected
        from selected import escaped
        import selected as again
        print(selected is again,escaped is selected.escaped,escaped(4),selected.declared,
              selected.__name__,selected.__file__,selected.__annotations__['declared'] is int)
        """, "True True 13 1 selected /selected.py True\n"),
        ("""
        events=[]
        def echo[T](value:T)->T:return value
        class Box:
            def read(self,n):return echo(n)
        def stream():
            for i in range(3):
                try:yield Box().read(i)
                finally:events.append(i)
        squares=[x*x for x in range(3)]
        lambda_value=lambda value:echo(value+1)
        """, """
        import selected as m
        print(list(m.stream()),m.events,m.squares,m.lambda_value(8),
              m.echo.__type_params__[0].__name__,
              m.echo.__annotations__['return'] is m.echo.__type_params__[0])
        """, "[0, 1, 2] [0, 1, 2] [0, 1, 4] 9 T True\n"),
        ("""
        state=0
        def read():return state
        first=read()
        state=9
        second=read()
        def mutate():
            global state
            state+=1
            return read()
        """, """
        import selected as m
        print(m.first,m.second,m.read(),m.mutate(),m.read())
        """, "0 9 9 10 10\n"),
        ("""
        events=[]
        with open('/payload.txt') as source:
            for line in source:
                try:
                    if line.strip()=='skip':continue
                    events.append(line.strip())
                    if line.startswith('stop'):break
                finally:events.append('finally')
        seen='|'.join(events)
        def read():
            with open('/payload.txt') as source:return source.readline().strip()
        """, "import selected\nprint(selected.seen,selected.read())",
            "finally|keep|finally|stop|finally skip\n"),
    ];

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task ImportedCodePreservesExportsClosuresMetadataAndEffects(bool asynchronous, int scenario)
    {
        var (module, entry, expected) = Cases[scenario];
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var compiled = new LythonEngine().Compile(entry, budget);
        Assert.True(compiled.IsValid, string.Join(";", compiled.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
        Seed(host, "/selected.py", module);
        Seed(host, "/payload.txt", "skip\nkeep\nstop\nlater\n");
        var options = Options();
        using (var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            if (host is DelayedLythonHost delayed) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        compiled.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "marker=0\nraise ValueError('boom')", "ValueError", "/selected.py")]
    [InlineData(true, "marker=0\nraise ValueError('boom')", "ValueError", "/selected.py")]
    [InlineData(false, "return 7", "RuntimeError", "/main.py")]
    [InlineData(true, "return 7", "RuntimeError", "/main.py")]
    [InlineData(false, "return", "RuntimeError", "/main.py")]
    [InlineData(true, "return", "RuntimeError", "/main.py")]
    [InlineData(false, "return None", "RuntimeError", "/main.py")]
    [InlineData(true, "return None", "RuntimeError", "/main.py")]
    [InlineData(false, "break", "SyntaxError", "/main.py")]
    [InlineData(true, "break", "SyntaxError", "/main.py")]
    public async Task ImportFailuresKeepPythonErrorsAndTheirSourcePath(
        bool asynchronous, string module, string exceptionType, string sourcePath)
    {
        ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
        Seed(host, "/selected.py", module);
        using var compiled = new LythonEngine().Compile("import selected");
        var options = Options();
        using var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Equal(sourcePath, result.Failure?.SourcePath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexecutedModuleReturnDoesNotBecomeAnImportFailure(bool asynchronous)
    {
        ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
        Seed(host, "/selected.py", "if False:return\nvalue=7");
        using var compiled = new LythonEngine().Compile("import selected\nreturn selected.value");
        using var result = asynchronous ? await compiled.RunAsync(host, Options()) : compiled.Run(host, Options());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(7), result.ReturnValue);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ImportCancellationAndRefusalStopGuestWorkAndReleaseOwners(bool asynchronous, bool refusal)
    {
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var compiled = new LythonEngine().Compile(
            "try:\n import selected\nexcept BaseException:print('caught')\nprint('after')", budget);
        Assert.True(compiled.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
        Seed(host, "/selected.py", "print('entered')\n" + (refusal ? "value='x'*1000000" : "while True:pass"));
        if (!refusal)
        {
            if (host is DelayedLythonHost delayed) delayed.OnStandardOutputWrite = cancellation.Cancel;
            else ((MockLythonHost)host).OnStandardOutputWrite = cancellation.Cancel;
        }
        var options = new LythonRunOptions
        {
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "selected" },
            SourcePath = "/main.py",
            CancellationToken = cancellation.Token,
            MemoryBudget = new LythonMemoryBudget(65536)
        };
        using (var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal(refusal ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                result.FailureAttribution?.Reason);
            Assert.StartsWith("entered", result.StandardOutput);
            Assert.DoesNotContain("caught", result.StandardOutput);
            Assert.DoesNotContain("after", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    private static LythonRunOptions Options() => new()
    {
        AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "selected" },
        SourcePath = "/main.py"
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModuleAttributesAndFunctionGlobalsStayInOneNamespace(bool asynchronous)
    {
        ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
        Seed(host, "/selected.py", "value=1\ndef get():return value\ndef change(n):\n global value\n value=n\ndef scope():return globals()");
        using var compiled = new LythonEngine().Compile("""
            import selected as m
            print(m.value,m.get())
            m.change(2)
            print(m.value,m.get())
            m.value=3
            print(m.value,m.get())
            scope=m.scope()
            scope['value']=4
            print(m.value,m.get())
            m.value=5
            print(scope['value'],m.get())
            """);
        using var result = asynchronous ? await compiled.RunAsync(host, Options()) : compiled.Run(host, Options());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1 1\n2 2\n3 3\n4 4\n5 5\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitModuleBindingsCanShadowBuiltins(bool asynchronous)
    {
        ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
        Seed(host, "/selected.py", "len=3\nstr=4");
        using var compiled = new LythonEngine().Compile("import selected as m\nfrom selected import len as length\nprint(m.len,m.str,length)");
        using var result = asynchronous ? await compiled.RunAsync(host, Options()) : compiled.Run(host, Options());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3 4 3\n", result.StandardOutput);
    }

    private static void Seed(ILythonHost host, string path, string text)
    {
        if (host is DelayedLythonHost delayed) delayed.SeedFile(path, text);
        else ((MockLythonHost)host).SeedFile(path, text);
    }
}
