using Lokad.Lython.Tests.Harness;
using System.Numerics;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CapturedScalarExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedCounterSnapshotsKeepTheirOwnerAcrossExecutionsAndCompilationCleanup(bool asynchronous)
    {
        const string source = """
            def make():
                value=1000
                def step():
                    nonlocal value
                    value+=1
                step()
                first=value
                step()
                return [first,value,first]
            return make()
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Check(first);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Check(second);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
            script.Dispose();
            Assert.True(budget.ReservedBytes > 0);
            Assert.True(budget.ReservedBytes < retained);
            Check(first);
        }
        Assert.Equal(0, budget.ReservedBytes);

        static void Check(LythonExecutionResult result)
        {
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsType<List<object?>>(result.ReturnValue);
            Assert.Equal(new BigInteger(1001), values[0]);
            Assert.Equal(new BigInteger(1002), values[1]);
            Assert.Equal(values[0], values[2]);
        }
    }

    // Exact isolated CPython 3.13 output for identity, promotion, namespace
    // publication and genuine protocol/I/O paths.
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["stable-identities-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    step()\n    first=read()\n    step()\n    second=read()\n    print(old,first,second,first is read(),second is read())\n    print(old is first,first is second,id(read())==id(read()))\n    replace(old)\n    print(read() is old)\nrun()\n","1000 1001 1002 False True\nFalse False True\nTrue\n",false];
        yield return ["stable-identities-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    step()\n    first=read()\n    step()\n    second=read()\n    print(old,first,second,first is read(),second is read())\n    print(old is first,first is second,id(read())==id(read()))\n    replace(old)\n    print(read() is old)\nrun()\n","1000 1001 1002 False True\nFalse False True\nTrue\n",false];
        yield return ["promotion-native","\ndef run():\n    value=9223372036854775806\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    for i in range(4):\n     step()\n     print(read())\n    print(old,read() is read())\nrun()\n","9223372036854775807\n9223372036854775808\n9223372036854775809\n9223372036854775810\n9223372036854775806 True\n",false];
        yield return ["promotion-ordinary","\ndef run():\n    value=9223372036854775806\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    for i in range(4):\n     step()\n     print(read())\n    print(old,read() is read())\nrun()\n","9223372036854775807\n9223372036854775808\n9223372036854775809\n9223372036854775810\n9223372036854775806 True\n",false];
        yield return ["signed-arithmetic-native","\ndef run():\n    value=-9\n    def step():\n        nonlocal value\n        value//=2\n        value%=3\n        value*=7\n        value-=4\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    for i in range(3):\n     step()\n     print(read())\nrun()\n","4\n11\n11\n",false];
        yield return ["signed-arithmetic-ordinary","\ndef run():\n    value=-9\n    def step():\n        nonlocal value\n        try:\n            value//=2\n            value%=3\n            value*=7\n            value-=4\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    for i in range(3):\n     step()\n     print(read())\nrun()\n","4\n11\n11\n",false];
        yield return ["bool-to-integer-native","\ndef run():\n    value=True\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    step()\n    first=read()\n    step()\n    print(old,first,read(),type(read()).__name__)\nrun()\n","True 2 3 int\n",false];
        yield return ["bool-to-integer-ordinary","\ndef run():\n    value=True\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    step()\n    first=read()\n    step()\n    print(old,first,read(),type(read()).__name__)\nrun()\n","True 2 3 int\n",false];
        yield return ["zero-overflow-divide-native","\ndef run():\n    value=-9223372036854775808\n    def step():\n        nonlocal value\n        value//=-1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(read())\n    step()\n    print(read())\nrun()\n","9223372036854775808\n-9223372036854775808\n",false];
        yield return ["zero-overflow-divide-ordinary","\ndef run():\n    value=-9223372036854775808\n    def step():\n        nonlocal value\n        try:\n            value//=-1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(read())\n    step()\n    print(read())\nrun()\n","9223372036854775808\n-9223372036854775808\n",false];
        yield return ["modulo-minimum-native","\ndef run():\n    value=-9223372036854775808\n    def step():\n        nonlocal value\n        value%=-1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(read())\nrun()\n","0\n",false];
        yield return ["modulo-minimum-ordinary","\ndef run():\n    value=-9223372036854775808\n    def step():\n        nonlocal value\n        try:\n            value%=-1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(read())\nrun()\n","0\n",false];
        yield return ["delete-rebind-native","\ndef run():\n    value=1234\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    def erase():\n     nonlocal value\n     del value\n    erase()\n    try:step()\n    except NameError:print('missing')\n    replace(2000)\n    step()\n    print(read())\nrun()\n","missing\n2001\n",false];
        yield return ["delete-rebind-ordinary","\ndef run():\n    value=1234\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    def erase():\n     nonlocal value\n     del value\n    erase()\n    try:step()\n    except NameError:print('missing')\n    replace(2000)\n    step()\n    print(read())\nrun()\n","missing\n2001\n",false];
        yield return ["lambda-publication-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    old=read()\n    reader=lambda:value\n    step()\n    print(old,reader(),reader() is read())\n    replace(old)\n    print(reader() is old)\nrun()\n","1001 1002 True\nTrue\n",false];
        yield return ["lambda-publication-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    old=read()\n    reader=lambda:value\n    step()\n    print(old,reader(),reader() is read())\n    replace(old)\n    print(reader() is old)\nrun()\n","1001 1002 True\nTrue\n",false];
        yield return ["locals-observation-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(locals()['value'],read())\n    step()\n    print(locals()['value'],read())\nrun()\n","1001 1001\n1002 1002\n",false];
        yield return ["locals-observation-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(locals()['value'],read())\n    step()\n    print(locals()['value'],read())\nrun()\n","1001 1001\n1002 1002\n",false];
        yield return ["comprehension-closure-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    readers=[lambda:value for i in range(2)]\n    old=read()\n    step()\n    print(old,[f() for f in readers],readers[0]() is read())\nrun()\n","1001 [1002, 1002] True\n",false];
        yield return ["comprehension-closure-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    readers=[lambda:value for i in range(2)]\n    old=read()\n    step()\n    print(old,[f() for f in readers],readers[0]() is read())\nrun()\n","1001 [1002, 1002] True\n",false];
        yield return ["observable-reads-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n        return value\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    snapshots=[step() for i in range(4)]\n    print(snapshots,read(),snapshots[-1] is read())\n    print(snapshots[0],snapshots[0] is snapshots[1])\nrun()\n","[1001, 1002, 1003, 1004] 1004 True\n1001 False\n",false];
        yield return ["observable-reads-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n            return value\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    snapshots=[step() for i in range(4)]\n    print(snapshots,read(),snapshots[-1] is read())\n    print(snapshots[0],snapshots[0] is snapshots[1])\nrun()\n","[1001, 1002, 1003, 1004] 1004 True\n1001 False\n",false];
        yield return ["protected-error-order-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n        try:\n         value//=0\n        except ZeroDivisionError:\n         print(value)\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(read())\nrun()\n","1001\n1001\n",false];
        yield return ["protected-error-order-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n            try:\n             value//=0\n            except ZeroDivisionError:\n             print(value)\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    print(read())\nrun()\n","1001\n1001\n",false];
        yield return ["dynamic-protocol-native","events=[]\nclass Start:\n def __iadd__(self,other):\n  events.append(other)\n  return 42\n\ndef run():\n    value=Start()\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    step()\n    step()\n    print(read(),events,type(old).__name__)\nrun()\n","43 [1] Start\n",false];
        yield return ["escaping-counter-native","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    old=read()\n    return step,read,old\nstep,read,old=run()\nstep()\nprint(old,read(),read() is read())\n","1001 1002 True\n",false];
        yield return ["dynamic-protocol-ordinary","events=[]\nclass Start:\n def __iadd__(self,other):\n  events.append(other)\n  return 42\n\ndef run():\n    value=Start()\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    old=read()\n    step()\n    step()\n    print(read(),events,type(old).__name__)\nrun()\n","43 [1] Start\n",false];
        yield return ["escaping-counter-ordinary","\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        try:\n            value+=1\n        finally:\n            pass\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    step()\n    old=read()\n    return step,read,old\nstep,read,old=run()\nstep()\nprint(old,read(),read() is read())\n","1001 1002 True\n",false];
        yield return ["actual-io","snapshots=[]\n\ndef run():\n    value=1000\n    def step():\n        nonlocal value\n        value+=1\n        with open('offset.txt') as source:offset=int(source.read())\n        snapshots.append(value)\n        value+=offset\n        value+=1\n        return value\n    def read():return value\n    def replace(new):\n        nonlocal value\n        value=new\n    first=step()\n    second=step()\n    print(first,second,snapshots,read(),second is read())\nrun()\n","1004 1008 [1001, 1005] 1008 True\n",true];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task CapturedUpdatesPreservePythonValuesAndAliases(string name, string source, string expected, bool actualIo)
    {
        _ = name;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/offset.txt", "2");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/offset.txt", "2");
            using (var result = script.Run(immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            if (actualIo)
            {
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.Equal(2, immediate.ClosedFileCount);
                Assert.Equal(2, delayed.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
