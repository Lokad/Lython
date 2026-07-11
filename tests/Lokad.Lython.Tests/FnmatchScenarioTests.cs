using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FnmatchScenarioTests
{
    [Fact]
    public void FilterAndUppercaseFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "Fnmatch", "FilterAndUppercase"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

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
    public void FnmatchFunction_SupportsStarQuestionAndFilter()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import fnmatch
vals = []
vals.append(str(fnmatch.fnmatch("alpha.txt", "*.txt")))
vals.append(str(fnmatch.fnmatch("alpha.txt", "a?pha.*")))
vals.append(str(fnmatch.fnmatch("alpha.txt", "*.md")))
vals.append(str(fnmatch.filter(["alpha.txt", "beta.md", "alps.txt"], "a*.txt")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|True|False|['alpha.txt', 'alps.txt']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FnmatchFilter_WithNonStringItem_FailsAtCompileTime()
    {
        var result = new LythonEngine().Run(
            """
import fnmatch
fnmatch.filter(["alpha.txt", 1], "*.txt")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("iterable of strings", StringComparison.Ordinal));
    }

    [Fact]
    public void Fnmatch_AnchorsWholeStringAndSupportsQuestionMarks()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import fnmatch
vals = []
vals.append(str(fnmatch.fnmatch("ab", "a")))
vals.append(str(fnmatch.fnmatch("ab", "??")))
vals.append(str(fnmatch.fnmatch("abc", "a?c")))
vals.append(str(fnmatch.filter(["ab", "abc", "axc"], "a?c")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("False|True|True|['abc', 'axc']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Fnmatch_UsesUnicodeCharacterSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import fnmatch
vals = []
vals.append(str(fnmatch.fnmatch("é😀.txt", "é?.txt")))
vals.append(str(fnmatch.filter(["é😀.txt", "éab.txt"], "é?.txt")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("True|['é😀.txt']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Fnmatch_ExpandedSurfaceSupportsCaseTranslateAndBrackets()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import fnmatch
import re

vals = []
vals.append(str(fnmatch.fnmatchcase("alpha.TXT", "*.txt")))
vals.append(str(fnmatch.fnmatch("alpha.TXT", "*.txt")))
vals.append(str(fnmatch.fnmatch("beta.md", "[ab]*.m[!x]")))
vals.append(str(fnmatch.fnmatch("gamma.py", "[!ab]*.py")))
vals.append(str(fnmatch.fnmatch("b", "[a-c]")))
vals.append(str(fnmatch.fnmatch("[abc", "[abc")))
vals.append(str(fnmatch.fnmatch("]", "[]]")))
vals.append(str(fnmatch.fnmatch("-", "[-]")))
vals.append(str(fnmatch.fnmatch("q", "[z-aq]")))
vals.append(str(fnmatch.fnmatch("z", "[z-aq]")))
vals.append(str(fnmatch.filter(["a.py", "b.txt", "c.py", "A.py"], "[a-c].py")))
translated = fnmatch.translate("file[0-9]?.txt")
vals.append(translated)
vals.append(str(re.fullmatch(translated, "file7a.txt") is not None))
invalid_range = fnmatch.translate("[z-a]")
vals.append(invalid_range)
vals.append(str(re.fullmatch(invalid_range, "z") is None))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("False|False|True|True|True|True|True|True|True|False|['a.py', 'c.py']|^file[0-9].\\.txt$|True|^(?!)$|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FnmatchExpandedSurface_WithInvalidArguments_FailsAtCompileTime()
    {
        var result = new LythonEngine().Run(
            """
import fnmatch
fnmatch.fnmatchcase("alpha.txt", 1)
fnmatch.translate(1)
fnmatch.translate("*.txt", 1)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("fnmatch.fnmatchcase(name, pattern) expects two string arguments.", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("fnmatch.translate(pattern) expects a string pattern.", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("fnmatch.translate(pattern) expects one argument.", StringComparison.Ordinal));
    }
}
