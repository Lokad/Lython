using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeSliceProtocolTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "read-instance-shadow", "class Bound:\n def __index__(self): return 1\nbound=Bound(); bound.__index__=lambda:0\nvalues=[0,1,2]\ntry:\n print(values[bound:])\nexcept TypeError: print('type-only')", "[1, 2]\n" },
        { "read-instance-only", "class Bound: pass\nbound=Bound(); bound.__index__=lambda:1\nvalues=[0,1,2]\ntry:\n print(values[bound:])\nexcept TypeError: print('type-only')", "type-only\n" },
        { "read-getattr-only", "class Bound:\n def __getattr__(self,name):\n  print(name)\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n print(values[bound:])\nexcept TypeError: print('type-only')", "type-only\n" },
        { "read-descriptor", "class Bound:\n @property\n def __index__(self):\n  print('bind')\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n print(values[bound:])\nexcept TypeError: print('type-only')", "bind\n[1, 2]\n" },
        { "assign-instance-shadow", "class Bound:\n def __index__(self): return 1\nbound=Bound(); bound.__index__=lambda:0\nvalues=[0,1,2]\ntry:\n values[bound:]=[7]; print(values)\nexcept TypeError: print('type-only')", "[0, 7]\n" },
        { "assign-instance-only", "class Bound: pass\nbound=Bound(); bound.__index__=lambda:1\nvalues=[0,1,2]\ntry:\n values[bound:]=[7]; print(values)\nexcept TypeError: print('type-only')", "type-only\n" },
        { "assign-getattr-only", "class Bound:\n def __getattr__(self,name):\n  print(name)\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n values[bound:]=[7]; print(values)\nexcept TypeError: print('type-only')", "type-only\n" },
        { "assign-descriptor", "class Bound:\n @property\n def __index__(self):\n  print('bind')\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n values[bound:]=[7]; print(values)\nexcept TypeError: print('type-only')", "bind\n[0, 7]\n" },
        { "delete-instance-shadow", "class Bound:\n def __index__(self): return 1\nbound=Bound(); bound.__index__=lambda:0\nvalues=[0,1,2]\ntry:\n del values[bound:]; print(values)\nexcept TypeError: print('type-only')", "[0]\n" },
        { "delete-instance-only", "class Bound: pass\nbound=Bound(); bound.__index__=lambda:1\nvalues=[0,1,2]\ntry:\n del values[bound:]; print(values)\nexcept TypeError: print('type-only')", "type-only\n" },
        { "delete-getattr-only", "class Bound:\n def __getattr__(self,name):\n  print(name)\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n del values[bound:]; print(values)\nexcept TypeError: print('type-only')", "type-only\n" },
        { "delete-descriptor", "class Bound:\n @property\n def __index__(self):\n  print('bind')\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n del values[bound:]; print(values)\nexcept TypeError: print('type-only')", "bind\n[0]\n" },
        { "indices-instance-shadow", "class Bound:\n def __index__(self): return 1\nbound=Bound(); bound.__index__=lambda:0\nvalues=[0,1,2]\ntry:\n print(slice(bound,None,None).indices(3))\nexcept TypeError: print('type-only')", "(1, 3, 1)\n" },
        { "indices-instance-only", "class Bound: pass\nbound=Bound(); bound.__index__=lambda:1\nvalues=[0,1,2]\ntry:\n print(slice(bound,None,None).indices(3))\nexcept TypeError: print('type-only')", "type-only\n" },
        { "indices-getattr-only", "class Bound:\n def __getattr__(self,name):\n  print(name)\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n print(slice(bound,None,None).indices(3))\nexcept TypeError: print('type-only')", "type-only\n" },
        { "indices-descriptor", "class Bound:\n @property\n def __index__(self):\n  print('bind')\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n print(slice(bound,None,None).indices(3))\nexcept TypeError: print('type-only')", "bind\n(1, 3, 1)\n" },
        { "length-instance-shadow", "class Bound:\n def __index__(self): return 1\nbound=Bound(); bound.__index__=lambda:0\nvalues=[0,1,2]\ntry:\n print(slice(None).indices(bound))\nexcept TypeError: print('type-only')", "(0, 1, 1)\n" },
        { "length-instance-only", "class Bound: pass\nbound=Bound(); bound.__index__=lambda:1\nvalues=[0,1,2]\ntry:\n print(slice(None).indices(bound))\nexcept TypeError: print('type-only')", "type-only\n" },
        { "length-getattr-only", "class Bound:\n def __getattr__(self,name):\n  print(name)\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n print(slice(None).indices(bound))\nexcept TypeError: print('type-only')", "type-only\n" },
        { "length-descriptor", "class Bound:\n @property\n def __index__(self):\n  print('bind')\n  return lambda:1\nbound=Bound()\nvalues=[0,1,2]\ntry:\n print(slice(None).indices(bound))\nexcept TypeError: print('type-only')", "bind\n(0, 1, 1)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeSlicesUseTypeLevelIndexHooks(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
