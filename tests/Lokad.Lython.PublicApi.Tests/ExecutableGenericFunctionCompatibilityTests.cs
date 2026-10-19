using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutableGenericFunctionCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return [false, "params-and-defaults", "T=17\ndef f[T](x=T):return x,T\nr=f()\nprint(r[0],r[1] is f.__type_params__[0],f.__annotations__)", "17 True {}\n"];
        yield return [true, "params-and-defaults", "T=17\ndef f[T](x=T):return x,T\nr=f()\nprint(r[0],r[1] is f.__type_params__[0],f.__annotations__)", "17 True {}\n"];
        yield return [false, "escaped-closure", "def make():\n seed=1\n def f[T](x:T)->T:return seed,x,T\n seed=2\n return f\nf=make()\nr=f(3)\nprint(r[:2],r[2] is f.__type_params__[0],f.__annotations__['x'] is f.__type_params__[0])", "(2, 3) True True\n"];
        yield return [true, "escaped-closure", "def make():\n seed=1\n def f[T](x:T)->T:return seed,x,T\n seed=2\n return f\nf=make()\nr=f(3)\nprint(r[:2],r[2] is f.__type_params__[0],f.__annotations__['x'] is f.__type_params__[0])", "(2, 3) True True\n"];
        yield return [false, "nested-helper", "def f[T](x):\n def g():return T,x\n x=7\n return g\ng=f(2)\nr=g()\nprint(r[1],r[0].__name__)", "7 T\n"];
        yield return [true, "nested-helper", "def f[T](x):\n def g():return T,x\n x=7\n return g\ng=f(2)\nr=g()\nprint(r[1],r[0].__name__)", "7 T\n"];
        yield return [false, "nonlocal", "def make():\n n=1\n def f[T]():\n  nonlocal n\n  n+=1\n  return n,T\n def current():return n\n return f,current\nf,current=make()\na=f()\nb=f()\nprint(a[0],b[0],current(),a[1] is b[1])", "2 3 3 True\n"];
        yield return [true, "nonlocal", "def make():\n n=1\n def f[T]():\n  nonlocal n\n  n+=1\n  return n,T\n def current():return n\n return f,current\nf,current=make()\na=f()\nb=f()\nprint(a[0],b[0],current(),a[1] is b[1])", "2 3 3 True\n"];
        yield return [false, "global", "T=11\ndef f[U]():\n global T\n T+=1\n return T,U\nr=f()\nprint(T,r[0],r[1] is f.__type_params__[0])", "12 12 True\n"];
        yield return [true, "global", "T=11\ndef f[U]():\n global T\n T+=1\n return T,U\nr=f()\nprint(T,r[0],r[1] is f.__type_params__[0])", "12 12 True\n"];
        yield return [false, "lazy-bound", "events=[]\ndef value():events.append('bound');return int\ndef f[T:value()](x:T)->T:return x\nprint(events,f(3),events)\np=f.__type_params__[0]\nprint(p.__bound__ is int,p.__bound__ is int,events)", "[] 3 []\nTrue True ['bound']\n"];
        yield return [true, "lazy-bound", "events=[]\ndef value():events.append('bound');return int\ndef f[T:value()](x:T)->T:return x\nprint(events,f(3),events)\np=f.__type_params__[0]\nprint(p.__bound__ is int,p.__bound__ is int,events)", "[] 3 []\nTrue True ['bound']\n"];
        yield return [false, "shadowed-enclosing-parameter", "def make():\n T=17\n def f[T](x:T=T)->T:return x,T\n return f\nf=make()\nv=f()\nprint(v[0],v[1] is f.__type_params__[0],f.__annotations__['x'] is v[1],f.__annotations__['return'] is v[1])", "17 True True True\n"];
        yield return [true, "shadowed-enclosing-parameter", "def make():\n T=17\n def f[T](x:T=T)->T:return x,T\n return f\nf=make()\nv=f()\nprint(v[0],v[1] is f.__type_params__[0],f.__annotations__['x'] is v[1],f.__annotations__['return'] is v[1])", "17 True True True\n"];
        yield return [false, "nested-generic-shadow", "def make():\n T=17\n def outer[U]():\n  def inner[T:U]():return T,U\n  return inner\n return outer\nouter=make()\ninner=outer()\na,b=inner()\nprint(a is inner.__type_params__[0],b is outer.__type_params__[0],inner.__type_params__[0].__bound__ is b)", "True True True\n"];
        yield return [true, "nested-generic-shadow", "def make():\n T=17\n def outer[U]():\n  def inner[T:U]():return T,U\n  return inner\n return outer\nouter=make()\ninner=outer()\na,b=inner()\nprint(a is inner.__type_params__[0],b is outer.__type_params__[0],inner.__type_params__[0].__bound__ is b)", "True True True\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GenericScopesAndCapturedCellsMatchPython(bool asynchronous, string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public void TypeParameterCannotBeReboundThroughAnEnclosingNonlocal()
    {
        using var script = new LythonEngine().Compile("def make():\n T=17\n def f[T]():\n  nonlocal T\n  return T\n return f\nprint(make()())");
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1100" && d.Message.Contains("nonlocal", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericBodyKeepsItsCapturedPayloadFundedAcrossHostIo(bool cancel)
    {
        const string source = """
def make():
 payload='held'*1024
 def read[T](value):
  with open('hold.txt','a') as target:target.write('done')
  return value,payload,T
 return read
read=make()
value,payload,parameter=read('argument')
print(value,parameter is read.__type_params__[0])
return payload
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/hold.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/hold.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        LythonExecutionResult? result = null;
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(entered, first);
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "argument True\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforedone", host.ReadText("/hold.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else
            {
                Assert.Equal(string.Concat(Enumerable.Repeat("held", 1024)), result.ReturnValue);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericBodiesPreserveRecursionLimits(bool asynchronous)
    {
        var source = "def f[T](n):return f(n+1)\nf(0)\nprint('unreachable')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxRecursionDepth = 8};
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("", result.StandardOutput);
            Assert.Equal("RecursionError", result.Failure?.ExceptionType);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
