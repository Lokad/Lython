using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonReadContinuationTests
{
    [Theory]
    [InlineData(false, false, "success")]
    [InlineData(false, true, "success")]
    [InlineData(true, false, "success")]
    [InlineData(true, true, "success")]
    [InlineData(false, false, "cancel")]
    [InlineData(false, true, "cancel")]
    [InlineData(true, false, "cancel")]
    [InlineData(true, true, "cancel")]
    [InlineData(false, false, "refuse")]
    [InlineData(false, true, "refuse")]
    [InlineData(true, false, "refuse")]
    [InlineData(true, true, "refuse")]
    public async Task NestedCallbacksRetainUsersAndAliasesUntilCleanup(bool raw, bool pairs, string outcome)
    {
        var payload = pairs ? "[{\"a\":1},{\"b\":2}]" : "[1,2]";
        var options = pairs ? "object_pairs_hook=hook" : "parse_int=hook";
        var decode = raw
            ? $"value,end=json.JSONDecoder({options}).raw_decode(prefix+document+' tail',len(prefix))\n assert end==len(prefix)+len(document)"
            : $"value=json.loads(document,{options})";
        var source = """
            import json,time
            marker=['x'*4096]
            def hook(value):
             time.sleep(0)
             return marker
            prefix='😀 ignored '
            """ + $"\ndocument='['*17+'{payload}'+']'*17\ntry:\n {decode}\n" + """
             for unused in range(17):value=value[0]
             print(value[0] is value[1])
            except BaseException:pass
            print('later')
            return marker
            """;
        const long limit = 4 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        using var cancellation = new CancellationTokenSource();
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            ILythonMemoryReservation? fill = null;
            try
            {
                Assert.Same(timing.Entered.Task, await Task.WhenAny(timing.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.Equal(cancellation.Token, timing.SeenToken);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);

                var otherTiming = new HeldTiming(); otherTiming.Release.TrySetResult();
                var otherHost = new MockLythonHost(); otherHost.SetTiming(otherTiming);
                using (var other = await script.RunAsync(otherHost).WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.Equal("True\nlater\n", other.StandardOutput);
                    Assert.Equal(new string('x', 4096), Assert.IsType<List<object?>>(other.ReturnValue)[0]);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);

                if (outcome == "cancel") cancellation.Cancel();
                if (outcome == "refuse") Assert.True(budget.TryReserve(limit - held, out fill));
                Assert.False(pending.IsCompleted);
                Assert.Equal(held + (fill?.Bytes ?? 0), budget.ReservedBytes);
                timing.Release.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (outcome == "success")
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True\nlater\n", result.StandardOutput);
                    Assert.Equal(new string('x', 4096), Assert.IsType<List<object?>>(result.ReturnValue)[0]);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                        result.FailureAttribution?.Reason);
                    Assert.DoesNotContain("later", result.StandardOutput, StringComparison.Ordinal);
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
            using (var reused = await script.RunAsync(host))
            {
                Assert.True(reused.Success, reused.Failure?.Message);
                Assert.Equal("True\nlater\n", reused.StandardOutput);
            }
            Assert.Equal(CancellationToken.None, timing.SeenToken);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("refuse")]
    public async Task WideRecordHooksKeepPendingUsersAndRetainedResultsFunded(string outcome)
    {
        const string source = """
            import json,time
            seen=[]
            marker=['saved'*1024]
            def hook(item):
             item['alias']=marker
             seen.append(item)
             if len(seen)==1:time.sleep(0)
             return item
            try:
             first=json.loads(document,object_hook=hook)
             second=json.loads(document,object_hook=hook)
            except BaseException:pass
            assert len(first)==18 and len(second)==18
            print(first['alias'] is second['alias'])
            print('later')
            return [first,second]
            """;
        var document = "{" + string.Join(",", Enumerable.Range(0, 17).Select(i => $"\"k{i:00}\":{i}")) + "}";
        var globals = new Dictionary<string, object?> { ["document"] = document };
        const long limit = 4 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        using var cancellation = new CancellationTokenSource();
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var pending = script.RunAsync(host, globals, cancellation.Token);
            ILythonMemoryReservation? fill = null;
            try
            {
                Assert.Same(timing.Entered.Task, await Task.WhenAny(timing.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.Equal(cancellation.Token, timing.SeenToken);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                var otherTiming = new HeldTiming(); otherTiming.Release.TrySetResult();
                var otherHost = new MockLythonHost(); otherHost.SetTiming(otherTiming);
                using (var other = await script.RunAsync(otherHost, globals).WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.Equal("True\nlater\n", other.StandardOutput);
                    Assert.Equal(2, Assert.IsType<List<object?>>(other.ReturnValue).Count);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);
                if (outcome == "cancel") cancellation.Cancel();
                if (outcome == "refuse") Assert.True(budget.TryReserve(limit - held, out fill));
                Assert.False(pending.IsCompleted);
                Assert.Equal(held + (fill?.Bytes ?? 0), budget.ReservedBytes);
                timing.Release.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (outcome == "success")
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True\nlater\n", result.StandardOutput);
                    Assert.Equal(2, Assert.IsType<List<object?>>(result.ReturnValue).Count);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                        result.FailureAttribution?.Reason);
                    Assert.DoesNotContain("later", result.StandardOutput, StringComparison.Ordinal);
                    if (outcome == "refuse") Assert.True(result.FailureAttribution?.Requested > 0);
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
            using (var reused = await script.RunAsync(host, globals))
            {
                Assert.True(reused.Success, reused.Failure?.Message);
                Assert.Equal("True\nlater\n", reused.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken SeenToken;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            SeenToken = cancellationToken;
            Entered.TrySetResult();
            // Cancellation cannot abandon an outstanding host user or refund it.
            await Release.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NestedGrammarAndHookOrderMatchPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Cases()
    {
        // Isolated CPython 3.13.2 outputs; Windows CRLF normalized to capture LF.
        yield return ["pairs_postorder", "import json\nevents=[]\ndef number(s):\n events.append(('number',s)); return int(s)\ndef pairs(value):\n events.append(('pairs',[k for k,v in value])); return ('pairs',value)\ndef wrong(value):raise AssertionError('object_hook')\nvalue=json.loads('{\"k\":[{},{\"x\":1,\"x\":2}],\"k\":null}',parse_int=number,object_pairs_hook=pairs,object_hook=wrong)\nprint(events,value)\n", "[('pairs', []), ('number', '1'), ('number', '2'), ('pairs', ['x', 'x']), ('pairs', ['k', 'k'])] ('pairs', [('k', [('pairs', []), ('pairs', [('x', 1), ('x', 2)])]), ('k', None)])\n"];
        yield return ["nested_errors_prefix", "import json\nfor document in ['[{},[1,{\"x\":[]}]]','[{},[1,{\"x\":}', '[1,]', '{\"x\":1,}', '{\"x\" 2}', '[[', '{', '{\"x\":', '[1', '{\"x\":1']:\n events=[]\n def number(s):events.append(s);return int(s)\n def pairs(p):events.append([k for k,v in p]);return dict(p)\n prefix='😀 ignored '\n source=prefix+document+' tail'\n try:\n  value,end=json.JSONDecoder(parse_int=number,object_pairs_hook=pairs).raw_decode(source,len(prefix))\n  print(value,end,events)\n except json.JSONDecodeError as e:print(e.pos,e.doc is source,events)\n", "[{}, [1, {'x': []}]] 27 [[], '1', ['x']]\n22 True [[], '1']\n12 True ['1']\n16 True ['1']\n15 True []\n13 True []\n12 True []\n16 True []\n13 True ['1']\n17 True ['1']\n"];
        yield return ["deep_prefix", "import json\nfor depth in [0,1,4,17,64]:\n document='['*depth+'42'+']'*depth\n prefix='😀 ignored '\n value,end=json.JSONDecoder().raw_decode(prefix+document+' tail',len(prefix))\n for i in range(depth):value=value[0]\n print(depth,value,end==len(prefix)+len(document))\n", "0 42 True\n1 42 True\n4 42 True\n17 42 True\n64 42 True\n"];
        yield return ["hook_aliases", "import json\nevents=[]\nmarker=['saved']\ndef object_hook(value):\n events.append(list(value.keys()))\n if 'x' in value:return marker\n return value\nvalue=json.loads('{\"a\":[{\"x\":1},{\"x\":2}],\"b\":{}}',object_hook=object_hook)\nprint(events,value['a'][0] is value['a'][1],value)\n", "[['x'], ['x'], [], ['a', 'b']] True {'a': [['saved'], ['saved']], 'b': {}}\n"];
        yield return ["lenient_controls", "import json\nsource='{\"a'+chr(1)+'\":[[],{\"b\":\"x'+chr(0)+'y\"}]}'\nprint(json.loads(source,strict=False))\ntry:json.loads(source)\nexcept json.JSONDecodeError as e:print(e.pos,e.doc is source)\n", "{'a\\x01': [[], {'b': 'x\\x00y'}]}\n3 True\n"];
        yield return ["numeric_prefix_callbacks", "import json\nfor document in ['01','-01','1.','2e','-3e+','1e-2','NaN','Infinity','-Infinity']:\n events=[]\n def number(s):events.append(('int',s));return int(s)\n def floating(s):events.append(('float',s));return float(s)\n def constant(s):events.append(('constant',s));return s\n value,end=json.JSONDecoder(parse_int=number,parse_float=floating,parse_constant=constant).raw_decode(document)\n print(value,end,events)\n", "0 1 [('int', '0')]\n0 2 [('int', '-0')]\n1 1 [('int', '1')]\n2 1 [('int', '2')]\n-3 2 [('int', '-3')]\n0.01 4 [('float', '1e-2')]\nNaN 3 [('constant', 'NaN')]\nInfinity 8 [('constant', 'Infinity')]\n-Infinity 9 [('constant', '-Infinity')]\n"];
    }
}
