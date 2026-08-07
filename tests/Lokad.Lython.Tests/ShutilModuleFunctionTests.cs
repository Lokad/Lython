using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ShutilModuleFunctionTests
{
    [Fact]
    public void Shutil_CopyHelpers_AreHostMediatedAndPythonShaped()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/src/a.txt", "alpha");
        host.SeedFile("/repo/src/b.txt", "beta");
        host.SeedFile("/repo/src/existing.txt", "old");
        host.MkDir("/repo/out");

        var result = new LythonEngine().Run(
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

        var result = new LythonEngine().Run(
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

        var result = new LythonEngine().Run(
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

        var result = new LythonEngine().Run(
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
    [InlineData("import shutil\nshutil.copyfile('/repo/missing.txt', '/repo/out.txt')\n", "RuntimeError")]
    [InlineData("import shutil\nshutil.copyfile('/repo/a.txt', '/repo/out')\n", "RuntimeError")]
    [InlineData("import shutil\nshutil.copyfile('/repo/a.txt', '/repo/b.txt', follow_symlinks=False)\n", "NotImplementedError")]
    [InlineData("import shutil\nshutil.copy2('/repo/a.txt', '/repo/b.txt')\n", "NotImplementedError")]
    [InlineData("import shutil\nshutil.move('/repo/a.txt', '/repo/b.txt')\n", "RuntimeError")]
    [InlineData("import shutil\nwith open('/repo/a.txt') as src:\n    with open('/repo/b.txt', 'w') as dst:\n        shutil.copyfileobj(src, dst, 1)\n", "NotImplementedError")]
    public void Shutil_UnsupportedAndFailureCases_AreExplicit(string source, string exceptionType)
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.txt", "alpha");
        host.SeedFile("/repo/b.txt", "beta");
        host.MkDir("/repo/out");

        var result = new LythonEngine().Run(source, host);

        Assert.False(result.Success);
        Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
    }

    [Fact]
    public void Shutil_StaticContracts_CoverCallShapeAndPathTypes()
    {
        var compiled = new LythonEngine().Compile(
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
