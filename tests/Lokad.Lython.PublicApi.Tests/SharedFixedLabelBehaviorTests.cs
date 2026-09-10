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
