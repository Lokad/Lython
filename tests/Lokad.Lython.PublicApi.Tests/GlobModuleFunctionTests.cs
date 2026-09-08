using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GlobModuleFunctionTests
{
    [Fact]
    public void GlobModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a.md", "alpha");
        host.SeedFile("/repo/docs/b.txt", "bravo");
        host.SeedFile("/repo/docs/sub/c.md", "charlie");
        host.SeedFile("/repo/docs/.hidden.md", "hidden");

        var result = new LythonEngine().Run(
            """
import glob

vals = []
vals.append(str(sorted(glob.glob("/repo/docs/*.md"))))
vals.append(str(sorted(glob.iglob("/repo/docs/**/*.md", recursive=True))))
vals.append(glob.escape("/repo/docs/[draft]*.md"))
vals.append(str([glob.has_magic("*.md"), glob.has_magic("docs/a.md")]))
vals.append(glob.translate("*.md"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['/repo/docs/a.md']|['/repo/docs/a.md', '/repo/docs/sub/c.md']|/repo/docs/[[]draft][*].md|[True, False]|^(?!\\.)[^/]*\\.md$", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobModule_RelativePatternsResolveAgainstHostCwd()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.py", "a");
        host.SeedFile("/repo/sub/b.py", "b");

        var result = new LythonEngine().Run(
            """
import glob
vals = sorted(glob.glob("**/*.py", recursive=True))
__lython_file = open("/out.txt", "w")
__lython_file.write(str(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a.py', 'sub/b.py']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobModule_RootDirAndPathLikePatterns_ReturnStrings()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/pkg/a.py", "a");
        host.SeedFile("/repo/pkg/b.txt", "b");
        host.SeedFile("/repo/other/c.py", "c");

        var result = new LythonEngine().Run(
            """
import glob
from pathlib import Path

vals = []
vals.append(str(sorted(glob.glob("*.py", root_dir="/repo/pkg"))))
vals.append(str(sorted(glob.glob(Path("*.py"), root_dir=Path("/repo/pkg")))))
vals.append(str(sorted(glob.glob("/repo/pkg/*.py", root_dir="/repo/other"))))
vals.append(glob.glob("pkg/*.py")[0].replace("pkg/", ""))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a.py']|['a.py']|['/repo/pkg/a.py']|a.py", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobModule_HiddenAndRecursiveContracts_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/.top.py", "top");
        host.SeedFile("/repo/.hiddendir/c.py", "hidden dir");
        host.SeedFile("/repo/sub/.hidden.py", "hidden");
        host.SeedFile("/repo/sub/vis.py", "visible");

        var result = new LythonEngine().Run(
            """
import glob

vals = []
vals.append(str(sorted(glob.glob("*.py"))))
vals.append(str(sorted(glob.glob("*.py", include_hidden=True))))
vals.append(str(sorted(glob.glob(".*.py"))))
vals.append(str(sorted(glob.glob("**/*.py", recursive=True))))
vals.append(str(sorted(glob.glob("**/*.py", recursive=True, include_hidden=True))))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[]|['.top.py']|['.top.py']|['sub/vis.py']|['.hiddendir/c.py', '.top.py', 'sub/.hidden.py', 'sub/vis.py']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobModule_RecursiveDoubleStar_PreservesDuplicateMatches()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.py", "a");
        host.SeedFile("/repo/sub/b.py", "b");

        var result = new LythonEngine().Run(
            """
import glob
__lython_file = open("/out.txt", "w")
__lython_file.write(str(sorted(glob.glob("**/**/*.py", recursive=True))))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a.py', 'sub/b.py', 'sub/b.py']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobModule_IGlob_ReturnsOneShotIterator()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.py", "a");
        host.SeedFile("/repo/b.py", "b");

        var result = new LythonEngine().Run(
            """
import glob

it = glob.iglob("*.py")
first = list(it)
second = list(it)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(first) + "|" + str(second))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a.py', 'b.py']|[]", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task GlobModule_RunAsync_AwaitsAsynchronousHostOperations()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/a.md", "alpha");
        host.SeedFile("/repo/docs/b.txt", "bravo");
        host.SeedFile("/repo/docs/sub/c.md", "charlie");
        host.SeedFile("/repo/docs/.hidden.md", "hidden");

        var result = await new LythonEngine().RunAsync(
            """
import glob

vals = []
vals.append(str(sorted(glob.glob("/repo/docs/*.md"))))
vals.append(str(sorted(glob.iglob("/repo/docs/**/*.md", recursive=True))))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("['/repo/docs/a.md']|['/repo/docs/a.md', '/repo/docs/sub/c.md']", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import glob
glob.glob("**/*.py", recursive="yes")
""",
        "compile",
        "recursive to be a bool")]
    [InlineData(
        """
import glob
glob.glob("*.py", root_dir=1)
""",
        "compile",
        "root_dir to be path-like or None")]
    [InlineData(
        """
import glob
glob.iglob("*.py", include_hidden=1)
""",
        "compile",
        "include_hidden to be a bool")]
    [InlineData(
        """
import glob
glob.glob("*.py", dir_fd=1)
""",
        "compile",
        "dir_fd=...) is not supported")]
    [InlineData(
        """
import glob
glob.glob("*.py", True)
""",
        "compile",
        "expects one path-like argument plus supported keyword options")]
    public void GlobModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost("/repo"));

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
