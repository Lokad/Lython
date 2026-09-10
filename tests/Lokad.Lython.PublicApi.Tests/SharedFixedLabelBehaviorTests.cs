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

    [Fact]
    public async Task BuiltinCallableNames()
    {
        var script = new LythonEngine().Compile("""
            import math
            import os
            import random
            m1 = len.__module__
            m2 = len.__module__
            return [len.__name__, m1, m1 is m2, len.__name__ is len.__name__,
                math.sqrt.__name__, math.sqrt.__module__,
                math.sqrt.__module__ is math.sqrt.__module__,
                os.listdir.__name__, os.listdir.__module__,
                os.listdir.__module__ is os.listdir.__module__,
                int.__name__, random.random.__name__, random.random.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "len", "builtins", true, false,
            "sqrt", "math", true,
            "listdir", "os", true,
            "int", "random", "random",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SingletonCallableNames()
    {
        // open.__module__ is builtins in Lython (CPython says io, which has no
        // Lython counterpart); everything else matches CPython exactly.
        var script = new LythonEngine().Compile("""
            p1 = print.__name__
            p2 = print.__module__
            return [p1, p2, p2 is print.__module__,
                min.__name__, min.__module__, max.__name__, max.__module__,
                zip.__name__, zip.__module__, dict.__name__, dict.__module__,
                open.__name__, open.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "print", "builtins", true,
            "min", "builtins", "max", "builtins",
            "zip", "builtins", "dict", "builtins",
            "open", "builtins",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundMethodNames()
    {
        // Bound engine methods report __module__ None like CPython C methods.
        // random.Random.gauss diverges (CPython reports random since its gauss
        // is Python-implemented; Lython models it as engine code like the rest).
        var script = new LythonEngine().Compile("""
            import datetime
            import random
            import re
            a = [].append
            m = re.compile("a").match
            d = datetime.date(2024, 1, 1).weekday
            g = random.Random(1).gauss
            return [a.__name__, a.__module__, m.__name__, m.__module__,
                d.__name__, d.__module__, g.__name__, g.__module__,
                a.__module__ is a.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "append", null, "match", null,
            "weekday", null, "gauss", null, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task UserFunctionAndBoundMethodModule()
    {
        var script = new LythonEngine().Compile("""
            def top():
                pass
            def outer():
                def inner():
                    pass
                return inner
            class C:
                def m(self):
                    pass
            t = top.__module__
            f = outer()
            a = f.__module__
            b = f.__module__
            c = C()
            d = c.m
            return [top.__name__, t, f.__name__, a, a is b,
                d.__name__, d.__module__, c.m.__module__ is d.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "top", "__main__", "inner", "__main__", true,
            "m", "__main__", true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundMethodSelfAndFunc()
    {
        var script = new LythonEngine().Compile("""
            class C:
                @staticmethod
                def s():
                    pass
                @classmethod
                def k(cls):
                    pass
                def m(self):
                    pass
            c = C()
            m = c.m
            return [m.__self__ is c, m.__func__.__name__, m.__func__ is m.__func__,
                C.s.__name__, C.s.__module__, C.k.__name__, C.k.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, "m", true, "s", "__main__", "k", "__main__",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
