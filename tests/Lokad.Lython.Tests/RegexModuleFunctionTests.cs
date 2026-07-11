using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RegexModuleFunctionTests
{
    [Theory]
    [InlineData("re.search(\"a+\", \"caaab\").group(0)", "aaa")]
    [InlineData("re.match(\"a+\", \"aaab\").span()", "(0, 3)")]
    [InlineData("str(re.fullmatch(\"abc\", \"abc\") is not None)", "True")]
    [InlineData("str(re.fullmatch(\"abc\", \"abcx\") is None)", "True")]
    [InlineData("re.findall(\"a.\", \"abac\")[0]", "ab")]
    [InlineData("len(re.findall(\"a.\", \"abac\"))", "2")]
    [InlineData("str(re.findall(\"a.\", \"abac\"))", "['ab', 'ac']")]
    [InlineData("str(list(re.findall(\"a.\", \"abac\")))", "['ab', 'ac']")]
    [InlineData("str(list(re.findall(\"(a)?b\", \"b ab b\")))", "['', 'a', '']")]
    [InlineData("str(list(re.findall(\"(a)|(x)\", \"axa\")))", "[('a', ''), ('', 'x'), ('a', '')]")]
    [InlineData("str(re.findall(\"(a)|(x)\", \"axa\")[1])", "('', 'x')")]
    [InlineData("re.findall(\"(a)|(x)\", \"axa\")[1][1]", "x")]
    [InlineData("re.search(\".\", \"𝒜\").group(0)", "𝒜")]
    [InlineData("re.sub(\"a+\", \"x\", \"caaab\")", "cxb")]
    [InlineData("str(re.subn(\"a+\", \"x\", \"caaab\"))", "('cxb', 1)")]
    [InlineData("str(re.split(\":+\", \":a::b:\", 1))", "['', 'a::b:']")]
    [InlineData("str(re.split(\"(:+)\", \":a::b:\"))", "['', ':', 'a', '::', 'b', ':', '']")]
    [InlineData("str(re.split(\"(a)?b\", \"b ab\"))", "['', None, ' ', 'a', '']")]
    [InlineData("re.escape(\"a+b?(é)\")", "a\\+b\\?\\(é\\)")]
    [InlineData("re.escape(\"a b#c&d\")", "a\\ b\\#c\\&d")]
    [InlineData("int(re.UNICODE)", "32")]
    [InlineData("str((int(re.I), int(re.M), int(re.S), int(re.X), int(re.A), int(re.U), int(re.DEBUG)))", "(2, 8, 16, 64, 256, 32, 128)")]
    [InlineData("re.compile(\"x\").flags", "32")]
    [InlineData("re.compile(\"(?i)x\").flags", "34")]
    [InlineData("str(re.findall(r\"\\w+\", \"é_1\", re.A))", "['_1']")]
    [InlineData("str(re.search(\"^a\", \"x\\na\", re.MULTILINE).start())", "2")]
    [InlineData("str(re.fullmatch(\"a.b\", \"a\\nb\", re.DOTALL).span())", "(0, 3)")]
    public void RegexModule_ModuleFunctions_HaveDirectCoverage(string expression, string expected)
    {
        Assert.Equal(expected, EvaluateToString(expression));
    }

    [Theory]
    [InlineData("re.compile(\"a+\").search(\"caaab\").group(0)", "aaa")]
    [InlineData("re.compile(\"a+\").findall(\"caaab aa\")[1]", "aa")]
    [InlineData("len(re.compile(\"a+\").findall(\"caaab aa\"))", "2")]
    [InlineData("str(list(re.compile(\"(a)?b\").findall(\"b ab b\")))", "['', 'a', '']")]
    [InlineData("str(list(re.compile(\"(a)|(x)\").findall(\"axa\")))", "[('a', ''), ('', 'x'), ('a', '')]")]
    [InlineData("re.compile(\"(a)|(x)\").findall(\"axa\")[1][1]", "x")]
    [InlineData("re.compile(\".\").search(\"𝒜\").group(0)", "𝒜")]
    [InlineData("str(re.compile(\"a+\").subn(\"x\", \"caaab aa\", 1))", "('cxb aa', 1)")]
    [InlineData("str(re.compile(\":+\").split(\":a::b:\", 1))", "['', 'a::b:']")]
    [InlineData("str(re.compile(\"(:+)\").split(\":a::b:\"))", "['', ':', 'a', '::', 'b', ':', '']")]
    [InlineData("str([m.group(0) for m in re.compile(\"a+\").finditer(\"caaab aa\")])", "['aaa', 'aa']")]
    [InlineData("re.search(\"(?P<label>ab)\", \"ab\").group(\"label\")", "ab")]
    [InlineData("str(re.search(\"(a)(b)\", \"ab\").group(1, 2))", "('a', 'b')")]
    [InlineData("str(re.search(\"(a)(b)\", \"ab\").span())", "(0, 2)")]
    public void RegexModule_PatternAndMatchObjects_HaveDirectCoverage(string expression, string expected)
    {
        Assert.Equal(expected, EvaluateToString(expression));
    }

    [Fact]
    public void RegexModule_FindAll_ProjectsRepresentativeResults()
    {
        var result = new LythonEngine().Run(
            """
import re
return re.findall("a.", "abac")
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<LythonRuntime.ReFindAllResult>(result.ReturnValue);
        Assert.Equal(["ab", "ac"], values.Items.Cast<string>().ToArray());
    }

    [Fact]
    public void RegexModule_CompiledPatternFindAll_ProjectsRepresentativeResults()
    {
        var result = new LythonEngine().Run(
            """
import re
return re.compile("a+").findall("caaab aa")
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<LythonRuntime.ReFindAllResult>(result.ReturnValue);
        Assert.Equal(["aaa", "aa"], values.Items.Cast<string>().ToArray());
    }

    [Fact]
    public void RegexModule_FindAll_ProjectsSingleCaptureResults()
    {
        var result = new LythonEngine().Run(
            """
import re
return re.findall("(a)?b", "b ab b")
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<LythonRuntime.ReFindAllResult>(result.ReturnValue);
        Assert.Equal(["", "a", ""], values.Items.Cast<string>().ToArray());
    }

    [Fact]
    public void RegexModule_FindAll_ProjectsTupleCaptureResults()
    {
        var result = new LythonEngine().Run(
            """
import re
return re.findall("(a)|(x)", "axa")
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<LythonRuntime.ReFindAllResult>(result.ReturnValue);
        Assert.Collection(
            values.Items,
            item => Assert.Equal(["a", ""], Assert.IsType<object[]>(item).Cast<string>().ToArray()),
            item => Assert.Equal(["", "x"], Assert.IsType<object[]>(item).Cast<string>().ToArray()),
            item => Assert.Equal(["a", ""], Assert.IsType<object[]>(item).Cast<string>().ToArray()));
    }

    private static string EvaluateToString(string expression)
    {
        var source = $$"""
import re
return str({{expression}})
""";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        return Assert.IsType<string>(result.ReturnValue);
    }
}
