using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StarredAnnotationTests
{
    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "refuse")]
    [InlineData(true, "refuse")]
    public async Task PendingIteratorKeepsOwnershipAndUnwindsBeforeAllowanceReuse(bool classBody, string outcome)
    {
        const long limit = 4 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var cancellation = new CancellationTokenSource();
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        var definition = classBody ? "class Holder:\n def helper(*args: *Pack()): pass\ntarget=Holder.helper\n"
            : "def helper(*args: *Pack()): pass\ntarget=helper\n";
        var source = """
            import time
            marker=[7]
            class Pack:
                def __iter__(self): return self
                def __next__(self):
                    time.sleep(0)
                    if hasattr(self,'done'): raise StopIteration
                    self.done=True
                    return marker
            """ + "\ntry:\n" + string.Join("\n", definition.TrimEnd().Split('\n').Select(line => " " + line)) + "\nexcept BaseException: pass\nprint('later')\nreturn target.__annotations__['args']\n";
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            ILythonMemoryReservation? fill = null;
            try
            {
                await timing.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.Equal(cancellation.Token, timing.SeenToken);
                var suspendedBalance = budget.ReservedBytes;
                Assert.True(suspendedBalance > baseline);
                if (outcome == "cancel") cancellation.Cancel();
                if (outcome == "refuse") Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out fill));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes >= suspendedBalance);
                timing.Release.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (outcome == "success")
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("later\n", result.StandardOutput);
                    Assert.Equal(new System.Numerics.BigInteger(7), Assert.Single(Assert.IsAssignableFrom<IEnumerable<object?>>(result.ReturnValue)));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                        result.FailureAttribution?.Reason);
                    Assert.Empty(result.StandardOutput);
                }
            }
            finally
            {
                timing.Release.TrySetResult();
                cancellation.Cancel();
                using var settled = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                fill?.Dispose();
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var reused = await script.RunAsync(host)) Assert.True(reused.Success, reused.Failure?.Message);
            Assert.Equal(CancellationToken.None, timing.SeenToken);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken SeenToken;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            SeenToken = cancellationToken;
            Entered.TrySetResult();
            // Deliberately settle only when released: cancellation cannot abandon
            // the operation or refund its borrowed storage while it still runs.
            await Release.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    // Independent isolated CPython 3.13 outputs, including ordinary iterable semantics.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["def f(*args: *tuple[int,str]): pass\nprint(f.__annotations__)\n", "{'args': *tuple[int, str]}\n"];
        yield return ["from typing import TypeVarTuple\nTs=TypeVarTuple('Ts')\ndef f(*args: *Ts): pass\nprint(f.__annotations__)\n", "{'args': typing.Unpack[Ts]}\n"];
        yield return ["def f(*args: *[int]): pass\nprint(f.__annotations__)\n", "{'args': <class 'int'>}\n"];
        yield return ["def f(*args: *(42,)): pass\nprint(f.__annotations__)\n", "{'args': 42}\n"];
        yield return ["try:\n def f(*args: *()): pass\nexcept Exception as e: print(type(e).__name__,str(e))\n", "ValueError not enough values to unpack (expected 1, got 0)\n"];
        yield return ["try:\n def f(*args: *(1,2)): pass\nexcept Exception as e: print(type(e).__name__,str(e))\n", "ValueError too many values to unpack (expected 1)\n"];
        yield return ["try:\n def f(*args: *42): pass\nexcept Exception as e: print(type(e).__name__,str(e))\n", "TypeError cannot unpack non-iterable int object\n"];
        yield return ["class Ann:\n def __iter__(self):\n  print('iter')\n  yield 'ok'\ndef f(*args: *Ann()): pass\nprint(f.__annotations__)\n", "iter\n{'args': 'ok'}\n"];
        yield return ["def outer():\n marker=['kept']\n class Holder:\n  def helper(*args: *[marker]): return marker\n return Holder\nH=outer()\nprint(H.helper.__annotations__['args'] is H.helper(), H.helper())\n", "True ['kept']\n"];
        yield return ["def values():\n yield 'before'\n def helper(*args: *('saved',)): pass\n yield helper.__annotations__['args']\nprint(list(values()))\n", "['before', 'saved']\n"];
        yield return ["from __future__ import annotations\ndef value(): raise AssertionError('not evaluated')\ndef helper(*args: *value()): pass\nprint(helper.__annotations__)\n", "{'args': '*value()'}\n"];
        yield return ["events=[]\ndef build():\n events.append('value')\n return [42]\ndef default():\n events.append('default')\n return 0\ndef decorate(f):\n events.append('decorate')\n return f\n@decorate\ndef helper(x=default(), *args: *build()): pass\nprint(events, helper.__annotations__)\n", "['default', 'value', 'decorate'] {'args': 42}\n"];
        yield return ["def packed():\n print('first')\n yield 1\n print('second')\n yield 2\n print('third')\n yield 3\ntry:\n def helper(*args: *packed()): pass\nexcept Exception as e: print(type(e).__name__,str(e))\n", "first\nsecond\nValueError too many values to unpack (expected 1)\n"];
        yield return ["class Bad:\n def __iter__(self): raise MemoryError('guest')\ntry:\n def helper(*args: *Bad()): pass\nexcept MemoryError as e: print('caught',str(e))\n", "caught guest\n"];
        yield return ["class Pack:\n def __iter__(self): return self\n def __next__(self):\n  if hasattr(self,'done'): raise StopIteration\n  self.done=True\n  return 7\n def __length_hint__(self): raise AssertionError('hint')\ndef helper(*args: *Pack()): pass\nprint(helper.__annotations__)\n", "{'args': 7}\n"];
        yield return ["from typing import TypeVar\nT=TypeVar('T')\ntry:\n def helper(*args: *T): pass\nexcept Exception as e: print(type(e).__name__,str(e))\n", "TypeError cannot unpack non-iterable typing.TypeVar object\n"];
        yield return ["def outer():\n def helper(*args: *(yield 'annotation')): pass\n yield helper.__annotations__\ng=outer()\nprint(next(g))\nprint(g.send([42]))\n", "annotation\n{'args': 42}\n"];
        yield return ["def helper(*args: *(None,)): pass\nprint(helper.__annotations__)\n", "{'args': None}\n"];
        yield return ["def helper(*args: *'😀'): pass\nprint(helper.__annotations__)\n", "{'args': '😀'}\n"];
        yield return ["def helper(*args: *b'\\x00'): pass\nprint(helper.__annotations__)\n", "{'args': 0}\n"];
        yield return ["def helper(*args: *list[int]): pass\nprint(helper.__annotations__)\n", "{'args': *list[int]}\n"];
        yield return ["class Pack:\n def __getitem__(self,index):\n  print(index)\n  if index: raise IndexError\n  return 99\ndef helper(*args: *Pack()): pass\nprint(helper.__annotations__)\n", "0\n1\n{'args': 99}\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MetadataErrorsAndEffectsMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
