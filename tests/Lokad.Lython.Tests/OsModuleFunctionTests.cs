using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class OsModuleFunctionTests
{
    [Fact]
    public void OsPathModule_PureHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo/work");
        host.SeedFile("/repo/docs/a.txt", string.Empty);

        var result = new LythonEngine().Run(
            """
import os
from pathlib import Path
from os import path

vals = []
vals.append(os.sep)
vals.append(os.fspath(Path("/repo/docs/guide.md")))
vals.append(path.join(Path("/repo"), "docs", Path("guide.md")))
vals.append(path.join("/repo", "docs", "guide.md"))
vals.append(str(path.split("/repo/docs/guide.md")))
vals.append(str(path.splitext("/repo/docs/guide.md")))
vals.append(path.basename("/repo/docs/guide.md"))
vals.append(path.dirname("/repo/docs/guide.md"))
vals.append(str(path.isabs("/repo/docs")))
vals.append(path.normpath("/repo/docs/../site/./page.md"))
vals.append(path.abspath("../docs/guide.md"))
vals.append(path.realpath("../docs/guide.md"))
vals.append(path.relpath("/repo/docs/guide.md", "/repo"))
vals.append(path.commonpath(["/repo/docs/a.md", "/repo/docs/b.txt"]))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/|/repo/docs/guide.md|/repo/docs/guide.md|/repo/docs/guide.md|('/repo/docs', 'guide.md')|('/repo/docs/guide', '.md')|guide.md|/repo/docs|True|/repo/site/page.md|/repo/docs/guide.md|/repo/docs/guide.md|docs/guide.md|/repo/docs", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsPathModule_ExpandedHelpers_FollowContainedPosixModel()
    {
        var host = new MockLythonHost("/repo/work");
        host.SeedFile("/repo/docs/a.txt", string.Empty);

        var result = new LythonEngine().Run(
            """
import os
from os import path

vals = []
vals.append(path.normcase("/Repo/File.TXT"))
vals.append(path.commonprefix(["/usr/lib", "/usr/local"]))
vals.append(path.expandvars("$ROOT/${NAME}/%NAME%/$MISSING/%MISSING%"))
vals.append(str(path.splitdrive("/repo/docs")))
vals.append(str(path.splitroot("relative/docs")))
vals.append(str(path.splitroot("/repo/docs")))
vals.append(str(path.splitroot("//server/share")))
vals.append(str(path.splitroot("///server/share")))
vals.append(str(path.ismount("/")))
vals.append(str(path.ismount("/repo")))
vals.append(str(path.getatime("/repo/docs/a.txt")))
vals.append(str(path.getctime("/repo/docs/a.txt")))
vals.append(str(path.supports_unicode_filenames))
vals.append(os.fsdecode(os.fsencode("/repo/unicode-é.txt")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Environment = new Dictionary<string, string>
                {
                    ["ROOT"] = "/repo",
                    ["NAME"] = "docs"
                }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/Repo/File.TXT|/usr/l|/repo/docs/docs/$MISSING/%MISSING%|('', '/repo/docs')|('', '', 'relative/docs')|('', '/', 'repo/docs')|('', '//', 'server/share')|('', '/', '//server/share')|True|False|0.0|0.0|True|/repo/unicode-é.txt", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsModule_Environment_IsContainedAndMutableWithinRun()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
import os
from os import environ

vals = []
vals.append(os.name)
vals.append(os.linesep == "\n")
vals.append(os.pathsep)
vals.append(str(os.altsep))
vals.append(os.extsep)
vals.append(os.devnull)
vals.append(str(os.F_OK) + str(os.R_OK) + str(os.W_OK) + str(os.X_OK))
vals.append(os.getenv("PATH", "missing"))
vals.append(str(os.get_exec_path()))
environ["NEW"] = "value"
vals.append(os.getenv("NEW"))
os.putenv("PUT", "ok")
vals.append(environ["PUT"])
vals.append(str("PUT" in environ))
vals.append(str(sorted(environ.keys())))
vals.append(str(sorted(environ.items())))
environ.update({"EXTRA": "yes"})
vals.append(os.environ.get("EXTRA"))
del environ["NEW"]
vals.append(str(os.getenv("NEW") is None))
os.unsetenv("PUT")
vals.append(str(os.getenv("PUT") is None))
vals.append(str(sorted(os.get_exec_path({"PATH": "/custom:/bin"}))))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join([str(v) for v in vals]))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Environment = new Dictionary<string, string>
                {
                    ["PATH"] = "/bin:/tools",
                    ["KEEP"] = "seed"
                }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("posix|True|:|None|.|/dev/null|0421|/bin:/tools|['/bin', '/tools']|value|ok|True|['KEEP', 'NEW', 'PATH', 'PUT']|[('KEEP', 'seed'), ('NEW', 'value'), ('PATH', '/bin:/tools'), ('PUT', 'ok')]|yes|True|True|['/bin', '/custom']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsModule_Environment_DoesNotReadAmbientProcessEnvironmentByDefault()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
import os
__lython_file = open("/out.txt", "w")
__lython_file.write(os.getenv("PATH", "missing") + "|" + str(os.get_exec_path()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("missing|[]", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task OsModule_RunAsync_PreservesEnvironmentWhenMergingCancellation()
    {
        var host = new MockLythonHost("/repo");
        using var cancellation = new CancellationTokenSource();

        var result = await new LythonEngine().RunAsync(
            """
import os
__lython_file = open("/out.txt", "w")
__lython_file.write(os.getenv("TOKEN"))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Environment = new Dictionary<string, string>
                {
                    ["TOKEN"] = "kept"
                }
            },
            cancellation.Token);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("kept", host.ReadText("/out.txt"));
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
from pathlib import Path

vals = []
vals.append(str(sorted(os.listdir("/repo/docs"))))
vals.append(os.getcwd())
vals.append(str(os.path.exists("/repo/docs/a.txt")))
vals.append(str(os.path.isfile("/repo/docs/a.txt")))
vals.append(str(os.path.isdir("/repo/docs")))
vals.append(str(os.stat(Path("/repo/docs/a.txt")).st_size))
vals.append(str(os.lstat("/repo/docs/a.txt").st_mtime))
vals.append(str(os.path.getsize(Path("/repo/docs/a.txt"))))
vals.append(str(os.path.getmtime("/repo/docs/a.txt")))
vals.append(str(os.path.lexists("/repo/docs/a.txt")))
entries = []
for entry in os.scandir("/repo/docs"):
    entries.append(entry.name + ":" + str(entry.is_file()) + ":" + str(entry.is_dir()) + ":" + str(entry.stat().st_size))
vals.append(str(sorted(entries)))
vals.append(str(os.path.samefile("/repo/docs/a.txt", Path("/repo/docs/a.txt"))))
os.makedirs("/repo/out/nested", exist_ok=True)
os.rename("/repo/docs/a.txt", "/repo/docs/c.txt")
os.unlink("/repo/docs/sub/b.txt")
vals.append(str(os.path.isdir("/repo/out/nested")))
vals.append(str(os.path.exists("/repo/docs/c.txt")))
vals.append(str(os.path.exists("/repo/docs/sub/b.txt")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a.txt', 'sub']|/repo|True|True|True|5|0.0|5|0.0|True|['a.txt:True:False:5', 'sub:False:True:0']|True|True|True|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsScandir_ReturnsContextManagedIteratorAndPathLikeEntries()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a.txt", "alpha");
        host.SeedFile("/repo/docs/sub/b.txt", "beta");

        var result = new LythonEngine().Run(
            """
import os

rows = []
with os.scandir("/repo/docs") as entries:
    for entry in entries:
        rows.append(os.fspath(entry) + ":" + entry.__fspath__() + ":" + str(os.path.getsize(entry)))

__lython_file = open("/out.txt", "w")
__lython_file.write("\n".join(sorted(rows)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo/docs/a.txt:/repo/docs/a.txt:5\n/repo/docs/sub:/repo/docs/sub:0", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write(str(sorted(os.listdir())))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a.txt', 'b.txt']", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|False", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("os.access('/repo', os.F_OK)", "os.access() is not supported")]
    [InlineData("os.chdir('/repo')", "os.chdir() is not supported")]
    [InlineData("os.path.expanduser('~/x')", "os.path.expanduser() is not supported")]
    [InlineData("os.path.islink('/repo')", "os.path.islink() is not supported")]
    [InlineData("next(os.scandir('/repo')).is_symlink()", "DirEntry.is_symlink() is not supported")]
    [InlineData("next(os.scandir('/repo')).inode()", "DirEntry.inode() is not supported")]
    public void OsModule_UnsupportedHostSurface_FailsExplicitly(string expression, string messageFragment)
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.txt", "a");

        var result = new LythonEngine().Run(
            $"""
import os
{expression}
""",
            host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("NotImplementedError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
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
__lython_file = open("/out.txt", "w")
__lython_file.write("\n".join(rows))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "/repo/docs|['a', 'b']|['root.txt']\n/repo/docs/a|[]|['alpha.txt']\n/repo/docs/b|[]|['beta.txt']",
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

__lython_file = open("/out.txt", "w")
__lython_file.write("\n".join(top_rows) + "\n---\n" + "\n".join(bottom_rows))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "/repo/docs|['b']|['root.txt']\n/repo/docs/b|[]|['beta.txt']\n---\n/repo/docs/a|[]|['alpha.txt']\n/repo/docs/b|[]|['beta.txt']\n/repo/docs|['a', 'b']|['root.txt']",
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

__lython_file = open("/out.txt", "w")
__lython_file.write("\n".join(rows) + "\n---\n" + "\n".join(events))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "/repo/docs|['a', 'b']|[]\n/repo/docs/b|[]|['beta.txt']\n---\nRuntimeError:Host listdir failed: blocked a",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task OsWalk_RunAsync_AwaitsLazyTraversalAndPreservesTopdownPruning()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/root.txt", "r");
        host.SeedFile("/repo/docs/a/alpha.txt", "a");
        host.SeedFile("/repo/docs/b/beta.txt", "b");

        var result = await new LythonEngine().RunAsync(
            """
import os

rows = []
for root, dirs, files in os.walk("/repo/docs", topdown=True):
    if root == "/repo/docs":
        del dirs[0]
    rows.append(root + "|" + str(dirs) + "|" + str(files))

__lython_file = open("/out.txt", "w")
__lython_file.write("\n".join(rows))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(
            "/repo/docs|['b']|['root.txt']\n/repo/docs/b|[]|['beta.txt']",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsWalk_RunWithAsynchronousHost_FailsFastWithRunAsyncGuidance()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/root.txt", "r");

        var result = new LythonEngine().Run(
            """
import os

for root, dirs, files in os.walk("/repo/docs"):
    pass
""",
            host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("use RunAsync", result.Failure.Message, StringComparison.Ordinal);
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
vals.append(open("/repo/docs/b.txt").read())
vals.append(str(os.path.exists("/repo/docs/a.txt")))
vals.append(str(os.path.exists("/repo/tmp/sub")))
vals.append(str(os.path.exists("/repo/tmp")))
vals.append(str(os.path.exists("/repo/empty")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha|False|False|True|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task OsModule_RunAsync_AwaitsAsynchronousHostOperations()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/a.txt", "alpha");
        host.SeedFile("/repo/docs/b.txt", "beta");
        host.SeedFile("/repo/tmp/keep.txt", "keep");
        host.SeedFile("/repo/tmp/sub/leaf.txt", "leaf");

        var result = await new LythonEngine().RunAsync(
            """
import os

vals = []
vals.append(str(sorted(os.listdir("/repo/docs"))))
vals.append(str(os.path.exists("/repo/docs/a.txt")))
vals.append(str(os.path.isfile("/repo/docs/a.txt")))
vals.append(str(os.path.isdir("/repo/docs")))
vals.append(str(os.stat("/repo/docs/a.txt").st_size))
vals.append(str(os.path.getsize("/repo/docs/b.txt")))
vals.append(str(sorted([entry.name for entry in os.scandir("/repo/docs")])))
os.makedirs("/repo/out/nested", exist_ok=True)
os.rename("/repo/docs/a.txt", "/repo/docs/c.txt")
os.replace("/repo/docs/c.txt", "/repo/docs/b.txt")
os.remove("/repo/tmp/sub/leaf.txt")
os.removedirs("/repo/tmp/sub")
os.mkdir("/repo/empty")
os.rmdir("/repo/empty")
vals.append(open("/repo/docs/b.txt").read())
vals.append(str(os.path.isdir("/repo/out/nested")))
vals.append(str(os.path.exists("/repo/docs/a.txt")))
vals.append(str(os.path.exists("/repo/tmp/sub")))
vals.append(str(os.path.exists("/repo/tmp")))
vals.append(str(os.path.exists("/repo/empty")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("['a.txt', 'b.txt']|True|True|True|5|4|['a.txt', 'b.txt']|alpha|True|False|False|True|False", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import os
os.path.commonpath([])
""",
        "compile",
        "non-empty iterable of path-like values")]
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
        "iterable of path-like values, not a single path")]
    [InlineData(
        """
import os
os.makedirs("/repo", exist_ok=False)
""",
        "RuntimeError",
        "target already exists")]
    [InlineData(
        """
import os
os.path.samefile("/repo/missing.txt", "/repo/also-missing.txt")
""",
        "RuntimeError",
        "both paths")]
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
            Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
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
