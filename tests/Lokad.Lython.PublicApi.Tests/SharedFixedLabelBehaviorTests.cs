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
