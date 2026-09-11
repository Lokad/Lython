using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringMethodCompatibilityTests
{
    [Theory]
    [InlineData("\"banana\".replace(\"na\", \"X\", 1)", "baXna")]
    [InlineData("\"banana\".replace(\"na\", \"X\", 0)", "banana")]
    [InlineData("\" a  b \".split(None, 0)", "['a  b ']")]
    [InlineData("\" a  b \".split(None, 1)", "['a', 'b ']")]
    [InlineData("\"a,b,c\".split(\",\", 0)", "['a,b,c']")]
    [InlineData("\"a,b,c\".split(\",\", 1)", "['a', 'b,c']")]
    [InlineData("\"a,b,c\".rsplit(\",\", 1)", "['a,b', 'c']")]
    [InlineData("\" a  b \".rsplit(None, 1)", "[' a', 'b']")]
    [InlineData("str(\"banana\".index(\"na\"))", "2")]
    [InlineData("str(\"banana\".index(\"na\", 3))", "4")]
    [InlineData("str(\"banana\".rfind(\"na\"))", "4")]
    [InlineData("str(\"banana\".rfind(\"na\", 0, -1))", "2")]
    [InlineData("str(\"banana\".rindex(\"na\"))", "4")]
    [InlineData("\"xyabcxy\".strip(\"xy\")", "abc")]
    [InlineData("\"xyabcxy\".lstrip(\"xy\")", "abcxy")]
    [InlineData("\"xyabcxy\".rstrip(\"xy\")", "xyabc")]
    [InlineData("\"hELLO world\".capitalize()", "Hello world")]
    [InlineData("\"straße\".upper()", "STRASSE")]
    [InlineData("\"ß\".capitalize()", "Ss")]
    [InlineData("\"İ\".lower()", "i\u0307")]
    [InlineData("str(\"ß\".islower())", "True")]
    [InlineData("\"AbÇ\".swapcase()", "aBç")]
    [InlineData("\"they're bill's\".title()", "They'Re Bill'S")]
    [InlineData("\"abc\".center(7)", "  abc  ")]
    [InlineData("\"abc\".center(8, \".\")", "..abc...")]
    [InlineData("\"ab\".center(5)", "  ab ")]
    [InlineData("\"a\".center(4)", " a  ")]
    [InlineData("str(\"aaa\".rsplit(\"aa\"))", "['a', '']")]
    [InlineData("str(\"aaaaa\".rsplit(\"aa\", 2))", "['a', '', '']")]
    [InlineData("\"abc\".ljust(5, \".\")", "abc..")]
    [InlineData("\"abc\".rjust(5, \".\")", "..abc")]
    [InlineData("\"42\".zfill(5)", "00042")]
    [InlineData("\"abc\".ljust(True, \".\")", "abc")]
    [InlineData("\"abc\".rjust(False, \".\")", "abc")]
    [InlineData("\"hello\".find(\"l\", True)", "2")]
    [InlineData("\"-42\".zfill(5)", "-0042")]
    [InlineData("\"a\\tb\".expandtabs()", "a       b")]
    [InlineData("\"a\\tb\".expandtabs(2)", "a b")]
    [InlineData("\"{} {}\".format(\"a\", \"b\")", "a b")]
    [InlineData("\"{name}:{count}\".format(name=\"alpha\", count=2)", "alpha:2")]
    [InlineData("\"{1}:{name}:{0}\".format(\"left\", \"right\", name=\"alpha\")", "right:alpha:left")]
    [InlineData("\"{name}:{count}\".format_map({\"name\": \"alpha\", \"count\": 2})", "alpha:2")]
    [InlineData("\"{items[0]}\".format(items=[\"a\", \"b\"])", "a")]
    [InlineData("\"{user[name]}\".format(user={\"name\": \"alpha\"})", "alpha")]
    [InlineData("\"{p.name}\".format(p=Path(\"/tmp/demo.txt\"))", "demo.txt")]
    [InlineData("str(\"banana\".find(\"na\", 3))", "4")]
    [InlineData("str(\"banana\".find(\"na\", 0, -1))", "2")]
    [InlineData("str(\"banana\".find(\"\", 2, 2))", "2")]
    [InlineData("str(\"banana\".count(\"na\", 3))", "1")]
    [InlineData("str(\"banana\".count(\"\", 2, 4))", "3")]
    [InlineData("str(\"hello.py\".startswith((\"he\", \"x\")))", "True")]
    [InlineData("str(\"hello.py\".endswith((\".txt\", \".py\")))", "True")]
    [InlineData("str(\"hello.py\".startswith(\"ell\", 1, 4))", "True")]
    [InlineData("str(\"hello.py\".endswith(\"lo\", 0, 5))", "True")]
    [InlineData("str(\"a\".startswith(\"\", 5))", "False")]
    [InlineData("str(\"a\".endswith(\"\", 5))", "False")]
    [InlineData("str(\"a=b=c\".rpartition(\"=\"))", "('a=b', '=', 'c')")]
    [InlineData("str(\"a\\rb\".splitlines())", "['a', 'b']")]
    [InlineData("str(\"a\\r\\nb\".splitlines(True))", "['a\\r\\n', 'b']")]
    [InlineData("str(\"a\vb\".splitlines())", "['a', 'b']")]
    [InlineData("str(\"a" + "\u2028" + "b\".splitlines())", "['a', 'b']")]
    [InlineData("str(\"ABC\".isupper())", "True")]
    [InlineData("str(\"AbC\".isupper())", "False")]
    [InlineData("str(\"é\".isalpha())", "True")]
    [InlineData("str(\"²\".isdigit())", "True")]
    [InlineData("str(\"é2\".isalnum())", "True")]
    [InlineData("str(\"\\t\\n\".isspace())", "True")]
    [InlineData("str(\"\".isalpha())", "False")]
    [InlineData("str(\"42\".isupper())", "False")]
    [InlineData("str(\"abc\".find(\"\", 2, 1))", "-1")]
    [InlineData("str(\"abc\".count(\"\", 2, 1))", "0")]
    [InlineData("str(\"abc\".rfind(\"\", 2, 1))", "-1")]
    [InlineData("str(\"abc\".find(\"b\", 2, 1))", "-1")]
    [InlineData("str(\"abc\".startswith(\"\", 2, 1))", "False")]
    [InlineData("str(\"abc\".endswith(\"\", 2, 1))", "False")]
    public void StringMethods_ExposeRepresentativePythonShapedBehavior(string expression, string expected)
    {
        Assert.Equal(expected, EvaluateToString(expression));
    }

    [Fact]
    public void StringMethods_AcceptKeywordShapedOptionalArguments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append("banana".replace(old="na", new="X", count=1))
vals.append(str("a,b,c".split(sep=",", maxsplit=1)))
vals.append(str("a,b,c".rsplit(sep=",", maxsplit=1)))
vals.append("xyabcxy".strip(chars="xy"))
vals.append("abc".center(width=7))
vals.append("42".zfill(width=5))
vals.append("{name}:{count}".format(name="alpha", count=2))
vals.append("{name}".format_map({"name": "beta"}))
vals.append("{items[0]}".format(items=["x", "y"]))
vals.append("{user[name]}".format(user={"name": "gamma"}))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("baXna|['a', 'b,c']|['a,b', 'c']|abc|  abc  |00042|alpha:2|beta|x|gamma", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StringMethods_ComposeInOrdinaryScriptFlows()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
parts = "  alpha\tbeta gamma  ".split(maxsplit=1)
vals.append(str(parts))
vals.append(parts[0].upper())
vals.append(parts[1].title())
vals.append(str("A1".isalnum()))
vals.append(str(" \n".isspace()))
vals.append("==alpha==".strip("="))
vals.append(str("hello.py".endswith((".txt", ".py"))))
vals.append(str("banana".find("na", 3)))
vals.append(str("a=b=c".rpartition("=")))
vals.append(str("banana".rfind("na")))
vals.append("abc".ljust(5, "."))
vals.append("a\tb".expandtabs(2))
vals.append("{} {}".format("a", "b"))
vals.append("a\rb".splitlines()[1])
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['alpha', 'beta gamma  ']|ALPHA|Beta Gamma  |True|True|alpha|True|4|('a=b', '=', 'c')|4|abc..|a b|a b|b", host.ReadText("/out.txt"));
    }

    private static string EvaluateToString(string expression)
    {
        var source = $$"""
from pathlib import Path
return str({{expression}})
""";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        return Assert.IsType<string>(result.ReturnValue);
    }
}
