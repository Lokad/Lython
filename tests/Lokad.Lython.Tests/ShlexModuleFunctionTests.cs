using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ShlexModuleFunctionTests
{
    [Fact]
    public void QuoteAndJoinMatchPythonShellSpelling()
    {
        var result = new LythonEngine().Run(
            """
import shlex

values = [
    shlex.quote(""),
    shlex.quote("abc-_.//:@%+=,"),
    shlex.quote("two words"),
    shlex.quote("can't"),
    shlex.quote("é"),
    shlex.quote("$HOME"),
    shlex.join(["git", "commit", "-m", "agent's change", "", "é"]),
    shlex.join("ab"),
]
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "''|abc-_.//:@%+=,|'two words'|'can'\"'\"'t'|'é'|'$HOME'|git commit -m 'agent'\"'\"'s change' '' 'é'|a b",
            result.ReturnValue);
    }

    [Fact]
    public void SplitMatchesPosixWhitespaceQuoteEscapeCommentAndUnicodeRules()
    {
        var result = new LythonEngine().Run(
            """
import shlex

values = [
    shlex.split(""),
    shlex.split("   \t\n"),
    shlex.split("alpha beta"),
    shlex.split("'alpha beta' gamma"),
    shlex.split('"alpha beta" gamma'),
    shlex.split("a\\ b c"),
    shlex.split('"a\\$b" "a\\qb"'),
    shlex.split("a''b \"\""),
    shlex.split("é 😀"),
    shlex.split("alpha # ignored\nbeta"),
    shlex.split("alpha # ignored\nbeta", comments=True),
    shlex.split("alpha#ignored\nbeta", comments=True),
]
return str(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "[[], [], ['alpha', 'beta'], ['alpha beta', 'gamma'], ['alpha beta', 'gamma'], ['a b', 'c'], ['a\\\\$b', 'a\\\\qb'], ['ab', ''], ['é', '😀'], ['alpha', '#', 'ignored', 'beta'], ['alpha', 'beta'], ['alpha', 'beta']]",
            result.ReturnValue);
    }

    [Fact]
    public void SplitSupportsTheLegacyNonPosixCallShape()
    {
        var result = new LythonEngine().Run(
            """
import shlex

values = [
    shlex.split("'alpha beta' gamma", posix=False),
    shlex.split("a\\ b c", posix=False),
    shlex.split("a''b \"\"", posix=False),
    shlex.split("alpha#ignored\nbeta", comments=True, posix=False),
    shlex.split("trailing\\", posix=False),
]
return str(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "[['\\'alpha beta\\'', 'gamma'], ['a\\\\', 'b', 'c'], ['a\\'\\'b', '\"\"'], ['alphabeta'], ['trailing\\\\']]",
            result.ReturnValue);
    }

    [Fact]
    public void InvalidInputsAndUnterminatedTokensFailExplicitly()
    {
        var result = new LythonEngine().Run(
            """
import shlex

values = []
for action in [
    lambda: shlex.split("'unterminated"),
    lambda: shlex.split("trailing\\"),
    lambda: shlex.split(None),
    lambda: shlex.quote(1),
    lambda: shlex.join(["ok", 1]),
]:
    try:
        action()
    except Exception as ex:
        values.append(ex.type + ":" + ex.message)
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "ValueError:No closing quotation|ValueError:No escaped character|ValueError:s argument must not be None|TypeError:shlex.quote() argument must be str|TypeError:shlex.join() argument must be str",
            result.ReturnValue);
    }

    [Fact]
    public void StaticContractsRecognizeShlexAndItsCallShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import shlex
from shlex import quote, join, split

escaped = quote("two words")
command = join(["echo", escaped])
words = split(command, comments=False, posix=True)
""");
        var invalid = new LythonEngine().Compile(
            """
import shlex
shlex.quote()
shlex.join([], [])
shlex.split()
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(3, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
}
