using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RepeatedScopeDirectiveTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "module_global","global x,x\nx=7\nprint(x)","7\n" };
        yield return new object[] { "function_global","x=1\ndef f():\n global x,x\n x+=2\n return x\nprint(f(),x)","3 3\n" };
        yield return new object[] { "function_nonlocal","def outer():\n x=1\n def f():\n  nonlocal x,x\n  x+=2\n  return x\n return f\nf=outer()\nprint(f(),f())","3 5\n" };
        yield return new object[] { "class_global","x=1\nclass C:\n global x,x\n x+=2\n value=x\nprint(x,C.value,hasattr(C,\"x\"))","3 3 False\n" };
        yield return new object[] { "class_nonlocal","def outer():\n x=1\n class C:\n  nonlocal x,x\n  x+=2\n  value=x\n return x,C.value\nprint(outer())","(3, 3)\n" };
        yield return new object[] { "delete_rebind","x=1\ndef f():\n global x,x,x\n del x\n x=4\nf()\nprint(x)","4\n" };
        yield return new object[] { "private_collision","class C:\n def f(self):\n  global __x,_C__x,__x\n  __x=5\nC().f()\nprint(_C__x)","5\n" };
        yield return new object[] { "class_cell","class C:\n def f(self):\n  nonlocal __class__,__class__\n  return __class__\nprint(C().f() is C)","True\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompatibleBindings(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("nonlocal x,x")]
    [InlineData("def f():\n global x,x\n nonlocal x,x")]
    [InlineData("def f(x):\n global x,x")]
    [InlineData("def f():\n nonlocal x,x")]
    [InlineData("def f():\n x=1\n global x,x")]
    [InlineData("def f(x,x):pass")]
    [InlineData("match [1,2]:\n case x,x:pass")]
    [InlineData("def outer():\n x=1\n def f():\n  print(x)\n  nonlocal x,x")]
    public void InvalidNamesAndScopeConflictsFailBeforeEffects(string source)
        => Assert.False(new LythonEngine().Compile("print('effect')\n" + source).IsValid);
}
