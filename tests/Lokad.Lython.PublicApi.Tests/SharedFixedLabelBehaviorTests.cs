using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: fixed class-level labels alias stably across accesses instead of
/// rebuilding fresh strings per read (random getstate tag, builtin exception
/// __module__), while the getstate tuple itself stays fresh per call.
/// </summary>
public sealed class SharedFixedLabelBehaviorTests
{
    [Fact]
    public async Task RandomStateTagIsShared()
    {
        var script = new LythonEngine().Compile("""
            import random
            s1 = random.getstate()
            s2 = random.getstate()
            random.setstate(s1)
            v = random.random()
            random.setstate(s2)
            w = random.random()
            return [s1[0] is s2[0], s1 is s2, s1[0], v == w]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, false, "lython.random.state", true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionModuleIsShared()
    {
        var script = new LythonEngine().Compile("""
            a = ValueError.__module__
            b = ValueError.__module__
            return [a is b, a]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, "builtins" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ModuleExceptionModuleIsShared()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            import copy
            import csv
            import decimal
            import gzip
            import json
            import shutil
            import statistics
            import zipfile
            z1 = zipfile.BadZipFile.__module__
            z2 = zipfile.BadZipFile.__module__
            a1 = argparse.ArgumentError.__module__
            a2 = argparse.ArgumentError.__module__
            d1 = decimal.InvalidOperation.__module__
            d2 = decimal.InvalidOperation.__module__
            return [z1 is z2, z1, a1 is a2, a1, d1 is d2, d1,
                json.JSONDecodeError.__module__ is json.JSONDecodeError.__module__,
                json.JSONDecodeError.__module__,
                gzip.BadGzipFile.__module__ is gzip.BadGzipFile.__module__,
                gzip.BadGzipFile.__module__,
                csv.Error.__module__ is csv.Error.__module__,
                csv.Error.__module__,
                copy.Error.__module__ is copy.Error.__module__,
                copy.Error.__module__,
                shutil.Error.__module__ is shutil.Error.__module__,
                shutil.Error.__module__,
                statistics.StatisticsError.__module__ is statistics.StatisticsError.__module__,
                statistics.StatisticsError.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, "zipfile", true, "argparse", true, "decimal",
            true, "json", true, "gzip", true, "csv", true, "copy",
            true, "shutil", true, "statistics",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BuiltinTypeModuleIsShared()
    {
        var script = new LythonEngine().Compile("""
            i1 = int.__module__
            i2 = int.__module__
            s1 = str.__module__
            s2 = str.__module__
            return [i1 is i2, i1, s1 is s2, s1, list.__module__ is list.__module__, list.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, "builtins", true, "builtins", true, "builtins" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
