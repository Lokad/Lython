using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ShlexLexerFunctionTests
{
    [Fact]
    public void ConstructorAttributesTokenMethodsAndIterationMatchPython()
    {
        var result = new LythonEngine().Run(
            """
import shlex

lexer = shlex.shlex("one 'two words'\nthree", "command.txt", True)
attributes = [
    lexer.infile,
    lexer.posix,
    lexer.eof is None,
    lexer.commenters,
    lexer.whitespace == " \t\r\n",
    lexer.whitespace_split,
    lexer.quotes,
    lexer.escape,
    lexer.escapedquotes,
    "é" in lexer.wordchars,
    lexer.punctuation_chars,
    lexer.lineno,
]
lexer.push_token("zero")
tokens = [lexer.get_token(), lexer.read_token(), lexer.get_token(), lexer.get_token()]
end = lexer.get_token()
return str(attributes) + "|" + "/".join(tokens) + "|" + str(end is None) + "|" + str(lexer.lineno)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "['command.txt', True, True, '#', True, False, '\\'\"', '\\\\', '\"', True, '', 1]|zero/one/two words/three|True|2",
            result.ReturnValue);
    }

    [Fact]
    public void PunctuationAndMutableCharacterClassesMatchPython()
    {
        var result = new LythonEngine().Run(
            """
import shlex

standard = shlex.shlex("a&&b;c|d", posix=True, punctuation_chars=True)
custom = shlex.shlex("alpha,beta # comment", posix=True, punctuation_chars=",")
custom.whitespace_split = True
custom.commenters = ""

mutable = shlex.shlex("a:b;c", posix=True)
mutable.whitespace = ";"
mutable.wordchars = mutable.wordchars + ":"
mutable.lineno = 7
mutable.infile = "changed"

legacy = shlex.shlex("'alpha beta' a-b")
return "|".join([
    "/".join(list(standard)),
    "/".join(list(custom)),
    "/".join(list(mutable)),
    str(mutable.lineno),
    mutable.infile,
    "/".join(list(legacy)),
    str(legacy.eof == ""),
])
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("a/&&/b/;/c/|/d|alpha/,/beta/#/comment|a:b/c|7|changed|'alpha beta'/a/-/b|True", result.ReturnValue);
    }

    [Fact]
    public void ReadableTextAndGzipHandlesAreAcceptedWithoutPathShortcuts()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/commands.txt", "echo 'two words'");

        var result = new LythonEngine().Run(
            """
import gzip
import shlex

with open("/repo/commands.txt", "r") as stream:
    plain = list(shlex.shlex(stream, "commands.txt", posix=True))

with gzip.open("/repo/commands.gz", "wt") as writer:
    writer.write("git commit -m 'message body'")
with gzip.open("/repo/commands.gz", "rt") as stream:
    compressed = list(shlex.shlex(stream, "commands.gz", posix=True))

return "/".join(plain) + "|" + "/".join(compressed)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("echo/two words|git/commit/-/m/message body", result.ReturnValue);
    }

    [Fact]
    public void ExplicitPushSourceReturnsToTheOriginalStream()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/nested.txt", "from-file");

        var result = new LythonEngine().Run(
            """
import shlex

lexer = shlex.shlex("outer", "outer.txt", True)
lexer.push_source("inner", "inner.txt")
first = list(lexer)

with open("/repo/nested.txt") as stream:
    second_lexer = shlex.shlex("tail", "tail.txt", True)
    second_lexer.push_source(stream, "nested.txt")
    second = list(second_lexer)
    pushed_closed = stream.closed

return "/".join(first) + "|" + lexer.infile + "|" + "/".join(second) + "|" + second_lexer.infile + "|" + str(pushed_closed)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("inner/outer|outer.txt|from/-/file/tail|tail.txt|True", result.ReturnValue);
    }

    [Fact]
    public async Task DefaultConstructorReadsOnlyTheHostMediatedStandardInput()
    {
        var syncHost = new MockLythonHost();
        syncHost.SeedStandardInput("alpha 'two words'");
        var sync = new LythonEngine().Run(
            "import shlex\nreturn '/'.join(list(shlex.shlex(posix=True)))\n",
            syncHost);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedStandardInput("beta \"three words\"");
        var asynchronous = await new LythonEngine().RunAsync(
            "import shlex\nreturn '/'.join(list(shlex.shlex(posix=True)))\n",
            asyncHost);

        var unavailable = new LythonEngine().Run(
            "import shlex\nshlex.shlex(posix=True)\n",
            new MockLythonHost());

        Assert.True(sync.Success, Describe(sync));
        Assert.Equal("alpha/two words", sync.ReturnValue);
        Assert.True(asynchronous.Success, Describe(asynchronous));
        Assert.Equal("beta/three words", asynchronous.ReturnValue);
        Assert.False(unavailable.Success);
        Assert.Equal("RuntimeError", unavailable.Failure?.ExceptionType);
        Assert.Contains("standard input is not available", unavailable.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticSourceInclusionAndUnsupportedInputsFailExplicitly()
    {
        var result = new LythonEngine().Run(
            """
import shlex

messages = []

lexer = shlex.shlex("include secret.txt", posix=True)
lexer.source = "include"
try:
    lexer.get_token()
except Exception as ex:
    messages.append(ex.type + ":" + ex.message)

for action in [
    lambda: shlex.shlex("x").sourcehook("secret.txt"),
    lambda: shlex.shlex([], posix=True),
    lambda: shlex.shlex("x", punctuation_chars=1),
    lambda: shlex.shlex("x").push_source([]),
    lambda: shlex.shlex("x").pop_source(),
]:
    try:
        action()
    except Exception as ex:
        messages.append(ex.type + ":" + ex.message)

try:
    fixed = shlex.shlex("x", punctuation_chars=True)
    fixed.punctuation_chars = "!"
except Exception as ex:
    messages.append(ex.type)

return "|".join(messages)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(7, Assert.IsType<string>(result.ReturnValue).Split('|').Length);
        Assert.Contains("NotImplementedError:automatic shlex source inclusion is unsupported", (string)result.ReturnValue, StringComparison.Ordinal);
        Assert.Contains("TypeError:shlex.shlex input must be a string or a supported readable text handle", (string)result.ReturnValue, StringComparison.Ordinal);
        Assert.Contains("IndexError:pop from an empty source stack", (string)result.ReturnValue, StringComparison.Ordinal);
        Assert.EndsWith("AttributeError", (string)result.ReturnValue, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticContractRecognizesTheLexerConstructorShape()
    {
        var valid = new LythonEngine().Compile(
            """
import shlex
lexer = shlex.shlex("a b", infile="cmd", posix=True, punctuation_chars=True)
tokens = list(lexer)
""");
        var invalid = new LythonEngine().Compile("import shlex\nshlex.shlex(1, 2, 3, 4, 5)\n");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3151");
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
}

