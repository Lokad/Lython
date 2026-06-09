using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DifflibModuleFunctionTests
{
    [Fact]
    public void DifflibModule_UnifiedAndContextDiff_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import difflib

left = "one\ntwo\nthree\n".splitlines(True)
right = "one\n2\nthree\nfour\n".splitlines(True)
unified = "".join(difflib.unified_diff(left, right, fromfile="old.py", tofile="new.py", n=1))
context = "".join(difflib.context_diff(left, right, fromfile="old.py", tofile="new.py", n=1))
write_text("/out.txt", unified + "\n--\n" + context)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "--- old.py\n" +
            "+++ new.py\n" +
            "@@ -1,3 +1,4 @@\n" +
            " one\n" +
            "-two\n" +
            "+2\n" +
            " three\n" +
            "+four\n" +
            "\n--\n" +
            "*** old.py\n" +
            "--- new.py\n" +
            "***************\n" +
            "*** 1,3 ****\n" +
            "  one\n" +
            "! two\n" +
            "  three\n" +
            "--- 1,4 ----\n" +
            "  one\n" +
            "! 2\n" +
            "  three\n" +
            "+ four\n",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void DifflibModule_NdiffRestoreAndCloseMatches_ComposeInScratchScripts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from difflib import get_close_matches, ndiff, restore

delta = list(ndiff(["alpha\n", "bravo\n"], ["alpha\n", "charlie\n"]))
left = "".join(restore(delta, 1))
right = "".join(restore(delta, 2))
matches = get_close_matches("appel", ["ape", "apple", "apply", "maple"], n=2, cutoff=0.5)
write_text("/out.txt", str(delta) + "|" + left + "|" + right + "|" + str(matches))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[  alpha\n, - bravo\n, + charlie\n]|alpha\nbravo\n|alpha\ncharlie\n|[apply, apple]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DifflibModule_SequenceMatcher_ReportsRatioBlocksAndOpcodes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import difflib

matcher = difflib.SequenceMatcher(None, "abcd", "abXcd")
parts = []
parts.append(str(matcher.ratio() > 0.88))
parts.append(str(matcher.get_matching_blocks()))
parts.append(str(matcher.get_opcodes()))
match = matcher.find_longest_match()
parts.append(str(match.a) + ":" + str(match[1]) + ":" + str(list(match)))
parts.append(str(matcher.get_grouped_opcodes(1)))
matcher.set_seqs(["one", "two"], ["one", "too"])
parts.append(str(matcher.quick_ratio()))
parts.append(str(matcher.real_quick_ratio()))
matcher.set_seq1(["zero"])
parts.append(str(matcher.ratio()))
write_text("/out.txt", "|".join(parts))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|[Match(a=0, b=0, size=2), Match(a=2, b=3, size=2), Match(a=4, b=5, size=0)]|[(equal, 0, 2, 0, 2), (insert, 2, 2, 2, 3), (equal, 2, 4, 3, 5)]|0:0:[0, 0, 2]|[[(equal, 1, 2, 1, 2), (insert, 2, 2, 2, 3), (equal, 2, 3, 3, 4)]]|0.5|1|0", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DifflibModule_JunkPredicatesDifferAndHtmlDiff_HandleCommonAgentUsage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import difflib

def ignore_hash(line):
    return line == "#\n"

def ignore_space(ch):
    return ch == " "

delta = list(difflib.ndiff(["alpha\n", "bravo\n"], ["alpha\n", "brvvo\n"], None, ignore_space))
compared = list(difflib.Differ(ignore_hash, ignore_space).compare(["#\n", "a b\n"], ["#\n", "ab\n"]))
html = difflib.HtmlDiff(2).make_file(["a\tb\n"], ["a c\n"], "left", "right", True, 1, "utf-8")
table = difflib.HtmlDiff().make_table(["same\n", "old\n"], ["same\n", "new\n"])

parts = [
    str(difflib.IS_LINE_JUNK("\n")),
    str(difflib.IS_LINE_JUNK("#\n")),
    str(difflib.IS_CHARACTER_JUNK(" ")),
    str(difflib.IS_CHARACTER_JUNK("x")),
    str("?   ^\n" in delta),
    str(compared[0] == "  #\n"),
    str("?  -\n" in compared),
    str("<!DOCTYPE html>" in html),
    str("<table class=\"diff\"" in html),
    str("charset=\"utf-8\"" in html),
    str("left" in html and "right" in html),
    str("diff_chg" in html),
    str("a&nbsp;b" in html),
    str("<table class=\"diff\"" in table and "diff_chg" in table),
]
write_text("/out.txt", "|".join(parts))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|True|False|True|True|True|True|True|True|True|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DifflibModule_DiffBytes_ReturnsByteLines()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import difflib

delta = list(difflib.diff_bytes(difflib.unified_diff, [b"alpha\n"], [b"beta\n"], b"old", b"new", None, None, 0, b"\n"))
write_text("/out.txt", str(delta))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[b'--- old\\x0a', b'+++ new\\x0a', b'@@ -1 +1 @@\\x0a', b'-alpha\\x0a', b'+beta\\x0a']", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("import difflib\ndifflib.unified_diff([1], [])\n", "expects a and b to be iterables of strings")]
    [InlineData("import difflib\ndifflib.ndiff([], [], linejunk=1)\n", "expects a callable or None")]
    [InlineData("import difflib\ndifflib.restore([], 3)\n", "expects which to be 1 or 2")]
    [InlineData("import difflib\ndifflib.get_close_matches(\"x\", [], n=0)\n", "expects n to be positive")]
    [InlineData("import difflib\ndifflib.get_close_matches(\"x\", [], cutoff=2)\n", "expects cutoff between 0 and 1")]
    [InlineData("import difflib\ndifflib.diff_bytes(difflib.unified_diff, [1], [])\n", "expects an iterable of bytes")]
    [InlineData("import difflib\ndifflib.HtmlDiff().make_table([1], [])\n", "expects iterables of strings")]
    [InlineData("import difflib\ndifflib.SequenceMatcher().find_longest_match(\"x\")\n", "expects an integer or None")]
    [InlineData("import difflib\ndifflib.SequenceMatcher().missing()\n", "has no member 'missing'")]
    public void DifflibModule_NearMisses_FailPrecisely(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }
}
