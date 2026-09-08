using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R19 ZIP bullet: <c>zipfile</c> error identities are registered explicitly
/// with direct <c>Exception</c> ancestry. <c>BadZipfile</c> is the same object
/// as <c>BadZipFile</c>, matching CPython's legacy alias, and neither name
/// collides with user-defined or other-module exceptions.
/// </summary>
public sealed class ZipExceptionIdentityTests
{
    [Fact]
    public void BadZipfileIsBadZipFile()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
return [zipfile.BadZipfile is zipfile.BadZipFile, zipfile.BadZipfile.__name__, zipfile.BadZipfile.__module__]
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, "BadZipFile", "zipfile" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void ModuleExposesReadSurfaceOnly()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
names = [zipfile.BadZipFile.__name__, zipfile.LargeZipFile.__name__]
names.append(zipfile.ZipInfo("n").filename)
names.append(zipfile.ZipFile is not None)
try:
    zipfile.ZipExtFile
except AttributeError:
    names.append("no-ZipExtFile")
try:
    zipfile.Path
except AttributeError:
    names.append("no-Path")
try:
    zipfile.PyZipFile
except AttributeError:
    names.append("no-PyZipFile")
return names
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "BadZipFile", "LargeZipFile", "n", true, "no-ZipExtFile", "no-Path", "no-PyZipFile" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void UncaughtErrorsReportQualifiedIdentities()
    {
        foreach (var (expression, typeName) in new[]
        {
            ("zipfile.BadZipFile", "BadZipFile"),
            ("zipfile.BadZipfile", "BadZipFile"),
            ("zipfile.LargeZipFile", "LargeZipFile"),
        })
        {
            var result = new LythonEngine().Run(
                $"import zipfile\nraise {expression}(\"torn\")\n",
                new MockLythonHost());
            Assert.False(result.Success);
            Assert.NotNull(result.Failure);
            Assert.Equal(typeName, result.Failure.RequireNotNull().ExceptionType);
            Assert.Contains("torn", result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AliasCatchesBothWays()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
outcomes = []
try:
    raise zipfile.BadZipFile("first")
except zipfile.BadZipfile:
    outcomes.append("alias-caught")
try:
    raise zipfile.BadZipfile("second")
except zipfile.BadZipFile:
    outcomes.append("canonical-caught")
try:
    raise zipfile.BadZipFile("third")
except Exception:
    outcomes.append("exception-caught")
return outcomes
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "alias-caught", "canonical-caught", "exception-caught" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void LargeZipFileSkipsNarrowerHandlers()
    {
        // One except clause per try (subset boundary); nesting composes them
        // into ordered dispatch: a narrow handler that matched would swallow
        // the error instead of letting it reach the outer handler.
        var result = new LythonEngine().Run(
            """
import zipfile
outcome = []
try:
    try:
        raise zipfile.LargeZipFile("huge")
    except OSError:
        outcome.append("oserror-wrong")
except Exception:
    outcome.append("survived-oserror")
try:
    try:
        raise zipfile.LargeZipFile("huge")
    except ValueError:
        outcome.append("value-wrong")
except Exception:
    outcome.append("survived-value")
try:
    raise zipfile.LargeZipFile("huge")
except Exception:
    outcome.append("exception-right")
return outcome
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "survived-oserror", "survived-value", "exception-right" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void SimilarlyNamedExceptionsDoNotCollide()
    {
        // User-defined exception classes are outside the supported subset, so
        // collisions are pinned between module exception identities instead.
        var result = new LythonEngine().Run(
            """
import gzip
import zipfile
outcomes = []
try:
    try:
        raise zipfile.BadZipFile("zip")
    except gzip.BadGzipFile:
        outcomes.append("gzip-caught-zip-wrong")
except zipfile.BadZipFile:
    outcomes.append("zip-survived-gzip")
try:
    try:
        raise gzip.BadGzipFile("gzip")
    except zipfile.BadZipFile:
        outcomes.append("zip-caught-gzip-wrong")
except gzip.BadGzipFile:
    outcomes.append("gzip-survived-zip")
try:
    try:
        raise zipfile.LargeZipFile("huge")
    except gzip.BadGzipFile:
        outcomes.append("gzip-caught-large-wrong")
except Exception:
    outcomes.append("large-survived-gzip")
return outcomes
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "zip-survived-gzip", "gzip-survived-zip", "large-survived-gzip" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void ShortNameBoundaryNormalizesZipErrors()
    {
        Assert.Equal(
            PythonExceptionIdentity.Module("zipfile", "BadZipFile"),
            PythonExceptionIdentity.FromRuntimeTypeName("BadZipFile"));
        Assert.Equal(
            PythonExceptionIdentity.Module("zipfile", "BadZipFile"),
            PythonExceptionIdentity.FromRuntimeTypeName("BadZipfile"));
        Assert.Equal(
            PythonExceptionIdentity.Module("zipfile", "LargeZipFile"),
            PythonExceptionIdentity.FromRuntimeTypeName("LargeZipFile"));
    }

    [Fact]
    public void ImportFormsResolveErrorNames()
    {
        var result = new LythonEngine().Run(
            """
import zipfile as zf
from zipfile import BadZipfile, LargeZipFile
return [zf.BadZipFile is BadZipfile, LargeZipFile.__module__]
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, "zipfile" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }
}
