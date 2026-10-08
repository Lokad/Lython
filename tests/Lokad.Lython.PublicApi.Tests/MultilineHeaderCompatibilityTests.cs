using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MultilineHeaderCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "header-preserves-explicit-joins-and-fstring-expression", "if (\n    True):\n    print(f\"{1 + \\\n2}\")\nprint(1 + \\\n2)\n", "3\n3\n" };
        yield return new object[] { "header-for-equal-continuation-and-body-indent", "for x in [\n    1,\n    2]:\n    print(x)\nprint('after')\n", "1\n2\nafter\n" };
        yield return new object[] { "header-nested-if-elif-and-real-dedent", "for x in [1,2]:\n    if (\n        x==1):\n        print('one')\n    elif (\n        x==2):\n        print('two')\n    else:\n        print('never')\nprint('after')\n", "one\ntwo\nafter\n" };
        yield return new object[] { "header-while-and-else", "x=0\nwhile (\n    x<2):\n    x+=1\n    print(x)\nelse:\n    print('done')\n", "1\n2\ndone\n" };
        yield return new object[] { "header-function-and-class", "def choose(\n    value,\n    extra=2):\n    return value+extra\nclass Base:\n    def value(self): return 3\nclass Child(\n    Base):\n    pass\nprint(choose(4),Child().value())\n", "6 3\n" };
        yield return new object[] { "header-match-and-case", "match [\n    1,2]:\n    case [\n        first,*rest]:\n        print(first,rest)\n    case _:\n        print('never')\nprint('after')\n", "1 [2]\nafter\n" };
        yield return new object[] { "header-with-guest-context", "class Manager:\n    def __enter__(self): return 'entered'\n    def __exit__(self,kind,error,trace): print('exit')\nwith (\n    Manager()) as value:\n    print(value)\nprint('after')\n", "entered\nexit\nafter\n" };
        yield return new object[] { "header-comment-and-string-delimiters", "for value in [ # misleading ) ] }\n    '(',\n    # another ] and \" delimiter\n    '#)',\n    ']']:\n    print(value)\nprint('after')\n", "(\n#)\n]\nafter\n" };
        yield return new object[] { "header-tab-and-comment-continuations", "if (\n\t# ignored close: )\n\tTrue):\n\tprint('tab')\nprint('after')\n", "tab\nafter\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task LogicalIndentationIgnoresContinuationLines(string name, string source, string expected)
    {
        _ = name;
        foreach (var text in new[] { source, source.Replace("\n", "\r\n") })
        {
            var script = new LythonEngine().Compile(text);
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
        }
    }

    [Theory]
    [InlineData("if (\n    True):\nprint('missing body')\n")]
    [InlineData("for x in [\n    1,2]:\nprint('missing body')\n")]
    [InlineData("def function(\n    value):\nreturn value\n")]
    [InlineData("if True:\n    if (\n        True):\n    print('missing nested body')\n")]
    public void MissingSuiteIndentationRemainsInvalid(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.NotEmpty(script.Diagnostics);
    }
}
