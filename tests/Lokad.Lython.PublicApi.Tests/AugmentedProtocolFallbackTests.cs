using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedProtocolFallbackTests
{
    public static IEnumerable<object[]> Operators()
    {
        foreach (var (token, method) in new[] { ("+", "add"), ("-", "sub"), ("*", "mul"),
            ("/", "truediv"), ("//", "floordiv"), ("%", "mod"), ("**", "pow"),
            ("|", "or"), ("^", "xor"), ("&", "and"), ("<<", "lshift"), (">>", "rshift"), ("@", "matmul") })
            yield return [token, method];
    }

    private static async Task AssertResult(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }

    [Theory]
    [MemberData(nameof(Operators))]
    public async Task DecliningInPlaceUsesOrdinaryLeftMethod(string token, string method)
        => await AssertResult($"class Left:\n def __i{method}__(self, other): return NotImplemented\n def __{method}__(self, other): return 'fallback'\nx = Left()\nx {token}= 1\nreturn x", "fallback");

    [Theory]
    [MemberData(nameof(Operators))]
    public async Task MissingInPlaceUsesOrdinaryLeftMethod(string token, string method)
        => await AssertResult($"class Left:\n def __{method}__(self, other): return 'fallback'\nx = Left()\nx {token}= 1\nreturn x", "fallback");

    [Theory]
    [MemberData(nameof(Operators))]
    public async Task BuiltinLeftCanUseReflectedRightMethod(string token, string method)
        => await AssertResult($"class Right:\n def __r{method}__(self, other): return 'reflected'\nx = 1\nx {token}= Right()\nreturn x", "reflected");

    [Theory]
    [MemberData(nameof(Operators))]
    public async Task DecliningLeftCanUseReflectedRightMethod(string token, string method)
        => await AssertResult($"class Left:\n def __i{method}__(self, other): return NotImplemented\n def __{method}__(self, other): return NotImplemented\nclass Right:\n def __r{method}__(self, other): return 'reflected'\nx = Left()\nx {token}= Right()\nreturn x", "reflected");

    [Fact]
    public async Task ReflectedSubclassMethodKeepsItsPrecedence()
        => await AssertResult("class Base:\n def __iadd__(self, other): return NotImplemented\n def __add__(self, other): return 'base'\nclass Derived(Base):\n def __radd__(self, other): return 'derived'\nx = Base()\nx += Derived()\nreturn x", "derived");

    [Fact]
    public async Task OrdinaryAdditionAlsoTriesReflectedSubclassFirst()
        => await AssertResult("class Base:\n def __add__(self, other): return 'base'\nclass Derived(Base):\n def __radd__(self, other): return 'derived'\nreturn Base() + Derived()", "derived");

    [Theory]
    [InlineData("x = x + Number()")]
    [InlineData("x += Number()")]
    public async Task SameTypeDoesNotTryItsReflectedMethod(string operation)
        => await AssertResult($"class Number:\n def __add__(self, other): return NotImplemented\n def __radd__(self, other): return 'incorrect'\nx = Number()\ntry:\n {operation}\nexcept TypeError:\n return 'declined'\nreturn 'incorrect'", "declined");

    [Theory]
    [InlineData("x = x + Derived()")]
    [InlineData("x += Derived()")]
    public async Task DecliningSubclassIsNotCalledTwice(string operation)
        => await AssertResult($"calls = []\nclass Base:\n def __add__(self, other):\n  calls.append('left')\n  return NotImplemented\nclass Derived(Base):\n def __radd__(self, other):\n  calls.append('right')\n  return NotImplemented\nx = Base()\ntry:\n {operation}\nexcept TypeError:\n return ','.join(calls)\nreturn 'incorrect'", "right,left");

    [Fact]
    public async Task InPlaceErrorsPropagateInsteadOfTryingOtherSlots()
        => await AssertResult("class Left:\n def __iadd__(self, other): raise ValueError('in-place error')\n def __add__(self, other): return 'fallback'\nx = Left()\ntry:\n x += 1\nexcept ValueError as error:\n return str(error)\nreturn 'missed'", "in-place error");

    [Fact]
    public async Task UnsupportedOperandsKeepTheAugmentedToken()
        => await AssertResult("x = 1\ntry:\n x += {}\nexcept TypeError as error:\n return str(error)\nreturn 'missed'", "unsupported operand type(s) for +=: 'int' and 'dict'");
}
