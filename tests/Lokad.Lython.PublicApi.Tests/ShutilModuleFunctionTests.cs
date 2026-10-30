using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ShutilModuleFunctionTests
{
    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task CopyAndMoveReturnOriginalDestinationObjects(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import shutil
            from pathlib import Path
            class Name:
                def __init__(self,path):self.path=path
                def __fspath__(self):return self.path
            for operation in (shutil.copyfile,shutil.copy,shutil.move):
                for make in (lambda path:path,Path,Name):
                    source=Path('/source')
                    source.write_text('payload')
                    destination=make('/destination')
                    Path('/destination').unlink(missing_ok=True)
                    print(operation(source,destination) is destination)
            for operation in (shutil.copy,shutil.move):
                source=Path('/source')
                source.write_text('payload')
                Path('/directory/source').unlink(missing_ok=True)
                destination=Name('/directory')
                result=operation(source,destination)
                print(type(result) is str,result)
            source.write_text('payload')
            destination=Name('/destination')
            print(shutil.copyfile(source,destination).path)
            print(shutil.copy(source,destination).path)
            Path('/destination').unlink()
            print(shutil.move(source,destination).path)
            """);
        var host = new MockLythonHost(); host.MkDir("/directory");
        var budget = new LythonMemoryBudget(1048576);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(string.Concat(Enumerable.Repeat("True\n", 9)) + string.Concat(Enumerable.Repeat("True /directory/source\n", 2))
                + string.Concat(Enumerable.Repeat("/destination\n", 3)), result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task ReturnedDestinationAliasesStayFundedUntilResultDisposal(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1048576);
        using (var script = new LythonEngine().Compile("import shutil\ndestination='/'+'x'*4096\nresult=shutil.copyfile('/source',destination)\nassert result is destination\nreturn result", budget))
        {
            var baseline = budget.ReservedBytes;
            for (var invocation = 0; invocation < 2; invocation++)
            {
                var host = new MockLythonHost(); host.SeedFile("/source", "payload");
                using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("/" + new string('x', 4096), result.ReturnValue);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void Shutil_CopyHelpers_AreHostMediatedAndPythonShaped()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/src/a.txt", "alpha");
        host.SeedFile("/repo/src/b.txt", "beta");
        host.SeedFile("/repo/src/existing.txt", "old");
        host.MkDir("/repo/out");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import os
import shutil
from pathlib import Path

vals = []
vals.append(shutil.copyfile("/repo/src/a.txt", "/repo/out/a-copy.txt"))
vals.append(shutil.copy(Path("/repo/src/b.txt"), "/repo/out"))
vals.append(shutil.copyfile("/repo/src/a.txt", "/repo/src/existing.txt"))
vals.append(str(os.path.exists("/repo/out/b.txt")))
vals.append(Path("/repo/out/a-copy.txt").read_text())
vals.append(Path("/repo/out/b.txt").read_text())
vals.append(Path("/repo/src/existing.txt").read_text())
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo/out/a-copy.txt|/repo/out/b.txt|/repo/src/existing.txt|True|alpha|beta|alpha", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Shutil_Move_UsesDestinationDirectoryShape()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/src/a.txt", "alpha");
        host.MkDir("/repo/out");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import os
import shutil
from pathlib import Path

target = shutil.move("/repo/src/a.txt", "/repo/out")
__lython_file = open("/out.txt", "w")
__lython_file.write(target + "|" + str(os.path.exists("/repo/src/a.txt")) + "|" + Path("/repo/out/a.txt").read_text())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo/out/a.txt|False|alpha", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Shutil_CopyFileObj_CopiesTextFileLikeObjects()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/src/a.txt", "alpha\nbeta");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import shutil
from pathlib import Path

with open("/repo/src/a.txt") as src:
    with open("/repo/out.txt", "w") as dst:
        shutil.copyfileobj(src, dst)

__lython_file = open("/check.txt", "w")
__lython_file.write(Path("/repo/out.txt").read_text())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha\nbeta", host.ReadText("/check.txt"));
    }

    [Fact]
    public void Shutil_SameFileError_IsCatchable()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.txt", "alpha");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import shutil

try:
    shutil.copyfile("/repo/a.txt", "/repo/./a.txt")
except shutil.SameFileError as ex:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(ex.type)
    __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("SameFileError", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("import shutil\nshutil.copyfile('/repo/missing.txt', '/repo/out.txt')\n", "FileNotFoundError")]
    [InlineData("import shutil\nshutil.copyfile('/repo/a.txt', '/repo/out')\n", "IsADirectoryError")]
    [InlineData("import shutil\nshutil.copyfile('/repo/a.txt', '/repo/b.txt', follow_symlinks=False)\n", "NotImplementedError")]
    [InlineData("import shutil\nshutil.copy2('/repo/a.txt', '/repo/b.txt')\n", "NotImplementedError")]
    [InlineData("import shutil\nshutil.move('/repo/a.txt', '/repo/b.txt')\n", "RuntimeError")]
    public void Shutil_UnsupportedAndFailureCases_AreExplicit(string source, string exceptionType)
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.txt", "alpha");
        host.SeedFile("/repo/b.txt", "beta");
        host.MkDir("/repo/out");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(source, host);

        Assert.False(result.Success);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
    }

    [Fact]
    public void Shutil_StaticContracts_CoverCallShapeAndPathTypes()
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
import shutil

shutil.copyfile(1, "/repo/out.txt")
shutil.copy("/repo/in.txt", 2)
shutil.copy2("/repo/in.txt")
shutil.move("/repo/in.txt", "/repo/out.txt", copy_function=shutil.copy)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("shutil.copyfile", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("shutil.copy", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3151" && d.Message.Contains("shutil.copy2", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("copy_function", StringComparison.Ordinal));
    }
}

