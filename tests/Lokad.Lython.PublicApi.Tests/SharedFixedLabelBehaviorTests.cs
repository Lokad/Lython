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
    public async Task SlotMethodQualnamesAndModules()
    {
        // Object slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; the __new__ builtin and the bound
        // __init_subclass__ report a None __module__ while other slot
        // wrappers leave __module__ missing.
        var script = new LythonEngine().Compile("""
            class E:
                pass
            return [object.__init__.__name__, object.__setattr__.__name__,
                object.__getattribute__.__name__, object.__delattr__.__name__,
                object.__init_subclass__.__name__,
                object.__init__.__qualname__, object.__new__.__qualname__,
                object.__init_subclass__.__qualname__,
                E().__init__.__qualname__, E().__setattr__.__qualname__,
                object.__new__.__module__ is None,
                object.__init_subclass__.__module__ is None,
                E().__init_subclass__.__module__ is None,
                hasattr(object.__init__, "__module__"),
                hasattr(object.__setattr__, "__module__"),
                hasattr(E().__init__, "__module__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "__init__", "__setattr__", "__getattribute__", "__delattr__",
            "__init_subclass__",
            "object.__init__", "object.__new__", "object.__init_subclass__",
            "object.__init__", "object.__setattr__",
            true, true, true, false, false, false,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SlotMethodClassIdentities()
    {
        // Object slot wrappers report the wrapper_descriptor runtime type,
        // their bound forms the method-wrapper type, and the __new__ builtin
        // plus the bound __init_subclass__ the builtin_function_or_method
        // type, like CPython (neither wrapper type is a builtin name).
        var script = new LythonEngine().Compile("""
            class E:
                pass
            return [type(object.__init__).__name__,
                type(E().__init__).__name__,
                type(object.__new__).__name__,
                type(object.__init_subclass__).__name__,
                type(E().__init_subclass__).__name__,
                object.__init__.__class__ is type(object.__init__),
                E().__init__.__class__ is type(E().__init__),
                object.__new__.__class__ is type(object.__new__),
                type(object.__init__).__module__,
                type(E().__init__).__module__,
                type(object.__init__).__bases__[0] is object,
                type(E().__init__).__bases__[0] is object]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "wrapper_descriptor", "method-wrapper",
            "builtin_function_or_method", "builtin_function_or_method",
            "builtin_function_or_method",
            true, true, true, "builtins", "builtins", true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SlotOwnerIdentities()
    {
        // The owning object type threads into slot wrappers at class
        // construction: __new__ reports it as __self__ and slot wrappers
        // (bound or not) as __objclass__, like CPython. The other slot
        // surfaces stay missing.
        var script = new LythonEngine().Compile("""
            class E:
                pass
            e = E()
            return [object.__new__.__self__ is object,
                e.__init__.__self__ is e,
                object.__init_subclass__.__self__ is object,
                E.__init_subclass__.__self__ is E,
                object.__init__.__objclass__ is object,
                e.__init__.__objclass__ is object,
                e.__setattr__.__objclass__ is object,
                hasattr(object.__init__, "__self__"),
                hasattr(object.__new__, "__objclass__"),
                hasattr(object.__init_subclass__, "__objclass__"),
                hasattr(e.__init_subclass__, "__objclass__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true,
            false, false, false, false,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task InstanceSlotFallback()
    {
        // Object instance slots resolve through the run object type on any
        // receiver like CPython, so member reads, identity and no-arg calls
        // behave uniformly on builtin values, engine callables and instances.
        var script = new LythonEngine().Compile("""
            class E:
                pass
            e = E()
            s = "x"
            return [s.__init__.__qualname__, ().__init__.__qualname__,
                (1).__init__.__qualname__, [].__init__.__qualname__,
                {}.__init__.__qualname__, len.__init__.__qualname__,
                s.__init__.__class__.__name__,
                ().__init__() is None, s.__init__() is None,
                e.__init__() is None,
                s.__setattr__.__qualname__, s.__getattribute__.__qualname__,
                s.__delattr__.__qualname__,
                hasattr(s, "__init__"), hasattr((), "__init__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "object.__init__", "object.__init__", "object.__init__",
            "object.__init__", "object.__init__", "object.__init__",
            "method-wrapper", true, true, true,
            "object.__setattr__", "object.__getattribute__",
            "object.__delattr__", true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SubclassSlotFallback()
    {
        // __init_subclass__ resolves through the run object type on any
        // receiver and binds type(target) like CPython, so builtin values
        // and engine callables behave like user instances; no-arg calls
        // succeed while explicit arguments still fail.
        var script = new LythonEngine().Compile("""
            class E:
                pass
            e = E()
            return [().__init_subclass__.__self__ is tuple,
                "x".__init_subclass__.__self__ is str,
                len.__init_subclass__.__self__ is type(len),
                e.__init_subclass__.__self__ is E,
                ().__init_subclass__() is None,
                len.__init_subclass__() is None,
                object.__init_subclass__() is None,
                E.__init_subclass__() is None,
                ().__init_subclass__.__qualname__,
                ().__init_subclass__.__class__.__name__,
                hasattr((), "__init_subclass__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true,
            "object.__init_subclass__", "builtin_function_or_method", true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NewSlotIdentities()
    {
        // Type constructors expose their own stable __new__ slot like
        // CPython, and builtin values resolve their type own slot while
        // engine callables share object.__new__. Construction through the
        // slot stays unsupported; other shapes stay missing.
        var script = new LythonEngine().Compile("""
            return [int.__new__.__qualname__, str.__new__.__qualname__,
                int.__new__ is int.__new__,
                (1).__new__ is int.__new__,
                "x".__new__ is str.__new__,
                [].__new__ is list.__new__,
                (1).__new__.__self__ is int,
                int.__new__.__module__ is None,
                len.__new__ is object.__new__,
                type(int.__new__).__name__,
                int.__new__.__class__ is type(int.__new__),
                object.__new__.__qualname__,
                hasattr((), "__new__"), hasattr(1, "__new__"),
                hasattr(len, "__new__"), hasattr(str, "__new__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "int.__new__", "str.__new__", true, true, true, true, true,
            true, true, "builtin_function_or_method", true,
            "object.__new__", true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NewSlotOwnerIdentities()
    {
        // Core runtime types own their __new__ slot like CPython (bound to
        // the type object itself), and modules resolve their run type for
        // both __new__ and __class__.
        var script = new LythonEngine().Compile("""
            import os
            def f():
                pass
            class E:
                def m(self):
                    pass
            return [f.__new__.__qualname__, f.__new__ is f.__new__,
                f.__new__.__self__ is type(f),
                E().m.__new__.__qualname__,
                E().m.__new__.__self__ is type(E().m),
                os.__new__.__qualname__, os.__new__.__self__ is type(os),
                os.__new__ is os.__new__,
                None.__new__.__qualname__,
                None.__new__.__self__ is type(None),
                os.__class__ is type(os),
                type(f.__new__).__name__,
                dict.__new__.__qualname__,
                {}.__new__ is dict.__new__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "function.__new__", true, true, "method.__new__", true,
            "module.__new__", true, true, "NoneType.__new__", true, true,
            "builtin_function_or_method", "dict.__new__", true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MetatypeNewSlot()
    {
        // The type metatype carries its own __new__ slot like CPython
        // while user classes keep sharing object.__new__.
        var script = new LythonEngine().Compile("""
            class E:
                pass
            return [type.__new__ is object.__new__,
                type.__new__.__qualname__,
                type.__new__.__self__ is type,
                type.__new__.__module__ is None,
                type(type.__new__).__name__,
                E.__new__ is object.__new__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            false, "type.__new__", true, true, "builtin_function_or_method",
            true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DatetimeNewSlots()
    {
        // Datetime runtime types own their __new__ slot like CPython, on
        // the type and on values alike.
        var script = new LythonEngine().Compile("""
            import datetime
            d = datetime.timedelta(1)
            t = datetime.time(1)
            return [datetime.timedelta.__new__.__qualname__,
                datetime.timedelta.__new__ is datetime.timedelta.__new__,
                d.__new__ is datetime.timedelta.__new__,
                (d.__new__).__self__ is datetime.timedelta,
                t.__new__ is datetime.time.__new__,
                datetime.date.__new__.__qualname__,
                datetime.datetime.__new__.__qualname__,
                datetime.timezone.__new__.__qualname__,
                datetime.timedelta.__new__.__module__ is None,
                type(datetime.timedelta.__new__).__name__,
                hasattr(d, "__new__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "timedelta.__new__", true, true, true, true,
            "date.__new__", "datetime.__new__", "timezone.__new__", true,
            "builtin_function_or_method", true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionNewSlots()
    {
        // Builtin exception types own or inherit their __new__ slot along
        // the builtin hierarchy like CPython; module exceptions stay
        // missing and unrelated types keep their own slots.
        var script = new LythonEngine().Compile("""
            import csv
            return [ValueError.__new__.__qualname__,
                ValueError.__new__ is ValueError.__new__,
                ValueError("bad").__new__ is ValueError.__new__,
                (ValueError("bad").__new__).__self__ is ValueError,
                KeyError.__new__ is LookupError.__new__,
                (KeyError("k").__new__).__qualname__,
                FileNotFoundError.__new__ is OSError.__new__,
                TypeError.__new__ is Exception.__new__,
                type(ValueError.__new__).__name__,
                hasattr(ValueError("bad"), "__new__"),
                hasattr(csv.Error, "__new__")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "ValueError.__new__", true, true, true, true,
            "LookupError.__new__", true, false,
            "builtin_function_or_method", true, false,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task OpaqueNewSlots()
    {
        // Random, struct_time and tzinfo own their __new__ slot while
        // pure-Python-shaped opaques share object.__new__, like CPython.
        var script = new LythonEngine().Compile("""
            import datetime
            import random
            import statistics
            import time
            r = random.Random()
            t = time.struct_time((2024, 1, 1, 0, 0, 0, 0, 1, -1))
            n = statistics.NormalDist(0, 1)
            return [random.Random.__new__.__qualname__,
                r.__new__ is random.Random.__new__,
                (r.__new__).__self__ is random.Random,
                t.__new__ is time.struct_time.__new__,
                (t.__new__).__qualname__,
                datetime.tzinfo.__new__.__qualname__,
                statistics.NormalDist.__new__ is object.__new__,
                n.__new__ is object.__new__,
                type(n.__new__).__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "Random.__new__", true, true, true, "struct_time.__new__",
            "tzinfo.__new__", true, true, "builtin_function_or_method",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CollectionNewSlots()
    {
        // Collections members route by name like CPython: deque owns its
        // slot, the dict subclasses inherit dict slot, and ChainMap shares
        // the object slot.
        var script = new LythonEngine().Compile("""
            import collections
            d = collections.deque()
            c = collections.Counter()
            o = collections.OrderedDict()
            return [collections.deque.__new__.__qualname__,
                d.__new__ is collections.deque.__new__,
                (d.__new__).__self__ is collections.deque,
                collections.Counter.__new__ is dict.__new__,
                c.__new__ is dict.__new__,
                collections.defaultdict.__new__ is dict.__new__,
                o.__new__ is dict.__new__,
                collections.OrderedDict.__new__ is dict.__new__,
                collections.ChainMap.__new__ is object.__new__,
                type(collections.deque.__new__).__name__,
                d.__class__ is collections.deque,
                c.__class__ is collections.Counter,
                type(d) is collections.deque]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "deque.__new__", true, true, true, true, true, true, true,
            true, "builtin_function_or_method", true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task PartialNewSlots()
    {
        // The partial factory owns its __new__ slot like CPython, on the
        // factory and on partial values alike (which also fixes their
        // class identity).
        var script = new LythonEngine().Compile("""
            import functools
            def f(a):
                return a
            p = functools.partial(f, 1)
            return [functools.partial.__new__.__qualname__,
                p.__new__ is functools.partial.__new__,
                (p.__new__).__self__ is functools.partial,
                type(p.__new__).__name__,
                p.__class__ is functools.partial,
                type(p) is functools.partial]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "partial.__new__", true, true, "builtin_function_or_method",
            true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FactoryNewSlots()
    {
        // Factory members reuse existing slots like CPython: partialmethod
        // shares object.__new__ while the namedtuple factory resolves the
        // shared function slot.
        var script = new LythonEngine().Compile("""
            import functools
            import collections
            def f():
                pass
            return [functools.partialmethod.__new__ is object.__new__,
                collections.namedtuple.__new__.__qualname__,
                collections.namedtuple.__new__ is collections.namedtuple.__new__,
                collections.namedtuple.__new__ is object.__new__,
                (collections.namedtuple.__new__).__self__ is type(f),
                type(functools.partialmethod.__new__).__name__,
                type(collections.namedtuple.__new__).__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, "function.__new__", true, false, true,
            "builtin_function_or_method", "builtin_function_or_method",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NewSlotConstruction()
    {
        // Construction routes through the owning slot like CPython, with
        // explicit failures for owner mismatches and missing receivers.
        var script = new LythonEngine().Compile("""
            import datetime
            import collections
            import functools
            results = []
            results.append(int.__new__(int, 5) == 5)
            results.append(str.__new__(str) == "")
            results.append((collections.deque.__new__(collections.deque, [1])).__class__ is collections.deque)
            results.append(len(collections.deque.__new__(collections.deque, [1, 2])) == 2)
            results.append((datetime.timedelta.__new__(datetime.timedelta, 1)).days == 1)
            results.append((functools.partial.__new__(functools.partial, len)).__class__ is functools.partial)
            results.append((ValueError.__new__(ValueError, "bad")).__class__ is ValueError)
            results.append((KeyError.__new__(KeyError, "k")).__class__ is KeyError)
            try:
                int.__new__(str, "5")
            except TypeError:
                results.append("mismatch")
            try:
                int.__new__()
            except TypeError:
                results.append("arity")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true,
            "mismatch", "arity",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MultipleExceptClauses()
    {
        // Multiple except clauses match in order like CPython, with
        // per-clause binding, else/finally interplay, bare fallthrough,
        // and propagation of unmatched exceptions.
        var script = new LythonEngine().Compile("""
            results = []
            try:
                raise ValueError("v")
            except ValueError:
                results.append("value")
            except TypeError:
                results.append("type")
            try:
                raise TypeError("t")
            except ValueError:
                results.append("value")
            except TypeError:
                results.append("type")
            try:
                raise KeyError("k")
            except ValueError:
                results.append("value")
            except KeyError as e:
                results.append("key")
            try:
                pass
            except ValueError:
                results.append("value")
            except TypeError:
                results.append("type")
            else:
                results.append("else")
            try:
                raise ValueError("v")
            except TypeError:
                results.append("type")
            except:
                results.append("bare")
            try:
                raise ValueError("v")
            except ValueError:
                results.append("value")
            except TypeError:
                results.append("type")
            finally:
                results.append("finally")
            try:
                try:
                    raise KeyError("k")
                except ValueError:
                    results.append("wrong")
                except TypeError:
                    results.append("wrong")
            except KeyError:
                results.append("propagated")
            try:
                raise ValueError("v")
            except Exception:
                results.append("exception")
            except ValueError:
                results.append("value")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "value", "type", "key", "else", "bare", "value", "finally",
            "propagated", "exception",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public void MisplacedBareExceptIsRejected()
    {
        // A default except clause before the last one is rejected like
        // CPython instead of silently shadowing later handlers.
        var script = new LythonEngine().Compile("""
            try:
                pass
            except:
                pass
            except ValueError:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1075");
    }

    [Fact]
    public async Task TypeMemberCallableIdentity()
    {
        // Datetime type members expose CPython-style identity like
        // C-implemented methods, with the defining type as __self__.
        var script = new LythonEngine().Compile("""
            import datetime
            return [datetime.date.today.__name__,
                datetime.date.today.__qualname__,
                datetime.date.today.__module__ is None,
                datetime.date.today.__self__ is datetime.date,
                datetime.date.fromordinal.__qualname__,
                datetime.date.fromordinal.__self__ is datetime.date,
                datetime.datetime.now.__self__ is datetime.datetime,
                type(datetime.date.today).__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "today", "date.today", true, true, "date.fromordinal", true,
            true, "builtin_function_or_method",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BareRaiseReraises()
    {
        // A bare raise re-raises the active exception like CPython, and
        // fails explicitly outside a handler.
        var script = new LythonEngine().Compile("""
            results = []
            try:
                try:
                    raise ValueError("inner")
                except ValueError:
                    raise
            except ValueError as e:
                results.append("reraised")
            try:
                raise
            except RuntimeError:
                results.append("no-active")
            try:
                try:
                    raise KeyError("k")
                except KeyError:
                    raise
                results.append("unreached")
            except KeyError:
                results.append("key")
            finally:
                results.append("finally")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "reraised", "no-active", "key", "finally",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task HandlerAndCaseBindingsReadInFunctionScope()
    {
        // Except-as and match-case bindings read like CPython inside their
        // own bodies when the reader sits in a function scope.
        var script = new LythonEngine().Compile("""
            results = []
            def read_except():
                try:
                    raise ValueError("boom")
                except ValueError as e:
                    return str(e)
                return "unreached"
            results.append(read_except())
            def read_case(subject):
                match subject:
                    case {"value": captured} if captured > 10:
                        return captured
                    case [first, *rest]:
                        return rest
                    case _:
                        return "none"
            results.append(read_case({"value": 42}))
            results.append(read_case([1, 2, 3]))
            results.append(read_case(0))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "boom",
            new BigInteger(42),
            new List<object?> { new BigInteger(2), new BigInteger(3) },
            "none",
        };
        var syncBindings = script.Run(new MockLythonHost());
        Assert.True(syncBindings.Success, syncBindings.Failure?.Message);
        Assert.Equal(expected, syncBindings.ReturnValue);

        var asyncBindings = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncBindings.Success, asyncBindings.Failure?.Message);
        Assert.Equal(expected, asyncBindings.ReturnValue);
    }

    [Fact]
    public async Task ExceptionImplicitContextChaining()
    {
        // Raises chain the active handler exception as __context__ like
        // CPython; explicit `from` additionally suppresses the context while
        // bare raises continue the active chain instead of starting a new one.
        var script = new LythonEngine().Compile("""
            results = []
            plain = ValueError("plain")
            results.append(plain.__context__ is None)
            results.append(plain.__suppress_context__ == False)
            try:
                try:
                    raise KeyError("inner")
                except KeyError:
                    raise ValueError("outer")
            except ValueError as e:
                results.append(e.__context__.__class__ is KeyError)
                results.append(e.__context__.args == ("inner",))
                results.append(e.__cause__ is None)
                results.append(e.__suppress_context__ == False)
            try:
                try:
                    raise KeyError("k2")
                except KeyError:
                    raise ValueError("v2") from TypeError("t")
            except ValueError as e2:
                results.append(e2.__cause__.args == ("t",))
                results.append(e2.__context__.args == ("k2",))
                results.append(e2.__suppress_context__ == True)
            try:
                try:
                    raise KeyError("k3")
                except KeyError:
                    raise ValueError("v3") from None
            except ValueError as e3:
                results.append(e3.__cause__ is None)
                results.append(e3.__context__.args == ("k3",))
                results.append(e3.__suppress_context__ == True)
            try:
                try:
                    raise KeyError("k4") from NameError("n4")
                except KeyError:
                    raise
            except KeyError as e4:
                results.append(e4.__cause__.args == ("n4",))
                results.append(e4.__suppress_context__ == True)
                results.append(e4.__context__ is None)
            def nested():
                try:
                    raise KeyError("outer")
                except KeyError:
                    try:
                        raise TypeError("inner")
                    except TypeError:
                        pass
                    raise ValueError("after")
            try:
                nested()
            except ValueError as e5:
                results.append(e5.__context__.args == ("outer",))
            def fled():
                try:
                    1 // 0
                except ZeroDivisionError:
                    raise KeyError("k")
            try:
                try:
                    fled()
                finally:
                    raise ValueError("v")
            except ValueError as e6:
                results.append(e6.__context__.args == ("k",))
            def selfreraise():
                try:
                    raise KeyError("s")
                except KeyError as a:
                    raise a
            try:
                selfreraise()
            except KeyError as e7:
                results.append(e7.__context__ is None)
                results.append(e7.args == ("s",))
            try:
                raise ValueError("lonely")
            except ValueError as e8:
                results.append(e8.__context__ is None)
                results.append(e8.__suppress_context__ == False)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true, true,
            true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task KeyErrorSingleArgumentStrUsesRepr()
    {
        // A single KeyError argument renders through repr like CPython
        // instead of str; every other arity and type keeps str rendering.
        var script = new LythonEngine().Compile("""
            results = []
            key = KeyError("k")
            results.append(str(key))
            results.append(repr(key))
            results.append(key.args == ("k",))
            results.append(key.message)
            results.append(str(KeyError(42)))
            results.append(str(KeyError()))
            results.append(str(KeyError("a", "b")))
            results.append(str(ValueError("x")))
            results.append(str(ValueError()))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "'k'",
            "KeyError('k')",
            true,
            "'k'",
            "42",
            string.Empty,
            "('a', 'b')",
            "x",
            string.Empty,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task KeyErrorMissingKeyCarriesKey()
    {
        // Mapping misses carry their key like CPython, so str renders the
        // key through repr and args holds it; reads, deletes, ChainMap
        // lookups, defaultdict misses, pop and percent formatting agree.
        var script = new LythonEngine().Compile("""
            results = []
            d = {"a": 1}
            for k in ["b"]:
                try:
                    d[k]
                except KeyError as e:
                    results.append(str(e))
                    results.append(e.args == ("b",))
                    results.append(repr(e))
            for k in ["c"]:
                try:
                    del d[k]
                except KeyError as e:
                    results.append(str(e))
                    results.append(e.args == ("c",))
            from collections import ChainMap
            cm = ChainMap({"a": 1})
            try:
                cm["z"]
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("z",))
            from collections import defaultdict
            dd = defaultdict(None)
            try:
                dd["k"]
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("k",))
            try:
                d.pop("q")
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("q",))
            try:
                x = "%(x)s" % {}
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("x",))
            try:
                d[(1, 2)]
            except KeyError as e:
                results.append(str(e))
            try:
                d[99]
            except KeyError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "'b'", true, "KeyError('b')",
            "'c'", true,
            "'z'", true,
            "'k'", true,
            "'q'", true,
            "'x'", true,
            "(1, 2)",
            "99",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionInstanceClassIdentity()
    {
        // Exception instances report their run type object like CPython, so
        // type(e), e.__class__ and sys.exc_info()[0] all alias the same
        // object the class name resolves to, for builtin and module types.
        var script = new LythonEngine().Compile("""
            import sys
            results = []
            try:
                raise KeyError("a")
            except KeyError as e:
                results.append(type(e) is KeyError)
                results.append(e.__class__ is type(e))
                results.append(e.__new__ is type(e).__new__)
                results.append(e.__init_subclass__.__self__ is KeyError)
                info = sys.exc_info()
                results.append(info[0] is KeyError)
                results.append(info[1] is e)
                results.append(info[0] is type(e))
            import csv
            try:
                raise csv.Error("x")
            except csv.Error as ce:
                results.append(type(ce) is csv.Error)
                results.append(ce.__class__ is type(ce))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ZeroDivisionMessagesDistinguishIntAndFloat()
    {
        // Division-by-zero messages name the operation and operand kind like
        // CPython across int and float division, floor division, modulo and
        // divmod, while funded divisions keep their values.
        var script = new LythonEngine().Compile("""
            results = []
            try:
                x = 1 % 0
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = 1.0 % 0.0
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = 1 // 0
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = 1.0 // 0.0
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = 1 / 0
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = 1.0 / 0.0
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = divmod(1, 0)
            except ZeroDivisionError as e:
                results.append(str(e))
            try:
                x = divmod(1.5, 0.0)
            except ZeroDivisionError as e:
                results.append(str(e))
            results.append(divmod(7, 3) == (2, 1))
            results.append(7.5 % 2 == 1.5)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "integer modulo by zero",
            "float modulo by zero",
            "integer division or modulo by zero",
            "float floor division by zero",
            "division by zero",
            "float division by zero",
            "integer division or modulo by zero",
            "float divmod()",
            true,
            true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionWithTracebackNone()
    {
        // With no traceback model, clearing with None returns the exception
        // itself like CPython, while arities and other values fail with the
        // CPython error texts; the returned exception raises normally.
        var script = new LythonEngine().Compile("""
            results = []
            e = ValueError("x")
            results.append(e.with_traceback(None) is e)
            results.append(e.with_traceback(None).args == ("x",))
            try:
                e.with_traceback()
            except TypeError as t:
                results.append(str(t) == "BaseException.with_traceback() takes exactly one argument (0 given)")
            try:
                e.with_traceback(None, None)
            except TypeError as t:
                results.append(str(t) == "BaseException.with_traceback() takes exactly one argument (2 given)")
            try:
                e.with_traceback(42)
            except TypeError as t:
                results.append(str(t) == "__traceback__ must be a traceback or None")
            try:
                raise e.with_traceback(None)
            except ValueError as caught:
                results.append(caught.args == ("x",))
                results.append(str(caught) == "x")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FactoryCallableClassIdentity()
    {
        // Factory and member callables report their defining kind like
        // CPython: the namedtuple factory is a function, while partial,
        // partialmethod and deque members are types, each aliasing the
        // object their __class__ and __new__ owner resolve to.
        var script = new LythonEngine().Compile("""
            from collections import namedtuple, deque
            from functools import partial, partialmethod
            results = []
            results.append(type(namedtuple) is type(lambda: 0))
            results.append(namedtuple.__class__ is type(namedtuple))
            results.append(namedtuple.__new__ is type(namedtuple).__new__)
            results.append(type(partial) is type)
            results.append(partial.__class__ is type)
            results.append(type(partialmethod) is type)
            results.append(type(deque) is type)
            results.append(deque.__class__ is type)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
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
    public async Task ObjectSlotCallSemanticsOnBuiltinValues()
    {
        // The object attribute slots accept builtin receivers like CPython:
        // setattr routes writable members (namespaces) through statement
        // assignment, getattribute resolves through the member choke with
        // receivers attached, and anything without an attribute table fails
        // with AttributeError; instances keep their terminal behavior.
        var script = new LythonEngine().Compile("""
            import argparse
            results = []
            ns = argparse.Namespace()
            ns.__setattr__("y", 2)
            results.append(ns.y)
            x = [1, 2]
            results.append(x.__getattribute__("append").__self__ is x)
            results.append(x.__getattribute__("append").__name__)
            class C:
                pass
            c = C()
            c.__setattr__("v", 10)
            results.append(c.v)
            c.__delattr__("v")
            results.append(hasattr(c, "v"))
            try:
                (1).__setattr__("x", 1)
            except AttributeError:
                results.append("setattr-attr")
            try:
                (1).__delattr__("x")
            except AttributeError:
                results.append("delattr-attr")
            try:
                x.__getattribute__("bogus")
            except AttributeError:
                results.append("getattr-attr")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(2), true, "append", new BigInteger(10), false,
            "setattr-attr", "delattr-attr", "getattr-attr",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task InitSubclassBindsOwningType()
    {
        // __init_subclass__ binds the owning type like CPython: user and
        // builtin types, exception types and type-denoting factories bind
        // themselves, the function-shaped namedtuple factory binds function,
        // and instances bind their value class.
        var script = new LythonEngine().Compile("""
            from collections import namedtuple, deque
            from functools import partial, partialmethod
            results = []
            results.append(int.__init_subclass__.__self__ is int)
            results.append(dict.__init_subclass__.__self__ is dict)
            results.append(KeyError.__init_subclass__.__self__ is KeyError)
            results.append(namedtuple.__init_subclass__.__self__ is type(namedtuple))
            results.append(deque.__init_subclass__.__self__ is deque)
            results.append(partial.__init_subclass__.__self__ is partial)
            results.append(partialmethod.__init_subclass__.__self__ is partialmethod)
            class C:
                pass
            results.append(C.__init_subclass__.__self__ is C)
            results.append([].__init_subclass__.__self__ is type([]))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NamedTupleTypeIdentity()
    {
        // Created namedtuple types report the type builtin like CPython
        // while instances share their defining type object (own __new__
        // slots live in FunctionShapedNewSlots).
        var script = new LythonEngine().Compile("""
            from collections import namedtuple
            NT = namedtuple("NT", ["x"])
            v = NT("a")
            results = []
            results.append(type(NT) is type)
            results.append(NT.__class__ is type)
            results.append(v.__class__ is NT)
            results.append(type(v) is NT)
            results.append(NT.__init_subclass__.__self__ is NT)
            results.append(v.__init_subclass__.__self__ is NT)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
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
    public async Task FunctionShapedNewSlots()
    {
        // Pure-Python-modeled types own a plain-function __new__ slot like
        // CPython: per-type identity shared with instances, function class,
        // qualified names, exact-owner construction, and explicit failure
        // otherwise; the module stays None like the other slot wrappers.
        var script = new LythonEngine().Compile("""
            from collections import namedtuple
            NT = namedtuple("NT", ["x"])
            NT2 = namedtuple("NT2", ["x"])
            v = NT("a")
            results = []
            results.append(NT.__new__ is object.__new__)
            results.append(v.__new__ is NT.__new__)
            results.append(NT.__new__ is NT2.__new__)
            results.append(type(NT.__new__) is type(lambda: 0))
            results.append(NT.__new__.__qualname__)
            results.append(NT.__new__.__module__ is None)
            results.append(NT.__new__(NT, "b").x)
            try:
                NT.__new__(NT2, "b")
            except TypeError:
                results.append("mismatch-typeerror")
            from pathlib import Path
            p = Path("a")
            results.append(Path.__new__ is object.__new__)
            results.append(p.__new__ is Path.__new__)
            results.append(type(Path.__new__) is type(lambda: 0))
            results.append(Path.__new__.__qualname__)
            results.append(str(Path.__new__(Path, "b")))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            false, true, false, true, "NT.__new__", true, "b",
            "mismatch-typeerror",
            false, true, true, "Path.__new__", "b",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ValueModuleReportsDefiningModule()
    {
        // Values report their defining module like CPython where it exists;
        // path uses the model label instead of the platform concrete one,
        // and static shapes without one keep their rejection below.
        var script = new LythonEngine().Compile("""
            import datetime
            import decimal
            import random
            import statistics
            import re
            import os
            import time
            from collections import Counter, deque, ChainMap, defaultdict
            from pathlib import Path
            results = []
            results.append(decimal.Decimal("1").__module__)
            p = Path("a")
            results.append(p.__module__)
            results.append(p.__module__ is Path.__module__)
            results.append(time.gmtime().__module__)
            results.append(Counter("ab").__module__)
            results.append(deque([1]).__module__)
            results.append(ChainMap({"a": 1}).__module__)
            results.append(defaultdict(int).__module__)
            results.append(random.Random().__module__)
            results.append(statistics.NormalDist().__module__)
            m = re.match("a", "a")
            results.append(m.__module__)
            results.append(os.stat(".").__module__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "decimal", "pathlib", true, "time", "collections", "collections",
            "collections", "collections", "random", "statistics", "re", "os",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TimetupleReturnsStructTime()
    {
        // date/datetime timetuple paths return struct_time values like
        // CPython (fields, indexing, zones, class identity without needing
        // the time import); aware timetuples report unknown DST.
        var script = new LythonEngine().Compile("""
            import datetime
            import time
            results = []
            t = datetime.date(2024, 1, 2).timetuple()
            results.append(type(t) is time.struct_time)
            results.append(t.__class__ is time.struct_time)
            results.append(t.tm_year)
            results.append(t[7])
            results.append(t.tm_zone is None)
            results.append(t.tm_gmtoff is None)
            results.append(isinstance(t, tuple))
            u = datetime.datetime(2024, 1, 2, 3, 4, 5, 6).utctimetuple()
            results.append(u.tm_hour)
            results.append(u.tm_isdst)
            a = datetime.datetime(2024, 1, 2, tzinfo=datetime.timezone.utc).timetuple()
            results.append(a.tm_isdst)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, new BigInteger(2024), new BigInteger(2), true, true,
            true, new BigInteger(3), new BigInteger(0), new BigInteger(-1),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IsoCalendarDateIdentity()
    {
        // isocalendar results share one opaque type object like the other
        // runtime types, with fields, module and subclass binding intact.
        var script = new LythonEngine().Compile("""
            import datetime
            results = []
            ic = datetime.date(2024, 1, 2).isocalendar()
            results.append(type(ic).__name__)
            results.append(ic.__class__ is type(ic))
            results.append(ic.__init_subclass__.__self__ is type(ic))
            results.append(ic.__module__)
            results.append((ic.year, ic.week, ic.weekday))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "IsoCalendarDate", true, true, "datetime",
            new List<object?> { new BigInteger(2024), new BigInteger(1), new BigInteger(2) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StructTimeTypeInitSubclassBindsSelf()
    {
        // The struct_time type binds itself like CPython, matching the
        // value route, which already binds the defining type object.
        var script = new LythonEngine().Compile("""
            import time
            results = []
            results.append(time.struct_time.__init_subclass__.__self__ is time.struct_time)
            t = time.gmtime()
            results.append(t.__init_subclass__.__self__ is time.struct_time)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TypingCallableClassIdentity()
    {
        // Typing callables report their defining kind like CPython:
        // factory functions are functions, while TypeVar/NewType are
        // types; each aliases the object its class reads resolve to.
        var script = new LythonEngine().Compile("""
            import typing
            results = []
            results.append(type(typing.NamedTuple) is type(lambda: 0))
            results.append(typing.NamedTuple.__class__ is type(typing.NamedTuple))
            results.append(type(typing.TypedDict) is type(lambda: 0))
            results.append(type(typing.TypeVar) is type)
            results.append(typing.TypeVar.__class__ is type)
            results.append(type(typing.NewType) is type)
            results.append(type(typing.cast) is type(lambda: 0))
            results.append(type(typing.get_origin) is type(lambda: 0))
            results.append(type(typing.get_args) is type(lambda: 0))
            results.append(typing.NamedTuple.__init_subclass__.__self__ is type(typing.NamedTuple))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SetMissingElementCarriesKey()
    {
        // Set misses carry their element like CPython, so str renders the
        // key through repr and args holds it; the pop message keeps its
        // text while gaining the argument.
        var script = new LythonEngine().Compile("""
            results = []
            s = {1, 2}
            try:
                s.remove(3)
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == (3,))
            try:
                set().pop()
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("pop from an empty set",))
            s.discard(99)
            results.append(sorted(s))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "3", true, "'pop from an empty set'", true,
            new List<object?> { new BigInteger(1), new BigInteger(2) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NamedTupleTypeModule()
    {
        // Created namedtuple types report their defining module like
        // CPython: an explicit module value is stored as-is while a missing
        // or None module aliases the caller module, and instances inherit it.
        var script = new LythonEngine().Compile("""
            from collections import namedtuple
            NT = namedtuple("NT", ["x"])
            results = []
            results.append(NT.__module__)
            def f():
                return namedtuple("In", ["y"]).__module__
            results.append(f())
            N2 = namedtuple("N2", ["x"], module="custom.mod")
            results.append(N2.__module__)
            N3 = namedtuple("N3", ["x"], module=None)
            results.append(N3.__module__)
            results.append(NT("a").__module__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "__main__", "__main__", "custom.mod", "__main__", "__main__",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TypingAliasNames()
    {
        // Typing aliases report their short name and defining module like
        // CPython, including subscripted and special forms.
        var script = new LythonEngine().Compile("""
            import typing
            results = []
            results.append(typing.NamedTuple.__name__)
            results.append(typing.NamedTuple.__module__)
            results.append(typing.TypedDict.__name__)
            results.append(typing.List.__name__)
            results.append(typing.List.__module__)
            results.append(typing.List[int].__name__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "NamedTuple", "typing", "TypedDict", "List", "typing", "List",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EnvironMissingKeyCarriesKey()
    {
        // os.environ misses carry their key like CPython, so str renders
        // the key through repr and args holds it; hits still read through.
        var script = new LythonEngine().Compile("""
            import os
            results = []
            try:
                os.environ["lython_missing_key_xyz"]
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("lython_missing_key_xyz",))
            try:
                del os.environ["lython_missing_key_xyz"]
            except KeyError as e:
                results.append(str(e))
                results.append(e.args == ("lython_missing_key_xyz",))
            results.append(os.environ.get("lython_missing_key_xyz") is None)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "'lython_missing_key_xyz'", true, "'lython_missing_key_xyz'", true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NewTypeResultIdentity()
    {
        // NewType results share one opaque type object with their name and
        // defining module intact, and factory callables report their own
        // short names, all like CPython.
        var script = new LythonEngine().Compile("""
            import typing
            results = []
            U = typing.NewType("U", int)
            results.append(type(U).__name__)
            results.append(U.__class__ is type(U))
            results.append(U.__name__)
            results.append(U.__module__)
            results.append(U(5))
            results.append(U.__init_subclass__.__self__ is type(U))
            results.append(typing.TypeVar.__name__)
            results.append(typing.NewType.__name__)
            results.append(typing.cast.__module__)
            results.append(typing.get_origin.__name__)
            results.append(typing.get_args.__module__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "NewType", true, "U", "__main__", new BigInteger(5), true,
            "TypeVar", "NewType", "typing", "get_origin", "typing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundMethodValueEquality()
    {
        // Bound methods compare by receiver and function like CPython
        // across engine methods, user methods and slot wrappers, with
        // matching hashes so they round-trip as dictionary keys.
        var script = new LythonEngine().Compile("""
            results = []
            x = [1, 2]
            results.append(x.append == x.append)
            results.append(x.append != x.append)
            m = x.append
            results.append(m == x.append)
            results.append(x.append == x.extend)
            y = [1, 2]
            results.append(x.append == y.append)
            class C:
                def meth(self):
                    pass
            c = C()
            results.append(c.meth == c.meth)
            class E:
                pass
            e = E()
            results.append(e.__init__ == e.__init__)
            results.append(hash(x.append) == hash(x.append))
            d = {}
            d[x.append] = 1
            results.append(d[x.append])
            results.append(len == len)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, false, true, false, false, true, true, true, new BigInteger(1), true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task UnboundBuiltinTypeMethods()
    {
        // Type constructors expose their instance methods as unbound
        // descriptors like CPython: reads share one cached wrapper per
        // constructor with descriptor identity, and calls take the
        // receiver first with exact descriptor failure texts.
        var script = new LythonEngine().Compile("""
            results = []
            x = [1]
            results.append(list.append == list.append)
            results.append(list.append != list.append)
            results.append(list.append == list.extend)
            results.append(list.append == x.append)
            results.append(type(list.append).__name__)
            results.append(type(list.append) is type(str.upper))
            results.append(list.append.__name__)
            results.append(list.append.__qualname__)
            results.append(list.append.__objclass__ is list)
            results.append(str(list.append))
            results.append(hasattr(list.append, "__self__"))
            results.append(hasattr(list.append, "__module__"))
            results.append(hasattr(list, "append"))
            results.append(callable(list.append))
            list.append(x, 2)
            results.append(x == [1, 2])
            m = list.append
            m(x, 3)
            results.append(x == [1, 2, 3])
            results.append(str.upper("ab"))
            results.append(str.join("-", ["a", "b"]))
            d = {}
            dict.update(d, {"k": 1})
            results.append(dict.get(d, "k"))
            s = {1}
            set.add(s, 2)
            results.append(s == {1, 2})
            results.append(bytes.decode(b"ab"))
            results.append(hash(list.append) == hash(list.append))
            e = {}
            e[list.append] = 7
            results.append(e[list.append])
            try:
                list.append()
            except TypeError as t:
                results.append(str(t))
            try:
                list.append(1, 2)
            except TypeError as t:
                results.append(str(t))
            try:
                list.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, false, false, false, "method_descriptor", true,
            "append", "list.append", true, "<method 'append' of 'list' objects>",
            false, false, true, true, true, true, "AB", "a-b",
            new BigInteger(1), true, "ab", true, new BigInteger(7),
            "unbound method list.append() needs an argument",
            "descriptor 'append' for 'list' objects doesn't apply to a 'int' object",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task UnboundDescriptorGetProtocol()
    {
        // The __get__ slot of unbound descriptors binds like a CPython
        // method-wrapper: receivers resolve to their bound member, None
        // against an explicit type returns the descriptor itself, and
        // arity, keyword and receiver failures use the wrapper texts.
        var script = new LythonEngine().Compile("""
            results = []
            x = [1, 2]
            g = list.append.__get__
            results.append(type(g).__name__)
            results.append(g.__name__)
            results.append(g.__qualname__)
            results.append(g.__self__ is list.append)
            results.append(g.__objclass__ is type(list.append))
            results.append(g == list.append.__get__)
            results.append(hash(g) == hash(list.append.__get__))
            results.append(callable(g))
            b = list.append.__get__(x, list)
            results.append(b == x.append)
            results.append(type(b).__name__)
            results.append(b.__self__ is x)
            b(3)
            results.append(x == [1, 2, 3])
            results.append(list.append.__get__(None, list) is list.append)
            try:
                list.append.__get__(None)
            except TypeError as t:
                results.append(str(t))
            try:
                list.append.__get__()
            except TypeError as t:
                results.append(str(t))
            try:
                list.append.__get__(x, list, 1)
            except TypeError as t:
                results.append(str(t))
            try:
                list.append.__get__(obj=x)
            except TypeError as t:
                results.append(str(t))
            try:
                list.append.__get__(1, list)
            except TypeError as t:
                results.append(str(t))
            results.append(hasattr(list.append, "__get__"))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "method-wrapper", "__get__", "method_descriptor.__get__", true, true,
            true, true, true, true, "builtin_function_or_method", true, true,
            true, "__get__(None, None) is invalid",
            " expected at least 1 argument, got 0",
            " expected at most 2 arguments, got 3",
            "wrapper __get__() takes no keyword arguments",
            "descriptor 'append' for 'list' objects doesn't apply to a 'int' object",
            true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BuiltinTypeClassMethods()
    {
        // Builtin classmethods (dict.fromkeys, bytes.fromhex) and the
        // str.maketrans staticmethod behave like CPython: type and instance
        // reads share bound-to-type callables with builtin identity, and
        // calls validate with the exact wrapper texts.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(dict.fromkeys(["a", "b"]) == {"a": None, "b": None})
            results.append(dict.fromkeys(["a", "b"], 0) == {"a": 0, "b": 0})
            results.append(dict.fromkeys("ab") == {"a": None, "b": None})
            results.append({}.fromkeys([1], []) == {1: []})
            results.append(bytes.fromhex("41 42").decode() == "AB")
            results.append(bytes.fromhex("41").decode() == "A")
            results.append(str.maketrans("ab", "cd") == {97: 99, 98: 100})
            results.append(str.maketrans("ab", "cd", "e") == {97: 99, 98: 100, 101: None})
            results.append(str.maketrans({"a": "b"}) == {97: "b"})
            results.append(str.maketrans({97: 98}) == {97: 98})
            results.append(type(dict.fromkeys).__name__)
            results.append(dict.fromkeys.__self__ is dict)
            results.append(dict.fromkeys.__name__)
            results.append(dict.fromkeys.__qualname__)
            results.append(dict.fromkeys.__module__ is None)
            results.append(str.maketrans.__self__ is None)
            results.append(str.maketrans.__module__ is None)
            results.append(dict.fromkeys == dict.fromkeys)
            results.append(dict.fromkeys is dict.fromkeys)
            results.append({}.fromkeys == dict.fromkeys)
            results.append(str.maketrans is str.maketrans)
            results.append("x".maketrans == str.maketrans)
            results.append(hash(dict.fromkeys) == hash(dict.fromkeys))
            results.append(callable(bytes.fromhex))
            results.append(hasattr(dict, "fromkeys"))
            try:
                dict.fromkeys()
            except TypeError as t:
                results.append(str(t))
            try:
                dict.fromkeys("a", 1, 2)
            except TypeError as t:
                results.append(str(t))
            try:
                bytes.fromhex()
            except TypeError as t:
                results.append(str(t))
            try:
                bytes.fromhex("41", "42")
            except TypeError as t:
                results.append(str(t))
            try:
                bytes.fromhex(65)
            except TypeError as t:
                results.append(str(t))
            try:
                bytes.fromhex("zz")
            except ValueError as v:
                results.append(str(v))
            try:
                bytes.fromhex("414")
            except ValueError as v:
                results.append(str(v))
            try:
                str.maketrans()
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans("a")
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans("ab", "c")
            except ValueError as v:
                results.append(str(v))
            try:
                str.maketrans("a", 1)
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans(1, "b")
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans("a", "b", 1)
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans(["a"])
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans({1.5: "b"})
            except TypeError as t:
                results.append(str(t))
            try:
                str.maketrans({"ab": "c"})
            except ValueError as v:
                results.append(str(v))
            try:
                dict.fromkeys(value=1)
            except TypeError as t:
                results.append(str(t))
            try:
                dict.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            "builtin_function_or_method", true, "fromkeys", "dict.fromkeys",
            true, true, true, true, false, true, true, true, true, true, true,
            "fromkeys expected at least 1 argument, got 0",
            "fromkeys expected at most 2 arguments, got 3",
            "bytes.fromhex() takes exactly one argument (0 given)",
            "bytes.fromhex() takes exactly one argument (2 given)",
            "fromhex() argument must be str, not int",
            "non-hexadecimal number found in fromhex() arg at position 0",
            "non-hexadecimal number found in fromhex() arg at position 3",
            "maketrans expected at least 1 argument, got 0",
            "if you give only one argument to maketrans it must be a dict",
            "the first two maketrans arguments must have equal length",
            "maketrans() argument 2 must be str, not int",
            "first maketrans argument must be a string if there is a second argument",
            "maketrans() argument 3 must be str, not int",
            "if you give only one argument to maketrans it must be a dict",
            "keys in translate table must be strings or integers",
            "string keys in translate table must be of length 1",
            "dict.fromkeys() takes no keyword arguments",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BuiltinTypeDirLists()
    {
        // dir() on builtin type constructors lists their instance members
        // plus the construction slot like CPython (other dunders stay out,
        // matching the instance lists), and every listed name resolves.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(dir(list) == ["__new__", "append", "clear", "copy", "count", "extend", "index", "insert", "pop", "remove", "reverse", "sort"])
            results.append(dir(str) == ["__new__", "capitalize", "casefold", "center", "count", "encode", "endswith", "expandtabs", "find", "format", "format_map", "index", "isalnum", "isalpha", "isascii", "isdecimal", "isdigit", "isidentifier", "islower", "isnumeric", "isprintable", "isspace", "istitle", "isupper", "join", "ljust", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper", "zfill"])
            results.append(dir(bytes) == ["__new__", "capitalize", "count", "decode", "endswith", "find", "fromhex", "hex", "index", "isalnum", "isalpha", "isascii", "isdigit", "islower", "isspace", "istitle", "isupper", "join", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper"])
            results.append(dir(dict) == ["__new__", "clear", "copy", "fromkeys", "get", "items", "keys", "pop", "popitem", "setdefault", "update", "values"])
            results.append(dir(set) == ["__new__", "add", "clear", "copy", "difference", "difference_update", "discard", "intersection", "intersection_update", "isdisjoint", "issubset", "issuperset", "pop", "remove", "symmetric_difference", "symmetric_difference_update", "union", "update"])
            results.append(dir([]) == ["append", "clear", "copy", "count", "extend", "index", "insert", "pop", "remove", "reverse", "sort"])
            results.append(dir({}) == ["clear", "copy", "fromkeys", "get", "items", "keys", "pop", "popitem", "setdefault", "update", "values"])
            results.append(dir("") == ["capitalize", "casefold", "center", "count", "encode", "endswith", "expandtabs", "find", "format", "format_map", "index", "isalnum", "isalpha", "isascii", "isdecimal", "isdigit", "isidentifier", "islower", "isnumeric", "isprintable", "isspace", "istitle", "isupper", "join", "ljust", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper", "zfill"])
            results.append(dir(b"") == ["capitalize", "count", "decode", "endswith", "find", "fromhex", "hex", "index", "isalnum", "isalpha", "isascii", "isdigit", "islower", "isspace", "istitle", "isupper", "join", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper"])
            for n in dir(list):
                if not hasattr(list, n):
                    results.append(n)
            for n in dir(dict):
                if not hasattr(dict, n):
                    results.append(n)
            for n in dir(str):
                if not hasattr(str, n):
                    results.append(n)
            for n in dir(bytes):
                if not hasattr(bytes, n):
                    results.append(n)
            for n in dir(set):
                if not hasattr(set, n):
                    results.append(n)
            results.append("truthful")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, "truthful",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DictPopitem()
    {
        // dict.popitem removes the last pair like CPython (last-enumerated,
        // matching every other enumeration-based path), with a key-carrying
        // KeyError on empty dictionaries and unbound access on the type.
        var script = new LythonEngine().Compile("""
            results = []
            d = {"a": 1, "b": 2}
            results.append(d.popitem() == ("b", 2))
            results.append(d == {"a": 1})
            results.append(dict.popitem(d) == ("a", 1))
            results.append(d == {})
            results.append(d.popitem == d.popitem)
            results.append(dict.popitem == dict.popitem)
            results.append(type(dict.popitem).__name__)
            results.append(type(d.popitem).__name__)
            results.append(dict.popitem.__name__)
            results.append(d.popitem.__self__ is d)
            results.append(hasattr(dict.popitem, "__self__"))
            results.append("popitem" in dir(dict))
            results.append("popitem" in dir({}))
            e = {}
            try:
                e.popitem()
            except KeyError as k:
                results.append(str(k))
                results.append(k.args == ("popitem(): dictionary is empty",))
            try:
                dict.popitem(d, 1)
            except TypeError as t:
                results.append(str(t))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true,
            true, true, "method_descriptor", "builtin_function_or_method",
            "popitem", true, false, true, true,
            "'popitem(): dictionary is empty'", true,
            "dict.popitem() expects no arguments.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DefaultDictPopFamily()
    {
        // defaultdict pop/update/popitem mirror the dict shapes like
        // CPython (inherited semantics, bound to the instance), with
        // key-carrying misses and the shared static surface.
        var script = new LythonEngine().Compile("""
            import collections
            results = []
            d = collections.defaultdict(int)
            d["a"] = 1
            d["b"] = 2
            results.append(d.pop("a"))
            results.append(len(d) == 1)
            results.append(d.get("b"))
            results.append(d.pop("zz", "dflt"))
            try:
                d.pop("zz")
            except KeyError as k:
                results.append(str(k))
                results.append(k.args == ("zz",))
            d.update({"c": 3})
            results.append(d.get("c"))
            results.append(len(d))
            results.append(d.popitem())
            results.append(len(d))
            e = collections.defaultdict(int)
            try:
                e.popitem()
            except KeyError as k:
                results.append(str(k))
                results.append(k.args == ("popitem(): dictionary is empty",))
            results.append(d.pop == d.pop)
            results.append(type(d.pop).__name__)
            results.append(d.pop.__self__ is d)
            results.append(d.popitem == d.popitem)
            results.append(d.pop.__name__)
            results.append(hasattr(d, "pop"))
            results.append(hasattr(d, "update"))
            results.append(hasattr(d, "popitem"))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(1), true, new BigInteger(2), "dflt",
            "'zz'", true, new BigInteger(3), new BigInteger(2),
            new List<object?> { "c", new BigInteger(3) }, new BigInteger(1),
            "'popitem(): dictionary is empty'", true,
            true, "builtin_function_or_method", true, true, "pop",
            true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TupleIndexCount()
    {
        // Plain tuples serve index/count like CPython (mirroring the list
        // shapes), with unbound descriptors on the constructor, static
        // coverage, and dir() lists.
        var script = new LythonEngine().Compile("""
            results = []
            t = (1, 2, 2)
            results.append(t.index(2))
            results.append(t.index(2, 2))
            results.append(t.count(2))
            results.append(t.count(9))
            results.append(tuple.index((1, 2), 2))
            results.append(t.index == t.index)
            results.append(tuple.index == tuple.index)
            results.append(tuple.index == t.index)
            results.append(type(tuple.index).__name__)
            results.append(type(t.index).__name__)
            results.append(t.index.__self__ is t)
            results.append(hasattr(tuple.index, "__self__"))
            results.append(dir(t) == ["count", "index"])
            results.append(dir(tuple) == ["__new__", "count", "index"])
            results.append("index" in dir(tuple))
            try:
                (1,).index(9)
            except ValueError as v:
                results.append(str(v))
            try:
                (1,).index()
            except TypeError as e:
                results.append(str(e))
            try:
                tuple.index(1, 2)
            except TypeError as e:
                results.append(str(e))
            try:
                tuple.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(1), new BigInteger(2), new BigInteger(2),
            new BigInteger(0), new BigInteger(1),
            true, true, false, "method_descriptor", "builtin_function_or_method",
            true, false, true, true, true,
            "tuple.index(value): value is not in tuple",
            "Method 'tuple.index' is missing argument 'value'.",
            "descriptor 'index' for 'tuple' objects doesn't apply to a 'int' object",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NamedTupleSequenceMembers()
    {
        // Namedtuple values serve the tuple index/count shapes like CPython
        // through the shared member core, on both namedtuple flavors.
        var script = new LythonEngine().Compile("""
            import collections
            import typing
            results = []
            P = collections.namedtuple("P", ["x", "y"])
            p = P(1, 2)
            results.append(p.index(2))
            results.append(p.index(2, 1))
            results.append(p.count(1))
            results.append(p.count(9))
            results.append(p.index == p.index)
            results.append(type(p.index).__name__)
            results.append(p.index.__self__ is p)
            results.append(tuple.index(p, 1))
            T = typing.NamedTuple("T", [("x", int), ("y", int)])
            t = T(3, 4)
            results.append(t.index(4))
            results.append(t.count(3))
            results.append(t.index == t.index)
            results.append(tuple.index(t, 3))
            try:
                p.index(9)
            except ValueError as v:
                results.append(str(v))
            try:
                p.count()
            except TypeError as e:
                results.append(str(e))
            results.append(p.x)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(1), new BigInteger(1), new BigInteger(1),
            new BigInteger(0), true, "builtin_function_or_method", true,
            new BigInteger(0), new BigInteger(1), new BigInteger(1), true,
            new BigInteger(0),
            "tuple.index(value): value is not in tuple",
            "Method 'tuple.count' is missing argument 'value'.",
            new BigInteger(1),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IntScalarMembers()
    {
        // Integer values (bools ride along) serve the scalar method and
        // property shapes like CPython, with unbound descriptors on the
        // constructor, static coverage, and dir() lists.
        var script = new LythonEngine().Compile("""
            results = []
            results.append((0).bit_length())
            results.append((-7).bit_length())
            results.append((255).bit_length())
            results.append((2**100).bit_length())
            results.append(True.bit_length())
            results.append((5).conjugate())
            results.append((-3).conjugate())
            results.append((5).as_integer_ratio() == (5, 1))
            results.append((-4).as_integer_ratio() == (-4, 1))
            results.append((5).is_integer())
            results.append((5).numerator)
            results.append((5).denominator)
            results.append(True.denominator)
            results.append((5).real)
            results.append((-5).imag)
            results.append(int.bit_length(7))
            b = 7
            results.append(b.bit_length == b.bit_length)
            results.append(int.bit_length == int.bit_length)
            results.append(int.bit_length == (7).bit_length)
            results.append(type(int.bit_length).__name__)
            results.append(hasattr(int.bit_length, "__self__"))
            results.append(int.bit_length.__name__)
            results.append(dir(5) == ["as_integer_ratio", "bit_count", "bit_length", "conjugate", "denominator", "from_bytes", "imag", "is_integer", "numerator", "real", "to_bytes"])
            results.append(dir(int) == ["__new__", "as_integer_ratio", "bit_count", "bit_length", "conjugate", "denominator", "from_bytes", "imag", "is_integer", "numerator", "real", "to_bytes"])
            for n in dir(int):
                if not hasattr(int, n):
                    results.append(n)
            for n in dir(5):
                if not hasattr(5, n):
                    results.append(n)
            results.append("truthful")
            try:
                (1).bit_length(1)
            except TypeError as e:
                results.append(str(e))
            try:
                int.bit_length()
            except TypeError as e:
                results.append(str(e))
            try:
                int.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(0), new BigInteger(3), new BigInteger(8),
            new BigInteger(101), new BigInteger(1),
            new BigInteger(5), new BigInteger(-3), true, true, true,
            new BigInteger(5), new BigInteger(1), new BigInteger(1),
            new BigInteger(5), new BigInteger(0), new BigInteger(3),
            true, true, false, "method_descriptor", false, "bit_length",
            true, true, "truthful",
            "int.bit_length() takes no arguments (1 given)",
            "unbound method int.bit_length() needs an argument",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FloatScalarMembers()
    {
        // Float values serve the scalar method and property shapes like
        // CPython, with unbound descriptors on the constructor, static
        // coverage, and dir() lists.
        var script = new LythonEngine().Compile("""
            results = []
            results.append((1.5).conjugate() == 1.5)
            results.append((-2.0).conjugate() == -2.0)
            results.append((1.5).real == 1.5)
            results.append((1.5).imag == 0.0)
            results.append((2.0).is_integer())
            results.append((2.5).is_integer())
            results.append((1.5).as_integer_ratio() == (3, 2))
            results.append((0.5).as_integer_ratio() == (1, 2))
            results.append((-0.5).as_integer_ratio() == (-1, 2))
            results.append((2.0).as_integer_ratio() == (2, 1))
            results.append((0.1).as_integer_ratio() == (3602879701896397, 36028797018963968))
            results.append(float.conjugate(1.5) == 1.5)
            f = 2.5
            results.append(f.is_integer == f.is_integer)
            results.append(float.conjugate == float.conjugate)
            results.append(float.conjugate == (1.5).conjugate)
            results.append(type(float.conjugate).__name__)
            results.append(hasattr(float.conjugate, "__self__"))
            results.append(float.conjugate.__name__)
            results.append(dir(1.5) == ["as_integer_ratio", "conjugate", "fromhex", "hex", "imag", "is_integer", "real"])
            results.append(dir(float) == ["__new__", "as_integer_ratio", "conjugate", "fromhex", "hex", "imag", "is_integer", "real"])
            for n in dir(float):
                if not hasattr(float, n):
                    results.append(n)
            for n in dir(1.5):
                if not hasattr(1.5, n):
                    results.append(n)
            results.append("truthful")
            try:
                (1.5).conjugate(1)
            except TypeError as e:
                results.append(str(e))
            try:
                (0.5).as_integer_ratio(1)
            except TypeError as e:
                results.append(str(e))
            try:
                float("inf").as_integer_ratio()
            except OverflowError as e:
                results.append(str(e))
            try:
                float("nan").as_integer_ratio()
            except ValueError as e:
                results.append(str(e))
            try:
                float.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, false, true, true, true, true,
            true, true, true, true, false, "method_descriptor", false,
            "conjugate", true, true, "truthful",
            "float.conjugate() takes no arguments (1 given)",
            "float.as_integer_ratio() takes no arguments (1 given)",
            "cannot convert Infinity to integer ratio",
            "cannot convert NaN to integer ratio",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IntByteConversions()
    {
        // int.to_bytes and int.from_bytes convert like CPython across
        // orders, signs, defaults and keyword shapes, with the exact
        // argument diagnostics.
        var script = new LythonEngine().Compile("""
            results = []
            results.append((1024).to_bytes(2, "big") == b"\x04\x00")
            results.append((1024).to_bytes(2, "little") == b"\x00\x04")
            results.append((-1).to_bytes(1, "big", signed=True) == b"\xff")
            results.append((-129).to_bytes(2, "big", signed=True) == b"\xff\x7f")
            results.append((256).to_bytes(2, "big", signed=True) == b"\x01\x00")
            results.append((0).to_bytes(0, "big") == b"")
            results.append((1).to_bytes() == b"\x01")
            results.append((2**100).to_bytes(13, "big") == b"\x10\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00")
            results.append(int.from_bytes(b"\x04\x00", "big") == 1024)
            results.append(int.from_bytes(b"\x00\x04", "little") == 1024)
            results.append(int.from_bytes(b"\xff", "big", signed=True) == -1)
            results.append(int.from_bytes([65]) == 65)
            results.append(int.from_bytes((65,), "big") == 65)
            results.append(int.from_bytes(range(65, 67), "big") == 16706)
            results.append(int.from_bytes(b"AB", byteorder="big", signed=True) == 16706)
            results.append(type(int.from_bytes).__name__)
            results.append(int.from_bytes.__self__ is int)
            results.append(int.from_bytes == int.from_bytes)
            results.append(int.from_bytes.__name__)
            try:
                (256).to_bytes(1, "big")
            except OverflowError as e:
                results.append(str(e))
            try:
                (-1).to_bytes(1, "big")
            except OverflowError as e:
                results.append(str(e))
            try:
                (1).to_bytes(2, "middle")
            except ValueError as e:
                results.append(str(e))
            try:
                (1).to_bytes("2", "big")
            except TypeError as e:
                results.append(str(e))
            try:
                (1).to_bytes(2, "big", "yes")
            except TypeError as e:
                results.append(str(e))
            try:
                (1).to_bytes(-1, "big")
            except ValueError as e:
                results.append(str(e))
            try:
                int.from_bytes("AB", "big")
            except TypeError as e:
                results.append(str(e))
            try:
                int.from_bytes([256], "big")
            except ValueError as e:
                results.append(str(e))
            try:
                int.from_bytes(b"A", "middle")
            except ValueError as e:
                results.append(str(e))
            try:
                int.from_bytes(b"A", 1)
            except TypeError as e:
                results.append(str(e))
            try:
                int.from_bytes()
            except TypeError as e:
                results.append(str(e))
            try:
                int.from_bytes(b"A", "big", True)
            except TypeError as e:
                results.append(str(e))
            try:
                (1).to_bytes(1, "big", length=2)
            except TypeError as e:
                results.append(str(e))
            try:
                int.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            true, true, true, true, true, "builtin_function_or_method", true,
            true, "from_bytes",
            "int too big to convert",
            "can't convert negative int to unsigned",
            "byteorder must be either 'little' or 'big'",
            "'str' object cannot be interpreted as an integer",
            "to_bytes() takes at most 2 positional arguments (3 given)",
            "length argument must be non-negative",
            "cannot convert 'str' object to bytes",
            "bytes must be in range(0, 256)",
            "byteorder must be either 'little' or 'big'",
            "from_bytes() argument 'byteorder' must be str, not int",
            "from_bytes() missing required argument 'bytes' (pos 1)",
            "from_bytes() takes at most 2 positional arguments (3 given)",
            "argument for to_bytes() given by name ('length') and position (1)",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FloatHexConversions()
    {
        // float.hex and float.fromhex round-trip exactly like CPython,
        // including subnormal edges, overflow, and the full grammar.
        var script = new LythonEngine().Compile("""
            results = []
            results.append((1.5).hex())
            results.append((0.0).hex())
            results.append((-0.0).hex())
            results.append((0.1).hex())
            results.append(float("inf").hex())
            results.append(float("nan").hex())
            results.append(float.fromhex("0x1.8000000000000p+0") == 1.5)
            results.append(float.fromhex("1.5") == 1.3125)
            results.append(float.fromhex("inf") == float("inf"))
            results.append(float.fromhex("-Infinity") == float("-inf"))
            results.append(float.fromhex("  0x10p0 ") == 16.0)
            results.append(float.fromhex("0x1p1023") == 8.98846567431158e+307)
            results.append(float.fromhex("0x1.fffffffffffff8p0") == 2.0)
            results.append(float.fromhex("0x1p-1074") == 5e-324)
            results.append(float.fromhex("0x3p-1075") == 1e-323)
            results.append(float.fromhex("0x1p-1075") == 0.0)
            results.append(type(float.fromhex).__name__)
            results.append(float.fromhex.__self__ is float)
            results.append(float.fromhex == float.fromhex)
            results.append(float.fromhex.__name__)
            results.append(dir(1.5) == ["as_integer_ratio", "conjugate", "fromhex", "hex", "imag", "is_integer", "real"])
            results.append(dir(float) == ["__new__", "as_integer_ratio", "conjugate", "fromhex", "hex", "imag", "is_integer", "real"])
            try:
                (1.5).hex(1)
            except TypeError as e:
                results.append(str(e))
            try:
                float.fromhex()
            except TypeError as e:
                results.append(str(e))
            try:
                float.fromhex("a", "b")
            except TypeError as e:
                results.append(str(e))
            try:
                float.fromhex(1)
            except TypeError as e:
                results.append(str(e))
            try:
                float.fromhex("zz")
            except ValueError as e:
                results.append(str(e))
            try:
                float.fromhex("0x1p")
            except ValueError as e:
                results.append(str(e))
            try:
                float.fromhex("0x1p+9999999999")
            except OverflowError as e:
                results.append(str(e))
            try:
                float.fromhex(string="0x1p0")
            except TypeError as e:
                results.append(str(e))
            try:
                float.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "0x1.8000000000000p+0", "0x0.0p+0", "-0x0.0p+0",
            "0x1.999999999999ap-4", "inf", "nan",
            true, true, true, true, true, true, true, true, true, true,
            "builtin_function_or_method", true, true, "fromhex", true, true,
            "float.hex() takes no arguments (1 given)",
            "float.fromhex() takes exactly one argument (0 given)",
            "float.fromhex() takes exactly one argument (2 given)",
            "bad argument type for built-in operation",
            "invalid hexadecimal floating-point string",
            "invalid hexadecimal floating-point string",
            "hexadecimal value too large to represent as a float",
            "float.fromhex() takes no keyword arguments",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoolTypeDelegation()
    {
        // bool shares int's method surface like CPython (identical
        // wrappers from int's cache), while its own dunders, bool.from_bytes
        // conversion, and dir() stay bool-shaped.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(bool.bit_length is int.bit_length)
            results.append(bool.conjugate is int.conjugate)
            results.append(bool.to_bytes is int.to_bytes)
            results.append(bool.from_bytes is int.from_bytes)
            results.append(bool.__new__ is int.__new__)
            results.append(bool.bit_length(True))
            results.append(bool.from_bytes(b"A", "big"))
            results.append(True.bit_length())
            results.append((True).to_bytes(1, "big") == b"\x01")
            results.append(bool.__name__)
            results.append(bool.from_bytes.__qualname__)
            results.append(bool.from_bytes.__self__ is bool)
            results.append(dir(bool) == dir(int))
            results.append(hasattr(bool, "bogus"))
            try:
                bool.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, false, false, new BigInteger(1), true,
            new BigInteger(1), true, "bool", "bool.from_bytes", true, true,
            false, "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NamedTupleTypeMembers()
    {
        // Namedtuple types share tuple's descriptors like CPython (identical
        // objects from the run constructor cache), on both flavors.
        var script = new LythonEngine().Compile("""
            import collections
            import typing
            results = []
            P = collections.namedtuple("P", ["x", "y"])
            results.append(P.index is tuple.index)
            results.append(P.count is tuple.count)
            results.append(P.index(P(1, 2), 1))
            results.append(P.count(P(2, 2), 2))
            results.append(P.index == tuple.index)
            T = typing.NamedTuple("T", [("x", int)])
            results.append(T.index is tuple.index)
            results.append(T.count is tuple.count)
            results.append(T.index(T(3), 3))
            results.append(hasattr(P, "index"))
            results.append(P.__name__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, new BigInteger(0), new BigInteger(2), true,
            true, true, new BigInteger(0), true, "P",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NamedTupleDirLists()
    {
        // dir() on namedtuple values and types lists fields plus their
        // served members like CPython, and every listed name resolves.
        var script = new LythonEngine().Compile("""
            import collections
            import typing
            results = []
            P = collections.namedtuple("P", ["x", "y"])
            p = P(1, 2)
            results.append(dir(p) == ["_asdict", "_field_defaults", "_fields", "_make", "_replace", "count", "index", "x", "y"])
            results.append(dir(P) == ["__new__", "_field_defaults", "_fields", "_make", "count", "index", "x", "y"])
            results.append(p._make([3, 4]) == P(3, 4))
            results.append(p._field_defaults == {})
            T = typing.NamedTuple("T", [("x", int), ("y", int)])
            t = T(1, 2)
            results.append(dir(t) == ["_fields", "_replace", "count", "index", "x", "y"])
            results.append(dir(T) == ["count", "index", "x", "y"])
            for n in dir(p):
                if not hasattr(p, n):
                    results.append(n)
            for n in dir(P):
                if not hasattr(P, n):
                    results.append(n)
            for n in dir(t):
                if not hasattr(t, n):
                    results.append(n)
            for n in dir(T):
                if not hasattr(T, n):
                    results.append(n)
            results.append("truthful")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, "truthful",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesTranslateMaketrans()
    {
        // bytes.maketrans builds the 256-table like CPython and bytes
        // translate applies it with an optional delete set; raw bound
        // shapes compare by name and receiver with coherent hashes.
        var script = new LythonEngine().Compile("""
            results = []
            t = bytes.maketrans(b"ab", b"cd")
            results.append(len(t))
            results.append(t[97:100] == b"cdc")
            results.append(b"aabbcc".translate(t) == b"ccddcc")
            results.append(b"aabbcc".translate(t, b"b") == b"cccc")
            results.append(b"aabbcc".translate(t, delete=b"b") == b"cccc")
            results.append(type(bytes.maketrans).__name__)
            results.append(bytes.maketrans.__self__ is None)
            results.append(bytes.maketrans.__name__)
            results.append(bytes.maketrans.__qualname__)
            results.append(bytes.maketrans.__module__ is None)
            x = b"xx"
            results.append(x.translate == x.translate)
            results.append(hash(x.translate) == hash(x.translate))
            results.append(bytes.translate == bytes.translate)
            results.append(bytes.maketrans is bytes.maketrans)
            results.append(b"".maketrans is bytes.maketrans)
            results.append(bytes.translate(b"ab", t) == b"cd")
            results.append("maketrans" in dir(bytes))
            results.append("translate" in dir(b""))
            d = {"a": 1}
            results.append(d.update == d.update)
            n = 5
            results.append(n.to_bytes == n.to_bytes)
            try:
                bytes.maketrans(b"a", b"cd")
            except ValueError as e:
                results.append(str(e))
            try:
                bytes.maketrans("a", "b")
            except TypeError as e:
                results.append(str(e))
            try:
                bytes.maketrans(b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".translate(b"cd")
            except ValueError as e:
                results.append(str(e))
            try:
                b"ab".translate()
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".translate(t, t, t)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".translate(t, bogus=1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(256), true, true, true, true,
            "builtin_function_or_method", true, "maketrans", "bytes.maketrans", true,
            true, true, true, true, true, true, true, true, true, true,
            "maketrans arguments must have same length",
            "a bytes-like object is required, not 'str'",
            "maketrans expected 2 arguments, got 1",
            "translation table must be 256 characters long",
            "translate() takes at least 1 positional argument (0 given)",
            "translate() takes at most 2 arguments (3 given)",
            "translate() got an unexpected keyword argument 'bogus'",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StringTranslate()
    {
        // str.translate rewrites through mapping tables like CPython
        // (None deletes, integers splice as characters, misses are kept),
        // with the exact argument diagnostics.
        var script = new LythonEngine().Compile("""
            results = []
            results.append("abc".translate({97: 98}))
            results.append("abc".translate({97: None}))
            results.append("abc".translate(str.maketrans("a", "b")))
            results.append("abc".translate({}))
            results.append("abc".translate({97: "XYZ"}))
            results.append("abc".translate([1]))
            results.append(type("abc".translate).__name__)
            s = "abc"
            results.append(s.translate == s.translate)
            results.append(str.translate == str.translate)
            results.append("abc".translate.__name__)
            results.append("abc".translate.__qualname__)
            results.append("abc".translate.__self__)
            results.append("abc".translate.__module__ is None)
            results.append("translate" in dir("abc"))
            results.append("translate" in dir(str))
            try:
                "abc".translate()
            except TypeError as e:
                results.append(str(e))
            try:
                "abc".translate({}, {})
            except TypeError as e:
                results.append(str(e))
            try:
                "abc".translate(table={})
            except TypeError as e:
                results.append(str(e))
            try:
                "abc".translate(42)
            except TypeError as e:
                results.append(str(e))
            try:
                "abc".translate({97: 5.5})
            except TypeError as e:
                results.append(str(e))
            try:
                "abc".translate({97: 1114112})
            except ValueError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "bbc", "bc", "bbc", "abc", "XYZbc", "abc",
            "builtin_function_or_method", true, true, "translate",
            "str.translate", "abc", true, true, true,
            "str.translate() takes exactly one argument (0 given)",
            "str.translate() takes exactly one argument (2 given)",
            "str.translate() takes no keyword arguments",
            "'int' object is not subscriptable",
            "character mapping must return integer, None or str",
            "character mapping must be in range(0x110000)",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DefaultDictDictInterop()
    {
        // defaultdict compares and constructs by mapping content like
        // CPython (the factory never participates in either).
        var script = new LythonEngine().Compile("""
            import collections
            results = []
            d = collections.defaultdict(int)
            d["a"] = 1
            results.append(d == {"a": 1})
            results.append({"a": 1} == d)
            results.append(d == collections.defaultdict(int, {"a": 1}))
            results.append(d == collections.defaultdict(str, {"a": 1}))
            results.append(d == {"a": 2})
            results.append(d == {})
            results.append(d == [("a", 1)])
            results.append(d == 1)
            results.append(d != {"a": 2})
            results.append(d != {"a": 1})
            results.append(dict(d) == {"a": 1})
            results.append(dict(d, b=2) == {"a": 1, "b": 2})
            results.append(dict(collections.defaultdict(int)) == {})
            results.append(d == d)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, false, false, false, false, true, false,
            true, true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NamedTupleFieldDescriptors()
    {
        // Namedtuple field reads share per-field descriptors like CPython,
        // with documentation, method-wrapper slots, and dir() coverage.
        var script = new LythonEngine().Compile("""
            import collections
            import typing
            results = []
            P = collections.namedtuple("P", ["x", "y"])
            results.append(str(P.x))
            results.append(type(P.x).__name__)
            results.append(P.x.__doc__)
            results.append(P.x.__module__)
            results.append(hasattr(P.x, "__name__"))
            results.append(P.x == P.x)
            results.append(P.x is P.x)
            results.append(P.x == P.y)
            results.append(P.x.__get__(P(1, 2)))
            results.append(P.x.__get__(None, P) is P.x)
            results.append(type(P.x.__get__).__name__)
            results.append(P.x.__get__.__qualname__)
            results.append(P.x.__get__.__self__ is P.x)
            results.append(P.x.__set__.__qualname__)
            try:
                P.x.__get__(1)
            except TypeError as e:
                results.append(str(e))
            try:
                P.x.__set__(P(1, 2), 5)
            except AttributeError as e:
                results.append(str(e))
            try:
                P.x.__get__()
            except TypeError as e:
                results.append(str(e))
            results.append(callable(P.x))
            T = typing.NamedTuple("T", [("x", int)])
            results.append(str(T.x))
            results.append(T.x is T.x)
            results.append(T.x == P.x)
            results.append(dir(P) == ["__new__", "_field_defaults", "_fields", "_make", "count", "index", "x", "y"])
            results.append(dir(P(1, 2)) == ["_asdict", "_field_defaults", "_fields", "_make", "_replace", "count", "index", "x", "y"])
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "<_tuplegetter(0, 'Alias for field number 0')>", "_tuplegetter",
            "Alias for field number 0", "collections", false, true, true,
            false, new BigInteger(1), true, "method-wrapper",
            "_tuplegetter.__get__", true, "_tuplegetter.__set__",
            "descriptor for index '0' for tuple subclasses doesn't apply to a 'int' object",
            "can't set attribute",
            " expected at least 1 argument, got 0",
            false,
            "<_tuplegetter(0, 'Alias for field number 0')>", true, false,
            true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StringTranslateMappings()
    {
        // str.translate accepts user mappings and defaultdicts through the
        // mapping protocol like CPython, with LookupError misses and exact
        // failure texts.
        var script = new LythonEngine().Compile("""
            import collections
            results = []
            class M:
                def __getitem__(self, k):
                    if k == 97:
                        return "X"
                    raise KeyError(k)
            results.append("abc".translate(M()))
            class T:
                def __getitem__(self, k):
                    raise TypeError("nope")
            try:
                "abc".translate(T())
            except TypeError as e:
                results.append(str(e))
            class V:
                def __getitem__(self, k):
                    return 1.5
            try:
                "abc".translate(V())
            except TypeError as e:
                results.append(str(e))
            dd = collections.defaultdict(int, {97: "Z"})
            results.append("abc".translate(dd))
            results.append(dd == {97: "Z", 98: 0, 99: 0})
            class N:
                __getitem__ = 42
            try:
                "abc".translate(N())
            except TypeError as e:
                results.append(str(e))
            class Q:
                pass
            try:
                "abc".translate(Q())
            except TypeError as e:
                results.append(str(e))
            results.append("abc".translate({97: 98}))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "Xbc", "nope", "character mapping must return integer, None or str",
            "Z\0\0", true,
            "'int' object is not callable",
            "'Q' object is not subscriptable",
            "bbc",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RangeMembers()
    {
        // Ranges serve attributes plus arithmetic index/count/contains
        // like CPython, with unbound descriptors and dir() lists.
        var script = new LythonEngine().Compile("""
            results = []
            r = range(1, 10, 2)
            results.append(r.start)
            results.append(r.stop)
            results.append(r.step)
            results.append(r.index(5))
            results.append(r.count(5))
            results.append(r.count(6))
            results.append(5 in r)
            results.append(6 in r)
            results.append(5.0 in r)
            results.append("a" in r)
            results.append(range.index(r, 5))
            results.append(r.index == r.index)
            results.append(range.index == range.index)
            results.append(range.index == r.index)
            results.append(type(range.index).__name__)
            results.append(type(r).__name__)
            results.append(r.index.__self__ is r)
            results.append(hasattr(range.index, "__self__"))
            results.append(dir(r) == ["count", "index", "start", "step", "stop"])
            results.append(dir(range) == ["__new__", "count", "index", "start", "step", "stop"])
            for n in dir(r):
                if not hasattr(r, n):
                    results.append(n)
            for n in dir(range):
                if not hasattr(range, n):
                    results.append(n)
            results.append("truthful")
            try:
                r.index(6)
            except ValueError as v:
                results.append(str(v))
            try:
                r.index()
            except TypeError as e:
                results.append(str(e))
            try:
                r.index(5, 1)
            except TypeError as e:
                results.append(str(e))
            try:
                range.index("a", 1)
            except TypeError as e:
                results.append(str(e))
            try:
                range.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(1), new BigInteger(10), new BigInteger(2),
            new BigInteger(2), new BigInteger(1), new BigInteger(0),
            true, false, true, false, new BigInteger(2),
            true, true, false, "method_descriptor", "range", true, false,
            true, true, "truthful",
            "6 is not in range",
            "Method 'range.index' is missing argument 'value'.",
            "Method 'range.index' received too many positional arguments.",
            "descriptor 'index' for 'range' objects doesn't apply to a 'str' object",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StringClassificationMembers()
    {
        // The Unicode classification members behave like CPython across
        // scripts and edge cases, with decimal tightening fixed as well.
        var script = new LythonEngine().Compile("""
            results = []
            results.append("abc123".isascii())
            results.append("caf\u00e9".isascii())
            results.append("123".isdecimal())
            results.append("\u00b2".isdecimal())
            results.append("\u00b2".isdigit())
            results.append("\u00bd".isdigit())
            results.append("\u2167".isdigit())
            results.append("\u00b2".isnumeric())
            results.append("\u00bd".isnumeric())
            results.append("\u2167".isnumeric())
            results.append("".isprintable())
            results.append("a b".isprintable())
            results.append("\u00a0".isprintable())
            results.append("caf\u00e9".isidentifier())
            results.append("_x".isidentifier())
            results.append("1a".isidentifier())
            results.append("".isidentifier())
            results.append("class".isidentifier())
            results.append("\u1885".isidentifier())
            results.append("A\u2167".isupper())
            results.append("\u00aa".islower())
            results.append("A\u01c5".isupper())
            results.append("Hello World".istitle())
            results.append("Hello world".istitle())
            results.append("Don't".istitle())
            results.append("A1B".istitle())
            results.append("AB".istitle())
            results.append("".istitle())
            results.append(str.isdecimal == str.isdecimal)
            s = "abc"
            results.append(s.isdecimal == s.isdecimal)
            results.append(type(str.isdecimal).__name__)
            results.append(str.isdecimal.__name__)
            results.append(hasattr(str, "isdecimal"))
            results.append("isdecimal" in dir("abc"))
            results.append("istitle" in dir(str))
            try:
                getattr("abc", "isdecimal")(1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, false, true, false, true, false, false, true, true, true,
            true, true, false, true, true, false, false, true, true, true,
            true, false, true, false, false, true, false, false, true, true,
            "method_descriptor", "isdecimal", true, true, true,
            "str.isdecimal() expects no arguments.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StringCaseFoldMember()
    {
        // The casefold member folds like CPython, including multi-character
        // expansions, with the usual descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append("ABC".casefold())
            results.append("Hello World".casefold())
            results.append("".casefold())
            results.append("\u00df".casefold())
            results.append("\u0130".casefold())
            results.append("\u03a3".casefold())
            results.append("\ufb00".casefold())
            results.append("A\u00df\u03a3\u01c5".casefold())
            results.append(str.casefold == str.casefold)
            s = "abc"
            results.append(s.casefold == s.casefold)
            results.append(type(str.casefold).__name__)
            results.append(str.casefold.__name__)
            results.append(hasattr(str, "casefold"))
            results.append("casefold" in dir("abc"))
            results.append("casefold" in dir(str))
            try:
                getattr("abc", "casefold")(1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "abc", "hello world", "", "ss", "i\u0307", "\u03c3", "ff", "ass\u03c3\u01c6",
            true, true, "method_descriptor", "casefold", true, true, true,
            "str.casefold() expects no arguments.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StrDirListsServedMembers()
    {
        // dir() lists every served str member like CPython: ljust and
        // format_map used to resolve (and call) while missing from dir().
        var script = new LythonEngine().Compile("""
            results = []
            results.append(hasattr("abc", "ljust"))
            results.append("ljust" in dir("abc"))
            results.append("abc".ljust(5, "."))
            results.append(hasattr("abc", "format_map"))
            results.append("format_map" in dir("abc"))
            results.append("{name}".format_map({"name": "beta"}))
            results.append(hasattr(str, "ljust"))
            results.append("ljust" in dir(str))
            results.append(str.ljust("abc", 5))
            results.append(hasattr(str, "format_map"))
            results.append("format_map" in dir(str))
            results.append(str.format_map("{v}", {"v": 1}))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, "abc..", true, true, "beta",
            true, true, "abc  ", true, true, "1",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesHexMember()
    {
        // bytes.hex renders lowercase hex like CPython, with right-grouped
        // separators and the usual descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"".hex())
            results.append(b"\xff\x00\xab".hex())
            results.append(b"abcdef".hex(":"))
            results.append(b"abcdef".hex(":", 2))
            results.append(b"abcde".hex(":", 2))
            results.append(b"abcd".hex(":", 0))
            results.append(b"abcd".hex(":", -2))
            results.append(b"abcdef".hex(bytes_per_sep=2))
            results.append(b"abcdef".hex(sep="-"))
            results.append(b"abcdef".hex(b"-", 2))
            results.append(bytes.hex(b"ab"))
            results.append(bytes.hex(b"abcdef", ":", 2))
            results.append(type(b"ab".hex).__name__)
            results.append(type(bytes.hex).__name__)
            results.append(bytes.hex.__name__)
            h = b"ab"
            results.append(h.hex == h.hex)
            results.append(bytes.hex == bytes.hex)
            results.append(hasattr(bytes, "hex"))
            results.append("hex" in dir(b"ab"))
            results.append("hex" in dir(bytes))
            try:
                b"ab".hex(":", 1, 2)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".hex(1)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".hex("::")
            except ValueError as e:
                results.append(str(e))
            try:
                b"ab".hex("\u00e9")
            except ValueError as e:
                results.append(str(e))
            try:
                b"ab".hex(":", "x")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".hex(":", sep=";")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".hex(bogus=1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "", "ff00ab", "61:62:63:64:65:66", "6162:6364:6566", "61:6263:6465",
            "61626364", "6162:6364", "616263646566", "61-62-63-64-65-66", "6162-6364-6566",
            "6162", "6162:6364:6566", "builtin_function_or_method", "method_descriptor", "hex",
            true, true, true, true, true,
            "hex() takes at most 2 arguments (3 given)",
            "hex() expects sep to be str or bytes.",
            "sep must be length 1.",
            "sep must be ASCII.",
            "bytes.hex([sep[, bytes_per_sep]]) expects bytes_per_sep to be an integer.",
            "argument for hex() given by name ('sep') and position (1)",
            "hex() got an unexpected keyword argument 'bogus'",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IntBitCountMember()
    {
        // int.bit_count counts one-bits like CPython (bools ride along),
        // with unbound descriptors and dir() lists beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append((0).bit_count())
            results.append((7).bit_count())
            results.append((-7).bit_count())
            results.append((255).bit_count())
            results.append(((2 ** 100) - 1).bit_count())
            results.append(True.bit_count())
            results.append(False.bit_count())
            results.append(int.bit_count(7))
            results.append(int.bit_count == int.bit_count)
            results.append(type(int.bit_count).__name__)
            results.append(int.bit_count.__name__)
            results.append(hasattr(int, "bit_count"))
            results.append("bit_count" in dir(5))
            results.append("bit_count" in dir(int))
            try:
                (1).bit_count(1)
            except TypeError as e:
                results.append(str(e))
            try:
                int.bit_count()
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(0), new BigInteger(3), new BigInteger(3),
            new BigInteger(8), new BigInteger(100), new BigInteger(1),
            new BigInteger(0), new BigInteger(3), true,
            "method_descriptor", "bit_count", true, true, true,
            "int.bit_count() takes no arguments (1 given)",
            "unbound method int.bit_count() needs an argument",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BuiltinDataDescriptors()
    {
        // int and float expose their scalar data attributes as getset
        // descriptors like CPython (bool shares int's cache), with documented
        // slots, binding reads, read-only writes, and dir() lists.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(repr(int.real))
            results.append(str(float.imag))
            results.append(type(int.real).__name__)
            results.append(type(float.imag).__name__)
            results.append(int.real.__name__)
            results.append(int.real.__qualname__)
            results.append(int.real.__objclass__ is int)
            results.append(int.real.__doc__)
            results.append(int.numerator.__doc__)
            results.append(float.imag.__doc__)
            results.append(int.real is int.real)
            results.append(bool.real is int.real)
            results.append(int.real.__get__(7))
            results.append(int.real.__get__(True))
            results.append(float.real.__get__(2.5) == 2.5)
            results.append(int.real.__get__(None, int) is int.real)
            results.append(hasattr(int, "real"))
            results.append("imag" in dir(int))
            results.append("real" in dir(float))
            results.append(hasattr(float.imag, "__set__"))
            results.append(hasattr(int.real, "__delete__"))
            try:
                int.real.__set__(5, 1)
            except AttributeError as e:
                results.append(str(e))
            try:
                float.imag.__delete__(2.5)
            except AttributeError as e:
                results.append(str(e))
            try:
                int.real.__get__(1.5)
            except TypeError as e:
                results.append(str(e))
            try:
                int.real.__get__()
            except TypeError as e:
                results.append(str(e))
            try:
                int.real(5)
            except TypeError as e:
                results.append(str(e))
            try:
                int.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "<attribute 'real' of 'int' objects>",
            "<attribute 'imag' of 'float' objects>",
            "getset_descriptor", "getset_descriptor", "real", "int.real",
            true, "the real part of a complex number",
            "the numerator of a rational number in lowest terms",
            "the imaginary part of a complex number",
            true, true, new BigInteger(7), new BigInteger(1), true, true,
            true, true, true, true, true,
            "attribute 'real' of 'int' objects is not writable",
            "attribute 'imag' of 'float' objects is not writable",
            "descriptor 'real' for 'int' objects doesn't apply to a 'float' object",
            " expected at least 1 argument, got 0",
            "Object is not callable.",
            "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RangeMemberDescriptors()
    {
        // range exposes start/stop/step as member descriptors like CPython,
        // sharing the getset machinery with read-only errors and None docs.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(repr(range.start))
            results.append(type(range.start).__name__)
            results.append(range.start.__doc__ is None)
            results.append(range.start.__name__)
            results.append(range.start.__qualname__)
            results.append(range.start.__objclass__ is range)
            results.append(range.start.__get__(range(2, 9, 3)))
            results.append(range.stop.__get__(range(2, 9, 3)))
            results.append(range.step.__get__(range(2, 9, 3)))
            results.append(range.start is range.start)
            results.append(range.start.__get__(None, range) is range.start)
            results.append(hasattr(range, "step"))
            results.append("stop" in dir(range))
            results.append("start" in dir(range(5)))
            try:
                range.start.__set__(range(5), 1)
            except AttributeError as e:
                results.append(str(e))
            try:
                range.stop.__delete__(range(5))
            except AttributeError as e:
                results.append(str(e))
            try:
                range.start.__get__(5)
            except TypeError as e:
                results.append(str(e))
            try:
                range.start(5)
            except TypeError as e:
                results.append(str(e))
            try:
                range.bogus
            except AttributeError:
                results.append("missing")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "<member 'start' of 'range' objects>", "member_descriptor", true,
            "start", "range.start", true, new BigInteger(2), new BigInteger(9),
            new BigInteger(3), true, true, true, true, true,
            "readonly attribute", "readonly attribute",
            "descriptor 'start' for 'range' objects doesn't apply to a 'int' object",
            "Object is not callable.", "missing",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FromBytesFromHexOnValues()
    {
        // int.from_bytes and float.fromhex bind through values like CPython
        // (bools convert through the bool flavor), with dir() lists beside.
        var script = new LythonEngine().Compile("""
            results = []
            results.append((5).from_bytes(b"A", "big"))
            results.append((5).from_bytes == int.from_bytes)
            results.append((5).from_bytes is int.from_bytes)
            results.append(type((5).from_bytes).__name__)
            results.append((5).from_bytes.__self__ is int)
            results.append((2.5).fromhex("0x1.8p+1") == 3.0)
            results.append((2.5).fromhex == float.fromhex)
            results.append(True.from_bytes(b"A", "big"))
            results.append(True.from_bytes == bool.from_bytes)
            results.append(True.from_bytes.__self__ is bool)
            results.append(hasattr(5, "from_bytes"))
            results.append("from_bytes" in dir(5))
            results.append("fromhex" in dir(2.5))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(65), true, false, "builtin_function_or_method", true,
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
    public async Task BytesSearchMembers()
    {
        // bytes search like CPython: int or bytes needles with bounds,
        // find misses against index misses, and the descriptor surface.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"abcab".count(98))
            results.append(b"abcab".count(b"b"))
            results.append(b"abcab".count(b"b", 2))
            results.append(b"abcab".count(b"b", 2, 4))
            results.append(b"abcab".find(99))
            results.append(b"abcab".find(b"d"))
            results.append(b"abcab".find(b"b", 3))
            results.append(b"abcab".index(b"c"))
            results.append(b"abcab".rfind(b"b"))
            results.append(b"abcab".rindex(b"b"))
            results.append(b"abcab".rfind(b"b", 0, 3))
            results.append(b"".count(b""))
            results.append(bytes.index(b"abcab", b"b"))
            results.append(bytes.count(b"abcab", 98))
            results.append(type(b"ab".find).__name__)
            results.append(type(bytes.find).__name__)
            results.append(bytes.find.__name__)
            h = b"ab"
            results.append(h.find == h.find)
            results.append(bytes.find == bytes.find)
            results.append(hasattr(bytes, "rindex"))
            results.append("count" in dir(b"ab"))
            results.append("rfind" in dir(bytes))
            try:
                b"ab".find()
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".find(b"a", 1, 2, 3)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".find(sub=b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".find(1.5)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".find(300)
            except ValueError as e:
                results.append(str(e))
            try:
                b"ab".find(b"a", "x")
            except TypeError as e:
                results.append(str(e))
            results.append(b"abc".find(b"", 2, 1))
            results.append(b"abc".count(b"", 2, 1))
            results.append(b"abc".rfind(b"", 2, 1))
            try:
                b"ab".index(b"z")
            except ValueError as e:
                results.append(str(e))
            try:
                b"abc".index(b"b", 2, 1)
            except ValueError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(2), new BigInteger(2), new BigInteger(1), new BigInteger(0),
            new BigInteger(2), new BigInteger(-1), new BigInteger(4), new BigInteger(2),
            new BigInteger(4), new BigInteger(4), new BigInteger(1), new BigInteger(1),
            new BigInteger(1), new BigInteger(2), "builtin_function_or_method",
            "method_descriptor", "find", true, true, true, true, true,
            "find expected at least 1 argument, got 0",
            "find expected at most 3 arguments, got 4",
            "bytes.find() takes no keyword arguments",
            "argument should be integer or bytes-like object, not 'float'",
            "byte must be in range(0, 256)",
            "slice indices must be integers or None or have an __index__ method",
            new BigInteger(-1), new BigInteger(0), new BigInteger(-1),
            "subsection not found",
            "subsection not found",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesAffixMembers()
    {
        // bytes affixes like CPython: bytes or tuple needles with bounds,
        // empty-range misses, and the descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"abc".startswith(b"a"))
            results.append(b"abc".startswith((b"x", b"a")))
            results.append(b"abc".startswith(()))
            results.append(b"abc".startswith(b""))
            results.append(b"abc".endswith(b"c"))
            results.append(b"abc".endswith((b"c", b"a")))
            results.append(b"abc".startswith(b"b", 1))
            results.append(b"abc".startswith(b"b", 1, 2))
            results.append(b"abc".endswith(b"b", 0, 2))
            results.append(b"abc".startswith(b"", 5))
            results.append(b"abc".startswith(b"", 2, 1))
            results.append(bytes.startswith(b"abc", b"a"))
            results.append(bytes.endswith(b"abc", b"c"))
            results.append(type(b"ab".startswith).__name__)
            results.append(type(bytes.startswith).__name__)
            results.append(bytes.startswith.__name__)
            results.append(hasattr(bytes, "endswith"))
            results.append("startswith" in dir(b"ab"))
            results.append("endswith" in dir(bytes))
            h = b"ab"
            results.append(h.startswith == h.startswith)
            results.append(bytes.startswith == bytes.startswith)
            try:
                b"ab".startswith()
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".startswith(b"a", 1, 2, 3)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".startswith(prefix=b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".startswith(1)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".startswith((1,))
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".startswith(b"a", "x")
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, false, true, true, true, true, true, true, false,
            false, true, true, "builtin_function_or_method", "method_descriptor",
            "startswith", true, true, true, true, true,
            "startswith expected at least 1 argument, got 0",
            "startswith expected at most 3 arguments, got 4",
            "bytes.startswith() takes no keyword arguments",
            "startswith first arg must be bytes or a tuple of bytes, not int",
            "a bytes-like object is required, not 'int'",
            "slice indices must be integers or None or have an __index__ method",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesReplaceMembers()
    {
        // bytes.replace rewrites like CPython, with count limits and the
        // descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"aaa".replace(b"aa", b"x") == b"xa")
            results.append(b"aaa".replace(b"a", b"x", 1) == b"xaa")
            results.append(b"aaa".replace(b"a", b"x", 0) == b"aaa")
            results.append(b"aaa".replace(b"a", b"x", -1) == b"xxx")
            results.append(b"".replace(b"", b"x") == b"x")
            results.append(b"ab".replace(b"", b"x") == b"xaxbx")
            results.append(b"ab".replace(b"", b"x", 1) == b"xab")
            results.append(b"ab".replace(b"a", b"") == b"b")
            results.append(b"ab".replace(b"z", b"x") == b"ab")
            results.append(b"ab".replace(b"ab", b"abcd") == b"abcd")
            results.append(bytes.replace(b"ab", b"a", b"x") == b"xb")
            results.append(bytes.replace(b"ab", b"a", b"x", 1) == b"xb")
            results.append(type(b"ab".replace).__name__)
            results.append(type(bytes.replace).__name__)
            results.append(bytes.replace.__name__)
            results.append(hasattr(bytes, "replace"))
            results.append("replace" in dir(b"ab"))
            results.append("replace" in dir(bytes))
            h = b"ab"
            results.append(h.replace == h.replace)
            results.append(bytes.replace == bytes.replace)
            try:
                b"ab".replace(b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".replace(b"a", b"x", 1, 2)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".replace(old=b"a", new=b"x")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".replace("a", b"x")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".replace(b"a", 1)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".replace(b"a", b"x", "1")
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            true, true, "builtin_function_or_method", "method_descriptor", "replace",
            true, true, true, true, true,
            "replace expected at least 2 arguments, got 1",
            "replace expected at most 3 arguments, got 4",
            "bytes.replace() takes no keyword arguments",
            "a bytes-like object is required, not 'str'",
            "a bytes-like object is required, not 'int'",
            "bytes.replace(old, new[, count]) expects count to be an integer.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesClassificationMembers()
    {
        // bytes classification matches CPython ASCII rules, with the usual
        // descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"abc123".isascii())
            results.append(b"123".isdigit())
            results.append(b"12a".isdigit())
            results.append(b"ABC".isupper())
            results.append(b"abc".islower())
            results.append(b"aB".islower())
            results.append(b"aB".isupper())
            results.append(b" ".isspace())
            results.append(b"Hello World".istitle())
            results.append(b"Hello world".istitle())
            results.append(b"A1B".istitle())
            results.append(b"caf\xe9".isascii())
            results.append(b"".isascii())
            results.append(b"".isdigit())
            results.append(b"\xff".isalpha())
            results.append(b"AB".isupper())
            results.append(b"Don\x27t".istitle())
            results.append(bytes.isalpha == bytes.isalpha)
            s = b"abc"
            results.append(s.isalpha == s.isalpha)
            results.append(type(bytes.isalpha).__name__)
            results.append(bytes.isalpha.__name__)
            results.append(hasattr(bytes, "isalpha"))
            results.append("isalpha" in dir(b"abc"))
            results.append("istitle" in dir(bytes))
            try:
                getattr(b"abc", "isalpha")(1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, false, true, true, false, false, true, true, false,
            true, false, true, false, false, true, false, true, true,
            "method_descriptor", "isalpha", true, true, true,
            "bytes.isalpha() expects no arguments.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesCaseMembers()
    {
        // bytes case maps follow CPython ASCII rules, with the usual
        // descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"AbC xYz! 123".lower() == b"abc xyz! 123")
            results.append(b"AbC xYz! 123".upper() == b"ABC XYZ! 123")
            results.append(b"AbC xYz! 123".swapcase() == b"aBc XyZ! 123")
            results.append(b"AbC xYz! 123".capitalize() == b"Abc xyz! 123")
            results.append(b"AbC xYz! 123".title() == b"Abc Xyz! 123")
            results.append(b"a1b 2c".title() == b"A1B 2C")
            results.append(b"ABC".capitalize() == b"Abc")
            results.append(b"abc".lower() == b"abc")
            results.append(b"".upper() == b"")
            results.append(b"a".capitalize() == b"A")
            results.append(bytes.lower(b"AbC") == b"abc")
            results.append(bytes.title(b"hello world") == b"Hello World")
            results.append(type(bytes.lower).__name__)
            results.append(bytes.lower.__name__)
            results.append(hasattr(bytes, "swapcase"))
            results.append("title" in dir(b"abc"))
            results.append("upper" in dir(bytes))
            s = b"abc"
            results.append(s.lower == s.lower)
            results.append(bytes.upper == bytes.upper)
            try:
                getattr(b"abc", "lower")(1)
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "upper")(x=1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            true, true, "method_descriptor", "lower", true, true, true, true, true,
            "bytes.lower() expects no arguments.",
            "bytes.upper() expects no arguments.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesStripMembers()
    {
        // bytes strips follow CPython over ASCII whitespace or a bytes
        // strip set, with the usual descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"  hello  ".strip() == b"hello")
            results.append(b"  hello  ".lstrip() == b"hello  ")
            results.append(b"  hello  ".rstrip() == b"  hello")
            results.append(b"\x09\x0a\x0b\x0c\x0d x \x0d\x0a".strip() == b"x")
            results.append(b"aabHello baa".strip(b"ab") == b"Hello ")
            results.append(b"aabHello baa".lstrip(b"ab") == b"Hello baa")
            results.append(b"aabHello baa".rstrip(b"ab") == b"aabHello ")
            results.append(b"abc".strip(None) == b"abc")
            results.append(b"abc".strip(b"") == b"abc")
            results.append(b"   ".strip() == b"")
            results.append(b"\xff\xfeab\xff".strip(b"\xff") == b"\xfeab")
            results.append(bytes.strip(b"  ab  ") == b"ab")
            results.append(type(bytes.strip).__name__)
            results.append(bytes.strip.__name__)
            results.append(hasattr(bytes, "lstrip"))
            results.append("rstrip" in dir(b"ab"))
            results.append("strip" in dir(bytes))
            s = b"abc"
            results.append(s.strip == s.strip)
            results.append(bytes.rstrip == bytes.rstrip)
            results.append(s.strip(b"") is s)
            results.append(s.strip(b"x") is s)
            results.append(s.strip() is s)
            t = b"   "
            results.append(t.strip() is t)
            e = b""
            results.append(e.strip() is e)
            results.append(e.lstrip() is e)
            try:
                getattr(b"abc", "strip")(b"a", b"b")
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "lstrip")(x=b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "strip")("a")
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "rstrip")(1)
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            true, true, "method_descriptor", "strip", true, true, true, true, true,
            true, true, true, false, true, true,
            "bytes.strip([chars]) expects zero or one argument.",
            "bytes.lstrip() takes no keyword arguments",
            "a bytes-like object is required, not 'str'",
            "a bytes-like object is required, not 'int'",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesSplitMembers()
    {
        // bytes splits follow CPython over an explicit separator,
        // whitespace, or line boundaries, with the usual descriptor
        // surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"a,b,,c".split(b",") == [b"a", b"b", b"", b"c"])
            results.append(b"a,b,,c".rsplit(b",", 1) == [b"a,b,", b"c"])
            results.append(b"  a  b  ".split() == [b"a", b"b"])
            results.append(b"  a  b  ".split(None, 1) == [b"a", b"b  "])
            results.append(b"  a  b  ".rsplit(None, 1) == [b"  a", b"b"])
            results.append(b"a\nb\rc\nd".splitlines() == [b"a", b"b", b"c", b"d"])
            results.append(b"a\nb\n".splitlines(keepends=True) == [b"a\n", b"b\n"])
            results.append(b"".split() == [])
            results.append(b"".split(b",") == [b""])
            results.append(b"".splitlines() == [])
            results.append(b"a b".split(None, 0) == [b"a b"])
            results.append(b"a,b".split(sep=b",") == [b"a", b"b"])
            results.append(b"a,b,c".rsplit(sep=b",", maxsplit=1) == [b"a,b", b"c"])
            results.append(bytes.split(b"a,b", b",") == [b"a", b"b"])
            results.append(type(bytes.split).__name__)
            results.append(bytes.split.__name__)
            results.append(hasattr(bytes, "splitlines"))
            results.append("rsplit" in dir(b"ab"))
            results.append("split" in dir(bytes))
            s = b"a,b"
            results.append(s.split == s.split)
            results.append(bytes.rsplit == bytes.rsplit)
            try:
                getattr(b"abc", "split")(b"a", b"b", b"c")
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "splitlines")(1, 2)
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "split")(x=1)
            except TypeError as e:
                results.append(str(e))
            try:
                b"abc".split(b"")
            except ValueError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "rsplit")(1)
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b"abc", "split")(b"a", "1")
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            true, true, true, true, "method_descriptor", "split", true, true, true, true, true,
            "Method 'bytes.split' received too many positional arguments.",
            "Method 'bytes.splitlines' received too many positional arguments.",
            "Method 'bytes.split' got an unexpected keyword argument 'x'.",
            "empty separator",
            "bytes.rsplit([sep[, maxsplit]]) expects zero, one, or two arguments with bytes separator and optional integer maxsplit.",
            "bytes.split([sep[, maxsplit]]) expects maxsplit to be an integer.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesPartitionMembers()
    {
        // bytes partitions follow CPython around the first or last
        // separator, with the usual descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"a,b,c".partition(b",") == (b"a", b",", b"b,c"))
            results.append(b"a,b,c".rpartition(b",") == (b"a,b", b",", b"c"))
            results.append(b"abc".partition(b",") == (b"abc", b"", b""))
            results.append(b"abc".rpartition(b",") == (b"", b"", b"abc"))
            results.append(b"".partition(b",") == (b"", b"", b""))
            results.append(b",".partition(b",") == (b"", b",", b""))
            s = b","
            results.append(b"a,b".partition(s) == (b"a", b",", b"b"))
            results.append(b"a,b".partition(s)[1] is s)
            results.append(bytes.partition(b"a,b", b",") == (b"a", b",", b"b"))
            results.append(bytes.rpartition(b"a,b", b",") == (b"a", b",", b"b"))
            results.append(type(bytes.partition).__name__)
            results.append(bytes.partition.__name__)
            results.append(hasattr(bytes, "rpartition"))
            results.append("partition" in dir(b"ab"))
            results.append("rpartition" in dir(bytes))
            h = b"ab"
            results.append(h.partition == h.partition)
            results.append(bytes.rpartition == bytes.rpartition)
            try:
                b"ab".partition()
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".partition(b"a", b"b")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".partition(sep=b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".partition(1)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".rpartition((1,))
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".partition(b"")
            except ValueError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true,
            "method_descriptor", "partition", true, true, true, true, true,
            "bytes.partition() takes exactly one argument (0 given)",
            "bytes.partition() takes exactly one argument (2 given)",
            "bytes.partition() takes no keyword arguments",
            "a bytes-like object is required, not 'int'",
            "a bytes-like object is required, not 'tuple'",
            "empty separator",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BytesJoinMembers()
    {
        // bytes join follows CPython over any iterable of bytes, with the
        // usual descriptor surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b",".join([b"a", b"b"]) == b"a,b")
            results.append(b",".join((b"a", b"b")) == b"a,b")
            results.append(b",".join(x for x in [b"a", b"b"]) == b"a,b")
            results.append(b",".join([]) == b"")
            results.append(b"".join([b"a", b"b"]) == b"ab")
            results.append(b"-".join([b""]) == b"")
            one = b"ab"
            results.append(b",".join([one]) is one)
            results.append(b"".join([one]) is one)
            results.append(bytes.join(b",", [b"a", b"b"]) == b"a,b")
            results.append(type(bytes.join).__name__)
            results.append(bytes.join.__name__)
            results.append(hasattr(bytes, "join"))
            results.append("join" in dir(b"ab"))
            results.append("join" in dir(bytes))
            j = b","
            results.append(j.join == j.join)
            results.append(bytes.join == bytes.join)
            try:
                getattr(b",", "join")()
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b",", "join")([b"a"], [b"b"])
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b",", "join")(iterable=[b"a"])
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b",", "join")(1)
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b",", "join")([b"a", "b"])
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b",", "join")([b"a", None])
            except TypeError as e:
                results.append(str(e))
            try:
                getattr(b",", "join")(b"ab")
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true,
            "method_descriptor", "join", true, true, true, true, true,
            "bytes.join() takes exactly one argument (0 given)",
            "bytes.join() takes exactly one argument (2 given)",
            "bytes.join() takes no keyword arguments",
            "Object is not iterable.",
            "sequence item 1: expected a bytes-like object, str found",
            "sequence item 1: expected a bytes-like object, NoneType found",
            "sequence item 0: expected a bytes-like object, int found",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    [Fact]
    public async Task BytesRemoveAffixMembers()
    {
        // bytes affixes like CPython, with failure paths and the descriptor
        // surface beside the values.
        var script = new LythonEngine().Compile("""
            results = []
            results.append(b"abc".removeprefix(b"a") == b"bc")
            results.append(b"abc".removeprefix(b"x") == b"abc")
            results.append(b"abc".removeprefix(b"") == b"abc")
            results.append(b"abc".removesuffix(b"c") == b"ab")
            results.append(b"abc".removesuffix(b"x") == b"abc")
            results.append(b"abc".removesuffix(b"") == b"abc")
            results.append(b"".removeprefix(b"a") == b"")
            results.append(bytes.removeprefix(b"abc", b"a") == b"bc")
            results.append(bytes.removesuffix(b"abc", b"c") == b"ab")
            results.append(type(b"ab".removeprefix).__name__)
            results.append(type(bytes.removeprefix).__name__)
            results.append(bytes.removeprefix.__name__)
            results.append(hasattr(bytes, "removesuffix"))
            results.append("removeprefix" in dir(b"ab"))
            results.append("removesuffix" in dir(bytes))
            h = b"ab"
            results.append(h.removeprefix == h.removeprefix)
            results.append(bytes.removeprefix == bytes.removeprefix)
            try:
                b"ab".removeprefix()
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".removeprefix(b"a", b"b")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".removeprefix(prefix=b"a")
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".removeprefix(1)
            except TypeError as e:
                results.append(str(e))
            try:
                b"ab".removesuffix((1,))
            except TypeError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true,
            "builtin_function_or_method", "method_descriptor", "removeprefix",
            true, true, true, true, true,
            "bytes.removeprefix() takes exactly one argument (0 given)",
            "bytes.removeprefix() takes exactly one argument (2 given)",
            "bytes.removeprefix() takes no keyword arguments",
            "a bytes-like object is required, not 'int'",
            "a bytes-like object is required, not 'tuple'",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StrRemoveAffixIdentityMembers()
    {
        // Empty str affixes return the same object like CPython, with fresh
        // objects only on real matches.
        var script = new LythonEngine().Compile("""
            s = 'abc'
            e = ''
            return [s.removeprefix('') is s, s.removesuffix('') is s,
                e.removeprefix('') is e, e.removesuffix('') is e,
                s.removeprefix('x') is s, s.removesuffix('x') is s,
                s.removeprefix('a') is s, s.removesuffix('c') is s,
                s.removeprefix('a') == 'bc', s.removesuffix('c') == 'ab']
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, false, false, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EllipsisAndNotImplemented()
    {
        // The Ellipsis literal and the NotImplemented singleton behave
        // like CPython values with their own runtime types.
        var script = new LythonEngine().Compile("""
            return [... is ..., ... is Ellipsis, NotImplemented is NotImplemented,
                (...).__class__ is type(...),
                NotImplemented.__class__ is type(NotImplemented),
                type(...).__name__, type(NotImplemented).__name__,
                str(...), repr(NotImplemented), bool(...), bool(NotImplemented),
                (...).__new__ is type(...).__new__,
                {...: 1}[...] == 1, hash(...) == hash(...)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, "ellipsis",
            "NotImplementedType", "Ellipsis", "NotImplemented", true, true,
            true, true, true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MultipleExceptControlFlow()
    {
        // Control-flow exits through multi-clause handlers behave like
        // CPython on both engines (return, reraised failures with
        // finally, and loop control with else).
        var script = new LythonEngine().Compile("""
            results = []
            def f():
                try:
                    raise ValueError("v")
                except ValueError:
                    return "handled"
                except TypeError:
                    return "wrong"
                finally:
                    pass
                return "fell-through"
            results.append(f())
            def g():
                try:
                    raise TypeError("t")
                except ValueError:
                    return "wrong"
                except TypeError:
                    raise KeyError("k")
                finally:
                    results.append("fin")
            try:
                g()
            except KeyError:
                results.append("key")
            for i in [1, 2, 3]:
                try:
                    if i == 2:
                        raise ValueError("v")
                except ValueError:
                    continue
                except TypeError:
                    results.append("wrong")
                else:
                    results.append(str(i))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "handled", "fin", "key", "1", "3",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundMethodSelf()
    {
        // Bound engine methods report their receiver like CPython,
        // including stored aliases that keep the original receiver.
        var script = new LythonEngine().Compile("""
            import hashlib
            import re
            import sys
            h = hashlib.md5(b"x")
            p = re.compile("x")
            l1 = [1]
            a = l1.append
            class E:
                pass
            e = E()
            e.append = a
            return [[].append.__self__ == [],
                "x".join.__self__ == "x",
                {1: 2}.get.__self__ == {1: 2},
                h.hexdigest.__self__ is h,
                p.match.__self__ is p,
                sys.stdout.write.__self__ is sys.stdout,
                e.append.__self__ is l1,
                a.__self__ is l1,
                type([].append.__self__).__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, "list",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RaiseFromCause()
    {
        // An explicit raise cause travels with the exception like
        // CPython, from instances and classes alike, while bad causes
        // fail explicitly.
        var script = new LythonEngine().Compile("""
            results = []
            try:
                raise ValueError("v") from KeyError("k")
            except ValueError as e:
                results.append((e.__cause__).message == "'k'")
                results.append((e.__cause__).__class__ is KeyError)
            try:
                raise ValueError("v") from KeyError
            except ValueError as e:
                results.append((e.__cause__).__class__ is KeyError)
            try:
                raise ValueError("v") from None
            except ValueError as e:
                results.append(e.__cause__ is None)
            try:
                raise ValueError("v") from 42
            except TypeError:
                results.append("cause-type")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, "cause-type",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RaiseBareClass()
    {
        // Raising an exception class instantiates it like CPython, while
        // non-exceptions still fail explicitly.
        var script = new LythonEngine().Compile("""
            results = []
            try:
                raise ValueError
            except ValueError as e:
                results.append(len(e.args) == 0)
            try:
                raise KeyError
            except KeyError as e:
                results.append(e.__class__ is KeyError)
            try:
                raise 42
            except TypeError:
                results.append("non-exception")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, "non-exception",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionNotes()
    {
        // add_note accumulates governed notes like CPython while
        // __notes__ stays missing until the first note; writes stay
        // unsupported like every other exception attribute.
        var script = new LythonEngine().Compile("""
            e = ValueError("v")
            results = []
            results.append(hasattr(e, "__notes__"))
            e.add_note("n1")
            e.add_note("n2")
            results.append(len(e.__notes__) == 2)
            results.append(e.__notes__[0] == "n1")
            results.append(e.__notes__ is e.__notes__)
            try:
                e.add_note(42)
            except TypeError:
                results.append("note-type")
            e2 = ValueError("w")
            results.append(hasattr(e2, "__notes__"))
            try:
                e.__notes__ = ["x"]
            except TypeError:
                results.append("read-only")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            false, true, true, true, "note-type", false, "read-only",
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
            import re
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
                C.__class__ is type, type(C) is type,
                re.compile("x").__class__ is type(re.compile("x")),
                re.compile("(x)").match("x").__class__ is type(re.compile("(x)").match("x")),
                re.compile("x").__new__ is object.__new__,
                re.compile("(x)").match("x").__new__ is object.__new__,
                re.compile("x").__class__.__name__,
                re.compile("(x)").match("x").__class__.__name__]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, "Pattern", "Match",
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
