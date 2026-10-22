using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ArgparseProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultsRetainAliasesAndAppendCopiesOnlyOnUse(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            p=argparse.ArgumentParser(add_help=False)
            original=[]
            constant={}
            p.add_argument('--x',default=original,choices=None)
            p.add_argument('--constant',action='store_const',const=constant)
            p.add_argument('--append',action='append')
            p.add_argument('--many',nargs='*')
            a=p.parse_args([])
            b=p.parse_args(['--constant'])
            print(a.x is original,a.x is b.x,b.constant is constant,a.append,a.many)
            a.x.append('v')
            print(original,b.x)
            q=argparse.ArgumentParser(add_help=False)
            q.add_argument('--x',action='append',default=original)
            c=q.parse_args(['--x','new'])
            print(c.x,original,q.parse_args([]).x is original)
            p.set_defaults(config=constant)
            print(getattr(p.parse_args([]),'config') is constant)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("True True True None None\n['v'] ['v']\n['v', 'new'] ['v'] True\nTrue\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultConversionRespectsOverridesAndOptionalConstants(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            def convert(value):
                print('convert',value)
                return int(value)
            p=argparse.ArgumentParser(add_help=False)
            p.add_argument('--x',type=convert,default='4',choices=[7])
            n=p.parse_args([])
            print(type(n.x).__name__,n.x)
            print(p.parse_args(['--x','7']).x)
            q=argparse.ArgumentParser(add_help=False)
            q.add_argument('--x',nargs='?',type=int,const='3',default='9')
            print(q.parse_args(['--x']).x)
            r=argparse.ArgumentParser(add_help=False)
            r.add_argument('--x',nargs='?',default='9')
            print(r.parse_args(['--x']).x)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("convert 4\nint 4\nconvert 7\n7\n3\nNone\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedNamespaceExposesProgressBeforeErrors(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            def fail(value):raise RuntimeError('stop')
            p=argparse.ArgumentParser(add_help=False)
            p.add_argument('--first')
            p.add_argument('--second',type=fail)
            n=argparse.Namespace(first='old')
            try:p.parse_args(['--first','new','--second','x'],namespace=n)
            except RuntimeError:print(n.first)
            try:p.parse_args(['--bad','--first','after'],namespace=n)
            except SystemExit:print(n.first)
            print(p.parse_args(['--first','last'],namespace=n) is n,n.first)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("new\nafter\nTrue last\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChoicesUseMembershipAndEqualityAndKeepNativeRangesNative(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            class Choices:
                def __iter__(self):return iter(['other'])
                def __contains__(self,value):
                    print('contains')
                    return True
            class Choice:
                def __eq__(self,value):
                    print('equals')
                    return True
            p=argparse.ArgumentParser(add_help=False)
            p.add_argument('--x',choices=Choices())
            print(p.parse_args(['--x','value']).x)
            q=argparse.ArgumentParser(add_help=False)
            q.add_argument('--x',choices=[Choice()])
            print(q.parse_args(['--x','value']).x)
            r=argparse.ArgumentParser(add_help=False)
            r.add_argument('--x',type=int,choices=range(10**12),metavar='X')
            print(r.parse_args(['--x','999999999999']).x)
            """);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var options = new LythonRunOptions { CancellationToken = cancellation.Token };
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, Describe(result));
        Assert.Equal("contains\nvalue\nequals\nvalue\n999999999999\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictsDoNotPublishDefinitionsAndSharedDestinationsRemainValid(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            p=argparse.ArgumentParser()
            p.add_argument('--x','-x')
            for names in [('--x',),('--new','-x'),('--help',)]:
                try:p.add_argument(*names)
                except argparse.ArgumentError:print('conflict')
            print('--new' in p.format_help())
            p.add_argument('--first',dest='value')
            p.add_argument('--second',dest='value')
            print(p.parse_args(['--second','b','--x','ok']).value)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("conflict\nconflict\nconflict\nFalse\nb\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("converter")]
    [InlineData("default")]
    [InlineData("contains")]
    [InlineData("equality")]
    [InlineData("arguments")]
    public async Task CallbacksSuspendAndCancelAfterActualEntry(string route)
    {
        var body = route switch
        {
            "converter" => "p.add_argument('--x',type=lambda value:int(read()))\nprint(p.parse_args(['--x','7']).x)",
            "default" => "p.add_argument('--x',type=lambda value:int(read()),default='7')\nprint(p.parse_args([]).x)",
            "contains" => "class Choices:\n def __contains__(self,value):return read()=='7'\n def __iter__(self):return iter([])\np.add_argument('--x',choices=Choices())\nprint(p.parse_args(['--x','7']).x)",
            "equality" => "class Choice:\n def __eq__(self,value):return read()=='7'\np.add_argument('--x',choices=[Choice()])\nprint(p.parse_args(['--x','7']).x)",
            _ => "class Args:\n def __iter__(self):yield read()\np.add_argument('x')\nprint(p.parse_args(Args()).x)"
        };
        using var script = new LythonEngine().Compile("import argparse\ndef read():\n with open('/value') as source:return source.read()\np=argparse.ArgumentParser(add_help=False)\n" + body);
        var budget = new LythonMemoryBudget(262144);
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost(); host.SeedFile("/value", "7");
            var entered = cancel ? host.PauseReadUntilCancellation("/value") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted); cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else { Assert.True(result.Success, Describe(result)); Assert.Equal("7\n", result.StandardOutput); Assert.True(host.CompletedAsynchronously > 0); }
            }
            finally { cancellation.Cancel(); using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedConverterCannotResumeAndRetainedDefaultsKeepTheirOwner(bool asynchronous)
    {
        using var rejected = new LythonEngine().Compile("""
            import argparse
            def convert(value):
                try:oversized=value*262144
                finally:return 'forbidden'
            p=argparse.ArgumentParser(add_help=False)
            p.add_argument('--x',type=convert)
            try:p.parse_args(['--x','value'])
            except MemoryError:print('caught')
            print('resumed')
            """);
        using var retained = new LythonEngine().Compile("import argparse\np=argparse.ArgumentParser(add_help=False)\np.add_argument('--x',default=['retained'])\nreturn p.parse_args([]).x");
        var budget = new LythonMemoryBudget(131072);
        using (var result = asynchronous ? await rejected.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }) : rejected.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var result = asynchronous ? await retained.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }) : retained.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal(new object[] { "retained" }, Assert.IsAssignableFrom<IEnumerable<object>>(result.ReturnValue));
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}
