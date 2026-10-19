using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorExpressionBoundaryTests
{
    // Independent CPython 3.13.2 expectations.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "partial-consumption", "g=(i*i for i in range(3))\nprint(next(g),list(g))", "0 [1, 4]\n" },
        { "function-late-capture", "def make():\n offset=10\n g=(i+offset for i in range(3))\n offset=20\n return g\nprint(list(make()))", "[20, 21, 22]\n" },
        { "module-late-binding", "offset=10\ng=(i+offset for i in range(3))\noffset=20\nprint(list(g))", "[20, 21, 22]\n" },
        { "outer-iter-acquired-once", "class Source:\n def __iter__(self):\n  print('iter')\n  return iter((1,2))\nsource=Source()\ng=(i*2 for i in source)\nprint('created')\nsource=[]\nprint(list(g))", "iter\ncreated\n[2, 4]\n" },
        { "outer-generator-does-not-advance", "def source():\n print('source')\n yield 7\ng=(i for i in source())\nprint('created')\nprint(next(g))", "created\nsource\n7\n" },
        { "nested-deferred-clauses", "def inner(value):\n print('inner',value)\n return range(value+1)\ng=(j for i in (1,2) for j in inner(i) if j%2==0)\nprint('created')\nprint(next(g))\nprint(list(g))", "created\ninner 1\n0\ninner 2\n[0, 2]\n" },
        { "iteration-name-isolation", "i='outer'\ng=(i for i in range(3))\nprint(next(g),i,list(g))", "0 outer [1, 2]\n" },
        { "late-lambda-cells", "g=(lambda:i for i in range(3))\nfs=list(g)\nprint([f() for f in fs])", "[2, 2, 2]\n" },
        { "produced-walrus", "total=-1\ng=((total:=i) for i in range(3))\nprint(total)\nprint(next(g),total)\nprint(list(g),total)", "-1\n0 0\n[1, 2] 2\n" },
        { "filtered-walrus", "total=0\ng=(i for i in range(3) if (total:=total+1)<0)\nprint(list(g),total)", "[] 3\n" },
        { "stopiteration-body", "def stop():raise StopIteration('done')\ng=(stop() for i in (0,))\ntry:next(g)\nexcept RuntimeError:print('translated')\nexcept StopIteration:print('raw')", "translated\n" },
        { "class-namespace-skipped", "offset=20\nclass Box:\n offset=10\n g=(i+offset for i in range(3))\nprint(list(Box.g))", "[20, 21, 22]\n" },
        { "filter-observes-store", "value=-1\ndef read():return value\ng=(i for i in range(3) if ((value:=i),read())[1]==i)\nprint(list(g),value)", "[0, 1, 2] 2\n" },
        { "item-observes-store", "value=-1\ndef read():return value\ng=((value:=i,read()) for i in range(3))\nprint(list(g),value)", "[(0, 0), (1, 1), (2, 2)] 2\n" },
        { "filter-raises-after-store", "value=-1\ndef bad():raise ValueError('later')\ng=(i for i in (7,) if ((value:=i),bad())[1])\ntry:list(g)\nexcept ValueError:print(value)\nprint(next(g,'end'))", "7\nend\n" },
        { "item-raises-after-store", "value=-1\ng=(((value:=i),1//0) for i in (7,))\ntry:list(g)\nexcept ZeroDivisionError:print(value)\nprint(next(g,'end'))", "7\nend\n" },
        { "external-rebinding", "total=0\ng=((total:=total+1) for i in range(3))\nprint(next(g),total)\ntotal=10\nprint(next(g),total)\nprint(list(g),total)", "1 1\n11 11\n[12] 12\n" },
        { "generator-under-eager-scope", "total=0\nrows=[list(i for i in range(2) if (total:=total+1)<0) for j in range(2)]\nprint(rows,total)", "[[], []] 4\n" },
        { "eager-under-generator-scope", "total=-1\ng=([(total:=j) for j in range(2)] for i in range(2))\nprint(list(g),total)", "[[0, 1], [0, 1]] 1\n" },
        { "nested-generator-scope", "total=0\ng=((i for i in range(2) if (total:=total+1)<0) for j in range(2))\nprint([list(inner) for inner in g],total)", "[[], []] 4\n" },
        { "function-local-store", "def run():\n total=0\n g=(i for i in range(3) if (total:=total+1)<0)\n return list(g),total\nprint(run())", "([], 3)\n" },
        { "global-directive-store", "total=0\ndef run():\n global total\n g=(i for i in range(3) if (total:=total+1)<0)\n return list(g)\nprint(run(),total)", "[] 3\n" },
        { "nonlocal-directive-store", "def outer():\n total=0\n def run():\n  nonlocal total\n  g=(i for i in range(3) if (total:=total+1)<0)\n  return list(g)\n rows=run()\n return rows,total\nprint(outer())", "([], 3)\n" },
        { "builtin-name-store", "g=((len:=i) for i in range(3))\nprint(list(g),len)", "[0, 1, 2] 2\n" },
        { "first-binding-without-yield", "g=(i for i in (7,) if (count:=i)<0)\nprint(list(g),count)", "[] 7\n" },
        { "lambda-walrus-is-local", "total=0\ng=((lambda:(total:=7))() for i in range(2))\nprint(list(g),total)", "[7, 7] 0\n" },
        { "global-walrus-not-in-locals", "total=-1\ng=((total:=i,'total' in locals()) for i in range(2))\nprint(list(g),total)", "[(0, False), (1, False)] 1\n" },
        { "nonlocal-walrus-in-locals", "def run():\n total=-1\n g=((total:=i,locals().get('total','missing')) for i in range(2))\n return list(g),total\nprint(run())", "([(0, 0), (1, 1)], 1)\n" },
        { "nonlocal-snapshot-after-rebinding", "def run():\n total=0\n g=(locals().get('total','missing') for i in range(2) if i>0 or (total:=1))\n print(next(g))\n total=10\n print(next(g),total)\nrun()", "1\n10 10\n" },
        { "body-stop-cause", "def stop():raise StopIteration('done')\ng=(stop() for i in (0,))\ntry:next(g,'fallback')\nexcept RuntimeError as e:print(str(e),str(e.__cause__),e.__cause__.args,e.__suppress_context__)\nprint(next(g,'end'))", "generator raised StopIteration done ('done',) True\nend\n" },
        { "filter-stop", "def stop():raise StopIteration('filter')\ng=(i for i in (0,) if stop())\ntry:next(g)\nexcept RuntimeError as e:print(str(e.__cause__))\nprint(next(g,'end'))", "filter\nend\n" },
        { "inner-iterable-stop", "def stop():raise StopIteration('inner')\ng=(j for i in (0,) for j in stop())\ntry:next(g)\nexcept RuntimeError as e:print(str(e.__cause__))\nprint(next(g,'end'))", "inner\nend\n" },
        { "truth-stop", "class Truth:\n def __bool__(self):raise StopIteration('truth')\ng=(i for i in (0,) if Truth())\ntry:next(g)\nexcept RuntimeError as e:print(str(e.__cause__))\nprint(next(g,'end'))", "truth\nend\n" },
        { "outer-creation-stop-is-raw", "class Source:\n def __iter__(self):raise StopIteration('creation')\ntry:g=(i for i in Source())\nexcept StopIteration as e:print(str(e))", "creation\n" },
        { "normal-exhaustion", "g=(i for i in ())\nprint(next(g,'end'),next(g,'end'))", "end end\n" },
        { "stop-after-walrus", "value=-1\ndef stop():raise StopIteration('later')\ng=((value:=i,stop()) for i in (7,))\ntry:next(g)\nexcept RuntimeError as e:print(value,str(e.__cause__))\nprint(next(g,'end'))", "7 later\nend\n" },
        { "nonlocal-dir", "def run():\n total=-1\n g=((total:=i,'total' in dir()) for i in range(2))\n return list(g),total\nprint(run())", "([(0, True), (1, True)], 1)\n" },
        { "global-dir", "total=-1\ng=((total:=i,'total' in dir()) for i in range(2))\nprint(list(g),total)", "[(0, False), (1, False)] 1\n" },
        { "translated-cause-context", "original=StopIteration('done')\ndef stop():raise original\ndef values():yield stop()\nfor factory in (lambda:(stop() for i in (0,)),values):\n try:next(factory())\n except RuntimeError as e:print(e.__cause__ is original,e.__context__ is original,e.__suppress_context__)", "True True True\nTrue True True\n" },
        { "suspended-owner-walrus", "def outer():\n total=0\n g=(i for i in range(3) if (total:=total+1)<0)\n yield g\n yield total\nsource=outer()\ng=next(source)\nprint(list(g),next(source))", "[] 3\n" },
        { "suspended-owner-nonlocal-helper", "def outer():\n total=0\n def change():\n  nonlocal total\n  total=9\n yield change\n yield str(total)\nsource=outer()\nchange=next(source)\nchange()\nprint(next(source))", "9\n" },
        { "escaped-owner-observer", "def make():\n total=0\n g=(i for i in range(3) if (total:=total+1)<0)\n observe=lambda:total\n return g,observe\ng,observe=make()\nprint(list(g),observe())", "[] 3\n" },
        { "outer-identity", "outer=iter([1,2])\ng=(locals()['.0'] is outer for i in outer)\nprint(list(g))", "[True, True]\n" },
        { "unstarted-close", "events=[]\nclass Source:\n def __iter__(self):events.append('iter');return self\n def __next__(self):events.append('next');return 1\ng=(i for i in Source())\ng.close()\nprint(events,next(g,'end'))", "['iter'] end\n" },
        { "send-discards-resumed-value", "g=(i*2 for i in range(3))\nprint(next(g),g.send(99),g.send(None),next(g,'end'))", "0 2 4 end\n" },
        { "throw-before-start", "events=[]\ndef source():\n events.append('started')\n yield 1\ng=(i for i in source())\ntry:g.throw(ValueError('before'))\nexcept ValueError as error:print(str(error))\nprint(events,next(g,'end'))", "before\n[] end\n" },
        { "close-preserves-outer-alias", "events=[]\ndef source():\n try:\n  yield 1\n  yield 2\n finally:events.append('closed')\nouter=source()\ng=(i for i in outer)\nprint(next(g))\ng.close()\nprint(events,next(outer))\nouter.close()\nprint(events)", "1\n[] 2\n['closed']\n" },
        { "closed-before-start-send", "def values():yield 7\nfor make in (values,lambda:(i for i in (7,))):\n g=make()\n g.close()\n try:g.send(99)\n except StopIteration:print('closed')", "closed\nclosed\n" },
        { "invalid-first-send-keeps-generator", "def values():yield 7\nfor make in (values,lambda:(i for i in (7,))):\n g=make()\n try:g.send(99)\n except TypeError:print('new')\n print(next(g),next(g,'end'))", "new\n7 end\nnew\n7 end\n" },
        { "reentrant-next-terminal", "def recurse():return next(g)\ng=(recurse() for i in (0,))\ntry:next(g)\nexcept ValueError as error:print(str(error))\nprint(next(g,'end'))", "generator already executing\nend\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task StoresAndEscapingErrorsFollowPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Code + " " + d.Message)));
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
}
