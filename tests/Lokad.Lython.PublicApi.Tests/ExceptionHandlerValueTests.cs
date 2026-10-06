using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionHandlerValueTests
{
    [Theory]
    [InlineData("(LookupError, ValueError)", "errors", "matched IndexError\n")]
    [InlineData("(LookupError, ValueError)", "(errors)", "matched IndexError\n")]
    [InlineData("()", "errors", "fallback\n")]
    [InlineData("(ValueError,)", "errors", "fallback\n")]
    [InlineData("()", "()", "fallback\n")]
    [InlineData("()", "(LookupError,)", "matched IndexError\n")]
    public async Task TupleValuesAndGroupedHandlersFollowPython(string value, string handler, string expected)
    {
        await AssertOutput($$"""
            errors = {{value}}
            try:
                raise IndexError("original")
            except {{handler}} as error:
                print("matched", type(error).__name__)
            except IndexError:
                print("fallback")
            """, expected);
    }

    [Theory]
    [InlineData("(IndexError, 42)", "errors", "TypeError")]
    [InlineData("((IndexError,), ValueError)", "errors", "TypeError")]
    [InlineData("[IndexError]", "errors", "TypeError")]
    [InlineData("(IndexError,)", "(errors,)", "TypeError")]
    [InlineData("(IndexError,)", "(IndexError, missing)", "NameError")]
    [InlineData("(IndexError,)", "(IndexError, int)", "TypeError")]
    public async Task EveryTupleMemberIsResolvedAndValidatedBeforeMatching(string value, string handler, string expected)
    {
        await AssertOutput($$"""
            errors = {{value}}
            try:
                try:
                    raise IndexError("original")
                except {{handler}}:
                    print("incorrect match")
            except (TypeError, NameError) as error:
                print(type(error).__name__)
            """, expected + "\n");
    }

    [Fact]
    public async Task QualifiedTupleAndNamedTupleValuesPreserveExceptionIdentity()
    {
        await AssertOutput("""
            import json
            from collections import namedtuple
            class Handlers:
                errors = (json.JSONDecodeError, ValueError)
            Errors = namedtuple("Errors", "first second")
            for index in range(2):
                errors = Errors(json.JSONDecodeError, ValueError)
                try:
                    json.loads("{")
                except Handlers.errors as error:
                    print(type(error).__name__)
                try:
                    raise ValueError("plain")
                except errors as error:
                    print(type(error).__name__)
            """, "JSONDecodeError\nValueError\nJSONDecodeError\nValueError\n");
    }

    [Fact]
    public async Task HandlerValueIsLookedUpWhenAnExceptionIsRaised()
    {
        await AssertOutput("""
            errors = (TypeError,)
            try:
                errors = (ValueError,)
                raise ValueError("original")
            except errors as error:
                print(str(error))
            try:
                print("normal")
            except missing:
                print("incorrect lookup")
            """, "original\nnormal\n");
    }

    [Fact]
    public async Task InvalidHandlerBypassesSiblingHandlersAndPreservesFinallyExceptionContext()
    {
        await AssertOutput("""
            original = IndexError("original")
            errors = (IndexError, int)
            try:
                try:
                    raise original
                except errors:
                    print("incorrect match")
                except TypeError:
                    print("incorrect sibling")
                finally:
                    try:
                        raise
                    except TypeError as error:
                        print("finally", error.__context__ is original)
            except TypeError as error:
                print("outer", error.__context__ is original)
            """, "finally True\nouter True\n");
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
