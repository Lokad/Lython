using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormFeedSyntaxTests
{
    [Fact]
    public async Task IndentationMaskPreservesOriginalFailureSpan()
    {
        var compiled = new LythonEngine().Compile(" \fmissing\n");
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Equal("NameError", result.Failure?.ExceptionType);
            Assert.Equal(new LythonSourceSpan(2, 7, 1, 3), result.Failure?.Span);
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "tokens","x\f=\f1\f+\f2\nprint\f(x)","3\n" };
        yield return new object[] { "first_line","\fprint(7)","7\n" };
        yield return new object[] { "reset_first_line"," \t  \fprint(7)","7\n" };
        yield return new object[] { "block","if True:\n\f print(7)\nprint(8)","7\n8\n" };
        yield return new object[] { "reset_prefix","if True:\n \t \f print(7)\n print(8)","7\n8\n" };
        yield return new object[] { "reset_to_module","if True:\n print(7)\n \fprint(8)","7\n8\n" };
        yield return new object[] { "blank_comment_lines","\f# comment\f\n \f\nif True:\n \f # comment\f\n\f print(7)","7\n" };
        yield return new object[] { "grouped","print(\n \f 1,\f2\n)","1 2\n" };
        yield return new object[] { "explicit_join","x=1\f+\\\n\f 2\nprint(x)","3\n" };
        yield return new object[] { "tab_after_reset","if True:\n \f\tprint(7)\n\tprint(8)","7\n8\n" };
        yield return new object[] { "plain_literal","print(repr('a\fb'))","'a\\x0cb'\n" };
        yield return new object[] { "raw_literal","print(repr(r'a\fb'))","'a\\x0cb'\n" };
        yield return new object[] { "triple_literal","print(repr('''a\n\f b'''))","'a\\n\\x0c b'\n" };
        yield return new object[] { "formatted_literal","print(repr(f'a\fb'))","'a\\x0cb'\n" };
        yield return new object[] { "formatted_field","print(f\"{\f1\f+\f2\f}\")","3\n" };
        yield return new object[] { "debug_field","print(repr(f\"{1\f=}\"))","'1\\x0c=1'\n" };
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
    [InlineData("\f print(1)")]
    [InlineData("if True:\n\fprint(1)")]
    [InlineData("if True:\n  print(1)\n\f print(2)")]
    [InlineData("x=1\\\f\n+2")]
    [InlineData("x=1\\ \f\n+2")]
    [InlineData("x=1\u000b+2")]
    public void InvalidNamesAndScopeConflictsFailBeforeEffects(string source)
        => Assert.False(new LythonEngine().Compile("print('effect')\n" + source).IsValid);
}
