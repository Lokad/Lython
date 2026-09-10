using System.Numerics;
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

    [Fact]
    public async Task BoundMethodQualnames()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            class C:
                @staticmethod
                def s():
                    pass
                def m(self):
                    pass
            class D(C):
                pass
            return [C().m.__qualname__, D().m.__qualname__, C.s.__qualname__,
                C.m.__qualname__, [].append.__qualname__, "x".join.__qualname__,
                datetime.timedelta(1).total_seconds.__qualname__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "C.m", "C.m", "C.s", "C.m",
            "list.append", "str.join", "timedelta.total_seconds",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SlotMethodNames()
    {
        var script = new LythonEngine().Compile("""
            import functools
            from dataclasses import dataclass
            @dataclass
            class P:
                x: int = 0
            @functools.total_ordering
            class C:
                def __eq__(self, o):
                    return True
                def __lt__(self, o):
                    return False
            class E:
                pass
            return [E().__init__.__name__, P.__init__.__name__, P.__eq__.__name__,
                P.__repr__.__name__, C().__gt__.__name__, object.__new__.__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "__init__", "__init__", "__eq__", "__repr__", "__gt__", "__new__",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task GeneratedMethodModule()
    {
        // Dataclass methods report the defining module; total_ordering methods
        // report functools, where CPython synthesizes them.
        var script = new LythonEngine().Compile("""
            import functools
            from dataclasses import dataclass
            @dataclass
            class P:
                x: int = 0
            @functools.total_ordering
            class C:
                def __eq__(self, o):
                    return True
                def __lt__(self, o):
                    return False
            return [P.__init__.__module__, P.__eq__.__module__, P.__repr__.__module__,
                C.__gt__.__module__, C().__gt__.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "__main__", "__main__", "__main__", "functools", "functools",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NestedQualnames()
    {
        var script = new LythonEngine().Compile("""
            def o():
                def i():
                    pass
                return i
            class C:
                class D:
                    def m(self):
                        pass
            def outer():
                return lambda: 1
            l = lambda: 0
            f = o()
            return [f.__qualname__, C.D().m.__qualname__, l.__qualname__,
                outer().__qualname__, l.__name__, l.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "o.<locals>.i", "D.m", "<lambda>", "outer.<locals>.<lambda>",
            "<lambda>", "__main__",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task LambdaNamesAreShared()
    {
        var script = new LythonEngine().Compile("""
            l = lambda: 0
            m = lambda: 1
            return [l.__name__ is l.__name__, l.__name__,
                l.__qualname__ is l.__qualname__, l.__name__ is m.__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, "<lambda>", true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BuiltinTypeBases()
    {
        var script = new LythonEngine().Compile("""
            b1 = int.__bases__
            b2 = int.__bases__
            m1 = int.__mro__
            return [b1 is b2, len(b1), b1[0] is object, bool.__bases__[0] is int,
                m1 is int.__mro__, len(m1), m1[0] is int, m1[1] is object,
                list.__bases__[0] is object, dict.__bases__[0] is object,
                zip.__bases__[0] is object, range.__bases__[0] is object]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, new BigInteger(1), true, true, true, new BigInteger(2),
            true, true, true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RuntimeTypeBases()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            import decimal
            import pathlib
            import random
            import statistics
            return [random.Random.__module__,
                random.Random.__name__ is random.Random.__name__,
                datetime.date.__bases__[0] is object,
                datetime.datetime.__bases__[0] is datetime.date,
                statistics.NormalDist.__module__,
                decimal.Decimal.__bases__[0] is object,
                pathlib.Path.__name__ is pathlib.Path.__name__,
                pathlib.Path.__module__,
                pathlib.PurePath.__bases__[0] is object,
                pathlib.PosixPath.__bases__[0] is pathlib.Path,
                range.__module__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "random", true, true, true, "statistics", true,
            true, "pathlib", true, true, "builtins",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RemainingTypeDunders()
    {
        var script = new LythonEngine().Compile("""
            import decimal
            import random
            import re
            import time
            import zipfile
            return [time.struct_time.__name__ is time.struct_time.__name__,
                time.struct_time.__qualname__, time.struct_time.__module__,
                time.struct_time.__bases__[0] is tuple,
                len(time.struct_time.__mro__),
                decimal.DecimalTuple.__bases__[0] is tuple,
                decimal.DecimalTuple.__module__,
                zipfile.ZipInfo.__name__, zipfile.ZipInfo.__module__,
                zipfile.ZipInfo.__bases__[0] is object,
                zipfile.ZipFile.__bases__[0] is object,
                re.RegexFlag.__name__, re.RegexFlag.__module__,
                random.Random.__mro__[0] is random.Random, random.Random.__mro__[1] is object]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, "struct_time", "time", true, new BigInteger(3),
            true, "decimal", "ZipInfo", "zipfile", true, true,
            "RegexFlag", "re", true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AbsentDocstringsAreNone()
    {
        // Partial objects expose no __name__ like CPython; their __module__
        // aliases shared functools. Doc texts are not stored anywhere.
        var script = new LythonEngine().Compile("""
            import functools
            def f():
                pass
            class C:
                def m(self):
                    pass
            p = functools.partial(int)
            return [f.__doc__, C().m.__doc__, C.__doc__,
                hasattr(p, "__name__"), hasattr(p, "__qualname__"),
                p.__module__, p.__doc__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            null, null, null, false, false, "functools", null,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FunctionDocstringsAreStored()
    {
        var script = new LythonEngine().Compile("""
            def f():
                "doc"
                pass
            def g():
                pass
            def h():
                f"{1}"
                pass
            class C:
                def m(self):
                    "mm"
                    pass
            return [f.__doc__, g.__doc__, h.__doc__, C().m.__doc__, C.__doc__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "doc", null, null, "mm", null,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ClassDocstringsAreStored()
    {
        var script = new LythonEngine().Compile("""
            class C:
                "cdoc"
                pass
            class D(C):
                pass
            class E:
                pass
            class F:
                "ignored"
                __doc__ = "custom"
            E.__doc__ = "post"
            return [C.__doc__, D.__doc__, E.__doc__, F.__doc__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "cdoc", null, "post", "custom",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionInstanceClass()
    {
        // Module exceptions stay missing until their type objects are interned.
        var script = new LythonEngine().Compile("""
            e = ValueError("x")
            k = KeyError("k")
            return [e.__class__ is ValueError, k.__class__ is KeyError,
                e.__class__ is k.__class__, e.__class__.__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, false, "ValueError",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ModuleExceptionInstanceClass()
    {
        var script = new LythonEngine().Compile("""
            import csv
            import zipfile
            e = csv.Error("y")
            z = zipfile.BadZipFile("z")
            return [e.__class__ is csv.Error,
                z.__class__ is zipfile.BadZipFile,
                z.__class__ is zipfile.BadZipfile]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ValueClassIdentities()
    {
        var script = new LythonEngine().Compile("""
            class C:
                pass
            import datetime
            import decimal
            import pathlib
            import random
            import statistics
            import time
            return [[].__class__ is list, {}.__class__ is dict,
                decimal.Decimal("1.5").__class__ is decimal.Decimal,
                datetime.date(2024, 1, 1).__class__ is datetime.date,
                datetime.timedelta(1).__class__ is datetime.timedelta,
                pathlib.Path("/x").__class__ is pathlib.Path,
                time.struct_time((2024, 1, 1, 0, 0, 0, 0, 1, -1)).__class__ is time.struct_time,
                random.Random(1).__class__ is random.Random,
                statistics.NormalDist(0, 1).__class__ is statistics.NormalDist,
                "s".__class__ is str,
                (1).__class__ is int, (1.5).__class__ is float,
                True.__class__ is bool, None.__class__ is type(None),
                C.__class__ is type, type(C) is type]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BaseExceptionsBypassException()
    {
        // GeneratorExit/KeyboardInterrupt derive BaseException directly: except
        // Exception must not catch them, except BaseException must.
        var script = new LythonEngine().Compile("""
            results = []
            try:
                raise KeyboardInterrupt("stop")
            except KeyboardInterrupt:
                results.append("ki")
            try:
                raise GeneratorExit()
            except GeneratorExit:
                results.append("ge")
            try:
                try:
                    raise KeyboardInterrupt("x")
                except Exception:
                    results.append("wrong")
            except BaseException:
                results.append("base")
            k = KeyboardInterrupt("y")
            results.append(k.__class__ is KeyboardInterrupt)
            results.append(KeyboardInterrupt.__module__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "ki", "ge", "base", true, "builtins",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CallableClassIdentities()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            from dataclasses import dataclass
            def f():
                pass
            class C:
                def m(self):
                    pass
            @dataclass
            class P:
                x: int = 0
            d = datetime.date(2024, 1, 1)
            return [f.__class__ is type(f),
                len.__class__ is type(len),
                [].append.__class__ is type([].append),
                C().m.__class__ is type(C().m),
                print.__class__ is type(print),
                d.weekday.__class__.__name__,
                P.__init__.__class__ is type(P.__init__),
                dict.__class__ is type, int.__class__ is type,
                type(f).__name__, type(C().m).__name__, type(len).__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true,
            "builtin_function_or_method", true, true, true,
            "function", "method", "builtin_function_or_method",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DescriptorClassIdentities()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            def f():
                pass
            sm = staticmethod(f)
            d = datetime.date(2024, 1, 1)
            return [type(d.weekday).__name__,
                sm.__class__ is staticmethod,
                staticmethod.__class__ is type,
                staticmethod.__bases__[0] is object,
                property.__class__ is type]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "builtin_function_or_method", true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
