using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexRewriteScenarioTests
{
    [Fact]
    public void BasicSubstitutionFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "RegexRewrite", "BasicSubstitution"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.Equal(fixture.Result.Success, result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void InvalidRegexFixture_ReturnsExpectedFailure()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "RegexRewrite", "InvalidPattern"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(fixture.Result.ExceptionType, result.Failure?.ExceptionType);
    }

    [Fact]
    public void PatternError_PreservesLegacyErrorAliasIdentity()
    {
        var result = new LythonEngine().Run(
            """
import re
return re.error is re.PatternError
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
    }

    [Fact]
    public void SearchSplitAndEscapeFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "RegexRewrite", "SearchSplitAndEscape"));
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void MultiFileCompiledPatternFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "RegexRewrite", "MultiFileCompiledPattern"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.Equal(fixture.Result.Success, result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void MatchObject_ExposesGroupStartEndAndSpan()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
m = re.search("beta", "alpha beta gamma")
__lython_file = open("/out.txt", "w")
__lython_file.write(m.group() + "|" + str(m.start()) + "|" + str(m.end()) + "|" + str(m.span()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("beta|6|10|(6, 10)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SplitWithoutMatch_ReturnsOriginalStringAsSinglePart()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
__lython_file = open("/out.txt", "w")
__lython_file.write(str(re.split("z+", "alpha")))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("['alpha']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MatchGroup_WithArgument_SupportsIndexedAndNamedGroups()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
pat = re.compile("(?P<word>beta)")
m = pat.search("alpha beta")
__lython_file = open("/out.txt", "w")
__lython_file.write(m.group(0) + "|" + m.group(1) + "|" + m.group("word"))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("beta|beta|beta", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Split_WithWrongArgumentType_FailsWithTypeError()
    {
        var result = new LythonEngine().Run(
            """
import re
re.split(1, "alpha")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("expects pattern to be a string or compiled regex pattern", StringComparison.Ordinal));
    }

    [Fact]
    public void Split_PreservesLeadingAndTrailingEmptyParts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
parts = re.split(":+", ":a::b:")
__lython_file = open("/out.txt", "w")
__lython_file.write(parts[0] + "<>" + parts[1] + "<>" + parts[2] + "<>" + parts[3])
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("<>a<>b<>", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Split_WithCaptureGroups_ProjectsPythonShapeIncludingUnsetCaptures()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
vals = []
vals.append(str(re.split("(:+)", ":a::b:")))
vals.append(str(re.split("(a)?b", "b ab")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("['', ':', 'a', '::', 'b', ':', '']|['', None, ' ', 'a', '']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MatchAndSearch_DistinguishPrefixFromInnerMatch()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
vals = []
vals.append(str(re.match("abc", "zabc") is None))
vals.append(str(re.search("abc", "zabc").start()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FindIter_WithZeroLengthMatches_AdvancesLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re

parts = []
for match in re.finditer("x*", "ab"):
    parts.append(str(match.span()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("(0, 0)|(1, 1)|(2, 2)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UnicodeMatch_SpanUsesPythonCharacterOffsets()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
m = re.search("😀", "a😀b")
__lython_file = open("/out.txt", "w")
__lython_file.write(m.group() + "|" + str(m.start()) + "|" + str(m.end()) + "|" + str(m.span()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("😀|1|2|(1, 2)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UnicodeMatch_ValueProjectionHandlesAstralCharacters()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
m = re.search(".", "𝒜")
__lython_file = open("/out.txt", "w")
__lython_file.write(m.group() + "|" + str(m.span()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("𝒜|(0, 1)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SubWithoutMatches_ReturnsOriginalTextAndEscapeHandlesMultipleMetacharacters()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
vals = []
vals.append(re.sub("z+", "x", "alpha"))
vals.append(re.escape("a.b[c]?"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("alpha|a\\.b\\[c\\]\\?", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ModuleRegexFunctions_RejectFlagsWhenPassedCompiledPattern()
    {
        var result = new LythonEngine().Run(
            """
import re
pat = re.compile("alpha")
re.search(pat, "alpha", re.IGNORECASE)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("no flags when pattern is compiled", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        """
import re
re.sub("a", "x", "alpha", count="1")
""",
        "compile",
        "expects count to be an integer")]
    [InlineData(
        """
import re
re.split("a", "alpha", maxsplit=2147483648)
""",
        "ValueError",
        "maxsplit is out of range")]
    public void ModuleRegexFunctions_ValidateCountAndMaxsplitContracts(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (exceptionType == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(exceptionType, result.Failure?.ExceptionType);
            Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompiledPatternsFlagsFindIterAndSubn_WorkLikePythonSubset()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
pat = re.compile("alpha", re.IGNORECASE)
items = []
for m in pat.finditer("Alpha alpha ALPHA"):
    items.append(m.group())
text, count = pat.subn("x", "Alpha alpha ALPHA")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + text + "|" + str(count))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['Alpha', 'alpha', 'ALPHA']|x x x|3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CompiledPatterns_SupportCallableSubAndSubnInScriptLikeRewriters()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re

ATTR_RE = re.compile(r'(?P<key>[A-Za-z][A-Za-z0-9_-]*)=(?P<value>[A-Za-z][A-Za-z0-9_-]*)')
line = 'src=a other=b'
keys = []
for match in ATTR_RE.finditer(line):
    keys.append(match.group("key"))

def replacer(match):
    if match.group("key") != "src":
        return match.group(0)
    return "src=x"

replaced, count = ATTR_RE.subn(replacer, line, count=1)
stripped = re.compile(r"(?<!\!)\[(?P<label>[^\]]+)\]\((?P<url>[^)]+)\)").sub(lambda match: match.group("label"), "[a](u) [b](v)")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(keys) + "|" + replaced + "|" + str(count) + "|" + stripped)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['src', 'other']|src=x other=b|1|a b", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FindIterMatches_CanBeListedSortedAndReassembledLikeScriptBlocks()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re

LINK_RE = re.compile(r"\[(?P<label>[^\]]+)\]\((?P<url>[^)]+)\)")
text = "[a](u) [b](v) [c](w)"
matches = list(LINK_RE.finditer(text))
parts = []
cursor = 0
for idx, match in enumerate(matches):
    parts.append(text[cursor:match.start()])
    if idx == 1:
        parts.append(match.group(0))
    else:
        parts.append(match.group("label"))
    cursor = match.end()
parts.append(text[cursor:])
__lython_file = open("/out.txt", "w")
__lython_file.write(str(len(matches)) + "|" + "".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3|a [b](v) c", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ExpandedRegexSurface_ExposesMetadataRangesLazyIteratorsAndErrors()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re

pat = re.compile(r"(?P<word>a)(?P<opt>b)?", re.I | re.NOFLAG)
m = pat.search("zzA zzab", pos=2, endpos=5)
groupdict = m.groupdict("-")
vals = []
vals.append(pat.pattern)
vals.append(str(pat.flags == (re.I | re.U)))
vals.append(str(pat.groups))
vals.append(str(pat.groupindex["word"]) + ":" + str(pat.groupindex["opt"]))
vals.append(m.group(0) + ":" + str(m.pos) + ":" + str(m.endpos))
vals.append(str(m.start("word")) + ":" + str(m.end("opt")) + ":" + str(m.span("opt")))
vals.append(str(m.groups("-")))
vals.append(groupdict["word"] + ":" + groupdict["opt"])
vals.append(m.expand(r"<\g<word>-\2>"))
vals.append(str(m.lastindex) + ":" + str(m.lastgroup))
vals.append(str(m.re is pat) + ":" + m.string)

it = pat.finditer("ab a")
vals.append(next(it).group(0))
vals.append(str([item.group(0) for item in it]))
vals.append(next(it, "done"))

vals.append(re.search("a", "xxa", pos=2).group())
vals.append(str(re.findall("a", "a a a", pos=2, endpos=4)))
vals.append(re.sub("a", "x", "a a a", pos=2, endpos=4))
subn_text, subn_count = pat.subn("z", "ab a", count=1, pos=1)
vals.append(subn_text + ":" + str(subn_count))
vals.append(str(pat.split("xaayaa", maxsplit=1, pos=1, endpos=4)))
re.purge()

try:
    re.compile("(")
except re.error as ex:
    vals.append("error:" + ex.type)

try:
    re.compile("[")
except re.PatternError as ex:
    vals.append("pattern:" + ex.type)

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("(?P<word>a)(?P<opt>b)?|True|2|1:2|A:2:5|2:-1:(-1, -1)|('A', '-')|A:-|<A->|1:word|True:zzA zzab|ab|['a']|done|a|['a']|a x a|ab z:1|['', 'a', None, 'ay']|error:PatternError|pattern:PatternError", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MatchGroup_SupportsTupleShapedOrdinaryPythonAccess()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re

m = re.match("((a)|(b))(c)?", "ac")
named = re.match("(?:(?P<a1>a)|(?P<b2>b))(?P<c3>c)?", "ac")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(m.group(2, 1)) + "|" + str(named.group("a1", "b2", "c3")))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("('a', 'a')|('a', None, 'c')", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import re
m = re.search("(a)", "a")
m.group(2)
""",
        "compile",
        "Regex group index is out of range.")]
    [InlineData(
        """
import re
m = re.search("(?P<label>a)", "a")
m.group("missing")
""",
        "compile",
        "Regex group 'missing' is not defined.")]
    public void MatchGroup_ErrorsStayPythonShapedInScriptCode(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (exceptionType == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(exceptionType, result.Failure?.ExceptionType);
            Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
        }
    }
}
