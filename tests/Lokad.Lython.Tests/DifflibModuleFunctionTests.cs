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
        Assert.Equal("[  alpha\n, - bravo\n, + charlie\n]|alpha\nbravo\n|alpha\ncharlie\n|[apple, apply]", host.ReadText("/out.txt"));
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
matcher.set_seqs(["one", "two"], ["one", "too"])
parts.append(str(matcher.quick_ratio()))
parts.append(str(matcher.real_quick_ratio()))
write_text("/out.txt", "|".join(parts))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|[(0, 0, 2), (2, 3, 2), (4, 5, 0)]|[(equal, 0, 2, 0, 2), (insert, 2, 2, 2, 3), (equal, 2, 4, 3, 5)]|0.5|1", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("import difflib\ndifflib.unified_diff([1], [])\n", "expects an iterable of strings")]
    [InlineData("import difflib\ndifflib.restore([], 3)\n", "expects which to be 1 or 2")]
    [InlineData("import difflib\ndifflib.get_close_matches(\"x\", [], cutoff=2)\n", "expects cutoff between 0 and 1")]
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
