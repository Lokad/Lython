using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnboundLocalVariableTests
{
    [Theory]
    [InlineData("for value in []: pass")]
    [InlineData("for value, other in []: pass")]
    [InlineData("if False:\n value = 1")]
    [InlineData("try:\n pass\nexcept Exception:\n value = 1")]
    [InlineData("if False:\n import math as value")]
    [InlineData("if False:\n from math import pi as value")]
    [InlineData("if False:\n def value(): pass")]
    [InlineData("if False:\n class value: pass")]
    [InlineData("if False:\n (value := 1)")]
    [InlineData("if False:\n value, other = (1, 2)")]
    public async Task UnexecutedFunctionBindingsNeverReadTheOuterValue(string binding)
    {
        await Check("value = 99\ndef f():\n " + binding.Replace("\n", "\n ") +
            "\n return value\ntry:\n f()\nexcept UnboundLocalError as error:\n return str(error)\nreturn 'incorrect'",
            "cannot access local variable 'value' where it is not associated with a value");
    }

    [Theory]
    [InlineData("return value")]
    [InlineData("return f'{value}'")]
    [InlineData("value += 1")]
    [InlineData("del value")]
    public async Task ReadsAfterAnEmptyLoopAreUnbound(string read)
    {
        await Check("value = 99\ndef f():\n for value in []: pass\n " + read +
            "\n for value in []: pass\ntry:\n f()\nexcept UnboundLocalError:\n return 'unbound'\nreturn 'incorrect'", "unbound");
    }

    [Theory]
    [InlineData("def f():\n value = 1\n if True:\n  del value\n return value\nf()", "UnboundLocalError")]
    [InlineData("def f():\n value = 1\n if True:\n  try:\n   raise ValueError()\n  except ValueError as value:\n   pass\n return value\nf()", "UnboundLocalError")]
    [InlineData("def f():\n for value in []: pass\n yield value\nlist(f())", "UnboundLocalError")]
    [InlineData("def outer():\n def inner(): return value\n return inner()\n value = 1\nouter()", "NameError")]
    [InlineData("def outer():\n for value in []: pass\n return [value for i in [1]]\nouter()", "NameError")]
    [InlineData("def outer():\n for value in []: pass\n return (lambda: value)()\nouter()", "NameError")]
    [InlineData("def outer():\n for value in []: pass\n def inner():\n  nonlocal value\n  return value\n return inner()\nouter()", "NameError")]
    [InlineData("def outer():\n for value in []: pass\n def inner():\n  nonlocal value\n  del value\n inner()\nouter()", "NameError")]
    [InlineData("for value in []: pass\nvalue", "NameError")]
    [InlineData("def f():\n global value\n return value\nf()", "NameError")]
    public async Task LocalAndFreeVariableFailuresHaveTheirPythonTypes(string body, string expected)
    {
        await Check("try:\n " + body.Replace("\n", "\n ") +
            "\nexcept NameError as error:\n return type(error).__name__\nreturn 'incorrect'", expected);
    }

    [Theory]
    [InlineData("value = 99\nclass C:\n for value in []: pass\n result = value\nreturn str(C.result)", "99")]
    [InlineData("value = 99\ndef f():\n global value\n for value in []: pass\n return value\nreturn str(f())", "99")]
    [InlineData("def f(a, b=2, *, c=3):\n for value in []: pass\n return [a, b, c]\nreturn str(f(1, c=4))", "[1, 2, 4]")]
    [InlineData("def outer():\n def inner(): return value\n value = 7\n return inner\nreturn str(outer()())", "7")]
    [InlineData("def f():\n for value in []: pass\n return 'value' in locals()\nreturn str(f())", "False")]
    public async Task OtherNamespacesAndBoundArgumentsKeepTheirBehavior(string source, string expected)
        => await Check(source, expected);

    [Theory]
    [InlineData("return value\nvalue = 1")]
    [InlineData("value: int\nreturn value")]
    [InlineData("value = 1\ndel value\nreturn value")]
    public void StaticallyCertainUnboundReadsStillHaveCompileDiagnostics(string body)
    {
        var script = new LythonEngine().Compile("def f():\n " + body.Replace("\n", "\n "));
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3146");
    }

    [Fact]
    public async Task UnboundLocalErrorIsAConstructibleNameErrorSubtype()
    {
        await Check("""
            assert issubclass(UnboundLocalError, NameError)
            assert issubclass(UnboundLocalError, Exception)
            assert not issubclass(NameError, UnboundLocalError)
            error = UnboundLocalError('local')
            assert isinstance(error, NameError)
            assert error.args == ('local',)
            try:
                raise error
            except NameError as caught:
                assert caught is error
                return type(caught).__name__ + ':' + str(caught)
            """, "UnboundLocalError:local");
    }

    private static async Task Check(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }
}
