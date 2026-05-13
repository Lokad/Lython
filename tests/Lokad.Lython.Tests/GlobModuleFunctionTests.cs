using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

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
vals.append(str(sorted(item.as_posix() for item in glob.glob("/repo/docs/*.md"))))
vals.append(str(sorted(item.as_posix() for item in glob.iglob("/repo/docs/**/*.md", recursive=True))))
vals.append(glob.escape("/repo/docs/[draft]*.md"))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[/repo/docs/a.md]|[/repo/docs/a.md, /repo/docs/sub/c.md]|/repo/docs/[[]draft][*].md", host.ReadText("/out.txt"));
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
vals = sorted(item.as_posix() for item in glob.glob("**/*.py", recursive=True))
write_text("/out.txt", str(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[/repo/a.py, /repo/sub/b.py]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobModule_HiddenAndRecursiveContracts_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/.top.py", "top");
        host.SeedFile("/repo/sub/.hidden.py", "hidden");
        host.SeedFile("/repo/sub/vis.py", "visible");

        var result = new LythonEngine().Run(
            """
import glob

vals = []
vals.append(str(sorted(item.as_posix() for item in glob.glob("*.py"))))
vals.append(str(sorted(item.as_posix() for item in glob.glob("**/*.py", recursive=True))))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[]|[/repo/sub/vis.py]", host.ReadText("/out.txt"));
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
vals.append(str(sorted(item.as_posix() for item in glob.glob("/repo/docs/*.md"))))
vals.append(str(sorted(item.as_posix() for item in glob.iglob("/repo/docs/**/*.md", recursive=True))))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("[/repo/docs/a.md]|[/repo/docs/a.md, /repo/docs/sub/c.md]", host.ReadText("/out.txt"));
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
glob.glob("*.py", root_dir="/repo")
""",
        "compile",
        "glob.glob(pathname[, recursive]) expects one or two arguments.")]
    [InlineData(
        """
import glob
glob.iglob("*.py", include_hidden=True)
""",
        "compile",
        "glob.iglob(pathname[, recursive]) expects one or two arguments.")]
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
            Assert.Equal(exceptionType, result.Failure!.ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }
}
