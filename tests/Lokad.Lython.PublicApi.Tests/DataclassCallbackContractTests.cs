using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DataclassCallbackContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedCallbackStorageIsTerminalAfterRealIo(bool resultFactory)
    {
        var source = """
            from dataclasses import dataclass,field,asdict
            def produce():
                try:
                    with open('/data') as f:n=int(f.read())
                    return 'x'*n
                except MemoryError:return 'suppressed'
            """ + (resultFactory ? "\n@dataclass\nclass D:x:int\ndef factory(pairs):return produce()\nasdict(D(1),dict_factory=factory)"
                : "\n@dataclass\nclass D:x:str=field(default_factory=produce)\nD()") + "\nprint('escaped')";
        using var script=new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget=new LythonMemoryBudget(512*1024);
        var host=new DelayedLythonHost();host.SeedFile("/data","1000000");
        using(var result=await script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget}))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
            Assert.Equal("",result.StandardOutput);
            Assert.True(host.CompletedAsynchronously>0);
        }
        Assert.Equal(0,budget.ReservedBytes);
        var recovery=new DelayedLythonHost();recovery.SeedFile("/data","1");
        using(var result=await script.RunAsync(recovery,new LythonRunOptions{MemoryBudget=budget}))Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal(0,budget.ReservedBytes);
    }

    private const string declarations = "from dataclasses import dataclass,field,asdict,astuple,replace\n@dataclass\nclass D:\n x:object\n";
    private const string io = "def produce():\n with open('/data') as f:return f.read()\n";
    private static readonly (string Name, string Source)[] Callbacks =
    {
    ("control-guest-call",io+"print(produce())"),
    ("default-factory",io+"from dataclasses import dataclass,field\n@dataclass\nclass C:\n x:str=field(default_factory=produce)\nprint(C())"),
    ("post-init","from dataclasses import dataclass\n@dataclass\nclass C:\n x:int=1\n def __post_init__(self):\n  with open('/data') as f:self.x=f.read()\nprint(C())"),
    ("asdict-factory",declarations+"def factory(pairs):\n with open('/data') as f:f.read()\n return dict(pairs)\nprint(asdict(D(1),dict_factory=factory))"),
    ("astuple-factory",declarations+"def factory(items):\n with open('/data') as f:f.read()\n return tuple(items)\nprint(astuple(D(1),tuple_factory=factory))"),
    ("replace-custom-init","from dataclasses import dataclass,replace\n@dataclass(init=False)\nclass C:\n x:int\n def __init__(self,x):\n  with open('/data') as f:f.read()\n  self.x=x\nc=C(1)\nprint(replace(c,x=2))"),
    ("deepcopy-control",io+"import copy\nclass Leaf:\n def __deepcopy__(self,memo):return produce()\nprint(copy.deepcopy(Leaf()))"),
    ("asdict-deepcopy",declarations+io+"class Leaf:\n def __deepcopy__(self,memo):return produce()\nprint(asdict(D(Leaf()))['x'] is not None)"),
    ("descriptor-control",declarations+io+"d=D(1);D.x=property(lambda self:produce())\nprint(d.x)"),
    ("descriptor-asdict",declarations+io+"d=D(1);D.x=property(lambda self:produce())\nprint(asdict(d))"),
    ("generated-repr-getter","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\n@dataclass\nclass D:x:object\nd=D(1);D.x=property(lambda self:produce())\nprint(repr(d))"),
    ("generated-eq-getter","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\n@dataclass\nclass D:x:object\na=D(1);b=D(2);D.x=property(lambda self:produce())\nprint(a==b)"),
    ("generated-order-getter","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\n@dataclass(order=True)\nclass D:x:object\na=D(1);b=D(2);D.x=property(lambda self:produce())\nprint(a<=b)"),
    ("field-truth-hook","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\nclass Flag:\n def __bool__(self):produce();return False\nflag=Flag()\n@dataclass\nclass D:x:int=field(default=1,repr=flag)\nprint(D(),fields(D)[0].repr is flag)"),
    ("decorator-truth-hook","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\nclass Flag:\n def __bool__(self):produce();return False\nflag=Flag()\nclass D:x:int=1\nD=dataclass(D,repr=flag)\nprint('__repr__' in D.__dict__,D.__dataclass_params__.repr is flag)"),
    ("make-dataclass-truth-hook","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\nclass Flag:\n def __bool__(self):produce();return False\nflag=Flag()\nD=make_dataclass('D',[('x',int,1)],repr=flag)\nprint('__repr__' in D.__dict__,D.__dataclass_params__.repr is flag)"),
    ("copy-replace-constructor","from dataclasses import dataclass,field,fields,asdict,astuple,replace,make_dataclass\ndef produce():\n with open('/data') as f:return f.read()\nimport copy\n@dataclass(init=False)\nclass D:\n x:int\n def __init__(self,x):produce();self.x=x\nprint(copy.replace(D(1),x=2))"),
};

    private static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>
    {
        ["control-guest-call"] = "ok\n",
        ["default-factory"] = "C(x='ok')\n",
        ["post-init"] = "C(x='ok')\n",
        ["asdict-factory"] = "{'x': 1}\n",
        ["astuple-factory"] = "(1,)\n",
        ["replace-custom-init"] = "C(x=2)\n",
        ["deepcopy-control"] = "ok\n",
        ["asdict-deepcopy"] = "True\n",
        ["descriptor-control"] = "ok\n",
        ["descriptor-asdict"] = "{'x': 'ok'}\n",
        ["generated-repr-getter"] = "D(x='ok')\n",
        ["generated-eq-getter"] = "True\n",
        ["generated-order-getter"] = "True\n",
        ["field-truth-hook"] = "D() True\n",
        ["decorator-truth-hook"] = "False True\n",
        ["make-dataclass-truth-hook"] = "False True\n",
        ["copy-replace-constructor"] = "D(x=2)\n",
    };
    public static IEnumerable<object[]> Cases() => Callbacks.Select(c => new object[] { c.Name, c.Source });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CallbacksAwaitRealHostIoAndReleaseOwnership(string name, string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(512 * 1024);
        var host = new DelayedLythonHost(); host.SeedFile("/data", "ok");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        { Assert.True(result.Success, result.Failure?.Message); Assert.Equal(Expected[name], result.StandardOutput); Assert.True(host.CompletedAsynchronously > 0); }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CancellationDuringCallbackAwaitsCleanupAndAllowsReuse(string name, string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(512 * 1024);
        var host = new DelayedLythonHost(); host.SeedFile("/data", "ok");
        var entered = host.PauseReadUntilCancellation("/data");
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally { cancellation.Cancel(); }
        using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10))) Assert.False(result.Success);
        Assert.Equal(0, budget.ReservedBytes);
        var recovery = new DelayedLythonHost(); recovery.SeedFile("/data", "ok");
        using (var result = await script.RunAsync(recovery, new LythonRunOptions { MemoryBudget = budget }))
        { Assert.True(result.Success, result.Failure?.Message); Assert.Equal(Expected[name], result.StandardOutput); }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
