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
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|True|False|[alpha.txt, alps.txt]", host.ReadText("/out.txt"));
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
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("False|True|True|[abc, axc]", host.ReadText("/out.txt"));
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
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("True|[é😀.txt]", host.ReadText("/out.txt"));
    }
}
