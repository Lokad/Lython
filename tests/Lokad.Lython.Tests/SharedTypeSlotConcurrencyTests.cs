using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// R21: shared __new__ slots for process-wide type singletons are published
// once in LythonRuntime's static constructor. Simultaneous first access from
// independent threads (and independent engines) must observe one identity per
// owner and never throw. A run stays single-threaded; independent engines on
// separate threads are the supported concurrency shape.
public sealed class SharedTypeSlotConcurrencyTests
{
    private static readonly object[] SlotOwners =
    [
        PyType.FunctionType,
        PyType.MethodType,
        PyType.ModuleType,
        PyType.NoneType,
        PyType.EllipsisType,
        PyType.NotImplementedType,
        PyDateTimeOps.TimedeltaType,
        PyDateTimeOps.DateType,
        PyDateTimeOps.TimeType,
        PyDateTimeOps.DateTimeType,
        PyDateTimeOps.TimezoneType,
        PyDateTimeOps.TzInfoType,
        LythonRuntime.RandomModule.RandomType,
        LythonRuntime.TimeStructTimeType.Instance,
        LythonRuntime.PartialFactory.Instance,
    ];

    private static readonly string[] ExpectedQualNames =
    [
        "function.__new__",
        "method.__new__",
        "module.__new__",
        "NoneType.__new__",
        "ellipsis.__new__",
        "NotImplementedType.__new__",
        "timedelta.__new__",
        "date.__new__",
        "time.__new__",
        "datetime.__new__",
        "timezone.__new__",
        "tzinfo.__new__",
        "Random.__new__",
        "struct_time.__new__",
        "partial.__new__",
    ];

    [Fact]
    public void ConcurrentFirstAccessObservesSingleSlotIdentityPerOwner()
    {
        const int threadCount = 16;
        using var ready = new CountdownEvent(threadCount);
        using var go = new ManualResetEventSlim(false);
        var slots = new object?[threadCount][];
        var failures = new Exception?[threadCount];
        var threads = new Thread[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            var index = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    ready.Signal();
                    if (!go.Wait(TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException("slot storm starter gate timed out.");
                    }

                    var seen = new object?[SlotOwners.Length];
                    for (var i = 0; i < SlotOwners.Length; i++)
                    {
                        Assert.True(
                            LythonRuntime.TryGetTypeNewSlot(SlotOwners[i], out var slot),
                            $"no shared slot for owner {i}");
                        Assert.NotNull(slot);
                        // A repeat lookup must alias the same wrapper: exactly-once
                        // publication, never a second instance for one owner.
                        Assert.True(
                            LythonRuntime.TryGetTypeNewSlot(SlotOwners[i], out var again),
                            $"no shared slot on repeat for owner {i}");
                        Assert.Same(slot, again);
                        seen[i] = slot;
                    }

                    slots[index] = seen;
                }
                catch (Exception ex)
                {
                    failures[index] = ex;
                }
            });
            threads[t].IsBackground = true;
            threads[t].Start();
        }

        Assert.True(ready.Wait(TimeSpan.FromSeconds(30)), "slot storm workers did not reach the gate.");
        go.Set();
        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "slot storm worker did not finish.");
        }

        foreach (var failure in failures)
        {
            Assert.Null(failure);
        }

        // Every thread observed the same wrapper per owner, with stable naming.
        for (var i = 0; i < SlotOwners.Length; i++)
        {
            for (var t = 0; t < threadCount; t++)
            {
                Assert.NotNull(slots[t]);
                Assert.Same(slots[0][i], slots[t][i]);
            }

            var attributes = Assert.IsAssignableFrom<IPyDynamicAttributes>(slots[0][i]);
            Assert.True(attributes.TryGetMember("__qualname__", out var qualName));
            Assert.Equal(ExpectedQualNames[i], ((PyString)qualName).AsString());
        }
    }

    [Fact]
    public void ParallelEnginesAgreeOnSharedSlots()
    {
        const string code = """
            import datetime
            import functools
            import random
            import time
            def probe():
                pass
            checks = []
            checks.append(type(None).__new__ is type(None).__new__)
            checks.append(type(Ellipsis).__new__ is type(Ellipsis).__new__)
            checks.append(type(NotImplemented).__new__ is type(NotImplemented).__new__)
            checks.append(type(probe).__new__ is type(probe).__new__)
            checks.append(type(datetime.timedelta(0)).__new__ is type(datetime.timedelta(0)).__new__)
            checks.append(type(datetime.date(2020, 1, 2)).__new__ is type(datetime.date(2020, 1, 2)).__new__)
            checks.append(type(datetime.time(1, 2)).__new__ is type(datetime.time(1, 2)).__new__)
            checks.append(type(datetime.datetime(2020, 1, 2)).__new__ is type(datetime.datetime(2020, 1, 2)).__new__)
            checks.append(type(datetime.timezone.utc).__new__ is type(datetime.timezone.utc).__new__)
            checks.append(type(random.Random()).__new__ is type(random.Random()).__new__)
            checks.append(type(time.struct_time((2020, 1, 2, 3, 4, 5, 6, 7, 8))).__new__ is type(time.struct_time((2020, 1, 2, 3, 4, 5, 6, 7, 8))).__new__)
            checks.append(type(functools.partial(len)).__new__ is type(functools.partial(len)).__new__)
            names = []
            names.append(type(None).__new__.__qualname__)
            names.append(type(datetime.timedelta(0)).__new__.__qualname__)
            names.append(type(random.Random()).__new__.__qualname__)
            names.append(type(time.struct_time((2020, 1, 2, 3, 4, 5, 6, 7, 8))).__new__.__qualname__)
            names.append(type(functools.partial(len)).__new__.__qualname__)
            return checks + names
            """;
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true, true, true, true,
            "NoneType.__new__",
            "timedelta.__new__",
            "Random.__new__",
            "struct_time.__new__",
            "partial.__new__",
        };

        const int threadCount = 8;
        using var ready = new CountdownEvent(threadCount);
        using var go = new ManualResetEventSlim(false);
        var actuals = new object?[threadCount];
        var failures = new Exception?[threadCount];
        var threads = new Thread[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            var index = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    ready.Signal();
                    if (!go.Wait(TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException("engine storm starter gate timed out.");
                    }

                    var result = new LythonEngine().Run(code, new MockLythonHost());
                    Assert.True(result.Success, result.Failure?.Message);
                    actuals[index] = result.ReturnValue;
                }
                catch (Exception ex)
                {
                    failures[index] = ex;
                }
            });
            threads[t].IsBackground = true;
            threads[t].Start();
        }

        Assert.True(ready.Wait(TimeSpan.FromSeconds(30)), "engine storm workers did not reach the gate.");
        go.Set();
        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "engine storm worker did not finish.");
        }

        foreach (var failure in failures)
        {
            Assert.Null(failure);
        }

        foreach (var actual in actuals)
        {
            Assert.Equal(expected, actual);
        }
    }
}
