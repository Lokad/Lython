using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class OsModuleFunctionTests
{
    [Fact]
    public void OsPathModule_PureHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo/work");

        var result = new LythonEngine().Run(
            """
import os
from os import path

vals = []
vals.append(os.sep)
vals.append(path.join("/repo", "docs", "guide.md"))
vals.append(str(path.split("/repo/docs/guide.md")))
vals.append(str(path.splitext("/repo/docs/guide.md")))
vals.append(path.basename("/repo/docs/guide.md"))
vals.append(path.dirname("/repo/docs/guide.md"))
vals.append(str(path.isabs("/repo/docs")))
vals.append(path.normpath("/repo/docs/../site/./page.md"))
vals.append(path.abspath("../docs/guide.md"))
vals.append(path.relpath("/repo/docs/guide.md", "/repo"))
vals.append(path.commonpath(["/repo/docs/a.md", "/repo/docs/b.txt"]))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/|/repo/docs/guide.md|(/repo/docs, guide.md)|(/repo/docs/guide, .md)|guide.md|/repo/docs|True|/repo/site/page.md|/repo/docs/guide.md|docs/guide.md|/repo/docs", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsModule_HostMediatedFileTreeHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a.txt", "alpha");
        host.SeedFile("/repo/docs/sub/b.txt", "beta");

        var result = new LythonEngine().Run(
            """
import os

vals = []
vals.append(str(sorted(os.listdir("/repo/docs"))))
vals.append(os.getcwd())
vals.append(str(os.path.exists("/repo/docs/a.txt")))
vals.append(str(os.path.isfile("/repo/docs/a.txt")))
vals.append(str(os.path.isdir("/repo/docs")))
os.makedirs("/repo/out/nested", exist_ok=True)
os.rename("/repo/docs/a.txt", "/repo/docs/c.txt")
os.unlink("/repo/docs/sub/b.txt")
vals.append(str(os.path.isdir("/repo/out/nested")))
vals.append(str(os.path.exists("/repo/docs/c.txt")))
vals.append(str(os.path.exists("/repo/docs/sub/b.txt")))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[a.txt, sub]|/repo|True|True|True|True|True|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsListDir_DefaultsToCurrentDirectory()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.txt", "a");
        host.SeedFile("/repo/b.txt", "b");

        var result = new LythonEngine().Run(
            """
import os
write_text("/out.txt", str(sorted(os.listdir())))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[a.txt, b.txt]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsModule_MkdirRemoveAndPathPredicates_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/tmp.txt", "x");

        var result = new LythonEngine().Run(
            """
import os

os.mkdir("/repo/sub")
vals = []
vals.append(str(os.path.isdir("/repo/sub")))
os.remove("/repo/tmp.txt")
vals.append(str(os.path.exists("/repo/tmp.txt")))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsWalk_TraversesTopDownWithSortedNames()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/root.txt", "r");
        host.SeedFile("/repo/docs/a/alpha.txt", "a");
        host.SeedFile("/repo/docs/b/beta.txt", "b");

        var result = new LythonEngine().Run(
            """
import os

rows = []
for root, dirs, files in os.walk("/repo/docs"):
    rows.append(root + "|" + str(dirs) + "|" + str(files))
write_text("/out.txt", "\n".join(rows))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "/repo/docs|[a, b]|[root.txt]\n/repo/docs/a|[]|[alpha.txt]\n/repo/docs/b|[]|[beta.txt]",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsWalk_SupportsTopdownFalseAndPruning()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/root.txt", "r");
        host.SeedFile("/repo/docs/a/alpha.txt", "a");
        host.SeedFile("/repo/docs/b/beta.txt", "b");

        var result = new LythonEngine().Run(
            """
import os

top_rows = []
for root, dirs, files in os.walk("/repo/docs", topdown=True):
    if root == "/repo/docs":
        del dirs[0]
    top_rows.append(root + "|" + str(dirs) + "|" + str(files))

bottom_rows = []
for root, dirs, files in os.walk("/repo/docs", topdown=False, followlinks=False):
    bottom_rows.append(root + "|" + str(dirs) + "|" + str(files))

write_text("/out.txt", "\n".join(top_rows) + "\n---\n" + "\n".join(bottom_rows))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "/repo/docs|[b]|[root.txt]\n/repo/docs/b|[]|[beta.txt]\n---\n/repo/docs/a|[]|[alpha.txt]\n/repo/docs/b|[]|[beta.txt]\n/repo/docs|[a, b]|[root.txt]",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsWalk_OnErrorCallback_CanObserveTraversalFailures()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a/alpha.txt", "a");
        host.SeedFile("/repo/docs/b/beta.txt", "b");
        host.FailListDir("/repo/docs/a", "blocked a");

        var result = new LythonEngine().Run(
            """
import os

events = []
def onerror(ex):
    events.append(ex.type + ":" + ex.message)

rows = []
for root, dirs, files in os.walk("/repo/docs", onerror=onerror):
    rows.append(root + "|" + str(dirs) + "|" + str(files))

write_text("/out.txt", "\n".join(rows) + "\n---\n" + "\n".join(events))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "/repo/docs|[a, b]|[]\n/repo/docs/b|[]|[beta.txt]\n---\nRuntimeError:Host listdir failed: blocked a",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsModule_RmdirRemovedirsAndReplace_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a.txt", "alpha");
        host.SeedFile("/repo/docs/b.txt", "beta");
        host.SeedFile("/repo/tmp/keep.txt", "keep");
        host.SeedFile("/repo/tmp/sub/leaf.txt", "leaf");

        var result = new LythonEngine().Run(
            """
import os

os.replace("/repo/docs/a.txt", "/repo/docs/b.txt")
os.remove("/repo/tmp/sub/leaf.txt")
os.removedirs("/repo/tmp/sub")
os.mkdir("/repo/empty")
os.rmdir("/repo/empty")

vals = []
vals.append(read_text("/repo/docs/b.txt"))
vals.append(str(os.path.exists("/repo/docs/a.txt")))
vals.append(str(os.path.exists("/repo/tmp/sub")))
vals.append(str(os.path.exists("/repo/tmp")))
vals.append(str(os.path.exists("/repo/empty")))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha|False|False|True|False", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import os
os.path.commonpath([])
""",
        "compile",
        "non-empty iterable of strings")]
    [InlineData(
        """
import os
os.path.commonpath(["/repo/a", "docs/b"])
""",
        "ValueError",
        "mix absolute and relative")]
    [InlineData(
        """
import os
os.path.commonpath("/repo/docs")
""",
        "compile",
        "iterable of strings, not a single string")]
    [InlineData(
        """
import os
os.makedirs("/repo", exist_ok=False)
""",
        "RuntimeError",
        "target already exists")]
    public void OsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
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

    [Theory]
    [InlineData(
        """
import os
list(os.walk("/repo", topdown="yes"))
""",
        "expects a bool or None")]
    [InlineData(
        """
import os
list(os.walk("/repo", onerror="boom"))
""",
        "callable or None")]
    public void OsModule_ProvableNearMissContracts_FailAtCompileTime(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost("/repo"));

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
    }
}
