using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG03/MG06: string methods adopt fresh results derived from unowned
/// (shared-constant) receivers, so retained derivations accumulate.
/// </summary>
public sealed class StringMethodOwnershipTests
{
    [Fact]
    public void EveryStringReturningMemberOwnsItsResult()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        AssertOwned("ABC", "lower");
        AssertOwned("abc", "upper");
        AssertOwned("abc", "capitalize");
        AssertOwned("aBc", "swapcase");
        AssertOwned("hello world", "title");
        AssertOwned("a-b-c", "replace", ["-", "+"]);
        AssertOwned("x", "center", [5]);
        AssertOwned("x", "ljust", [5]);
        AssertOwned("x", "rjust", [5]);
        AssertOwned("42", "zfill", [5]);
        AssertOwned("unprefixed", "removeprefix", ["un"]);
        AssertOwned("abcxx", "removesuffix", ["xx"]);
        AssertOwned("Hi {}", "format", ["there"]);
        AssertOwned("  ab  ", "strip");
        AssertOwned("  ab  ", "lstrip");
        AssertOwned("  ab  ", "rstrip");
        AssertOwned("a\tb", "expandtabs");

        void AssertOwned(string text, string name, params object[] arguments)
        {
            var receiver = PyString.FromString(text);
            Assert.Null(receiver.OwnerMemoryGovernor);
            Assert.True(PyMemberAccess.TryResolve(receiver, name, context, span, out var value));
            var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
            var converted = new CallArgumentValue[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                converted[i] = arguments[i] is int integer
                    ? CallArgumentValue.Positional(new BigInteger(integer))
                    : CallArgumentValue.Positional(PyString.FromString((string)arguments[i]));
            }

            var result = Assert.IsType<PyString>(bound.Invoke(converted, span, context));
            Assert.NotNull(result.OwnerMemoryGovernor);
        }

    }
}