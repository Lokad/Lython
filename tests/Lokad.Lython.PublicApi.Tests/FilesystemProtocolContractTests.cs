using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FilesystemProtocolContractTests
{
    public static IEnumerable<object[]> Cases()
    {
        const string path = "class P:\n def __fspath__(self):\n  with open('/value') as f:return f.read()\np=P()\n";
        foreach (var (expression, expected) in new[]
        {
            ("basename(p)", "a"), ("dirname(p)", "/"), ("splitext(p)", "('/a', '')"),
            ("normpath(p)", "/a"), ("relpath(p,'/')", "a"), ("realpath(p)", "/a"),
            ("samefile(p,'/a')", "True"), ("splitroot(p)", "('', '/', 'a')"), ("expandvars(p)", "/a")
        }) yield return new object[] { path + "from os.path import *\nprint(" + expression + ")", expected + "\n" };
        yield return new object[] { path + "from pathlib import Path\nprint(Path('/a').samefile(p))", "True\n" };
        yield return new object[] { "class P:\n def __fspath__(self):\n  with open('/value') as f:f.read()\n  return '/keep'\nwith open(P()) as f:print(f.read())", "original\n" };
        yield return new object[] { path + "import os\nprint(os.fspath(p),os.fsencode(p),os.fsdecode(p))", "/a b'/a' /a\n" };
        yield return new object[] { path + "from pathlib import Path\nprint(Path(p),Path('x',p).joinpath('b'),Path('/a/b').relative_to(p))", "/a /a/b b\n" };
        yield return new object[] { path + "import os\nprint(os.path.join('x',p,'b'),os.stat(p).is_dir)", "/a/b True\n" };
        yield return new object[] { "import os\nclass Paths:\n def __iter__(self):\n  with open('/value') as f:p=f.read()\n  yield p+'/b'\n  yield p+'/c'\nprint(os.path.commonpath(Paths()))", "/a\n" };
        const string truth = "class Truth:\n def __bool__(self):\n  with open('/value') as f:return bool(f.read())\n";
        yield return new object[] { truth + "from pathlib import Path\nPath('/missing').unlink(missing_ok=Truth())\nPath('/made').mkdir(parents=Truth())\nPath('/keep').touch(exist_ok=Truth())\nprint(Path('/made').exists())", "True\n" };
        yield return new object[] { "from pathlib import Path\nfor call in [lambda:Path('/keep').touch(exist_ok=None),lambda:Path('/new').touch(mode=None)]:\n try:call()\n except Exception as e:print(type(e).__name__)\nPath('/keep').touch()\nPath('/missing').unlink(missing_ok='yes')\nprint('ok')", "RuntimeError\nTypeError\nok\n" };
        yield return new object[] { "from pathlib import Path\nclass Mode:\n def __index__(self):\n  with open('/value') as f:f.read()\n  return 420\nPath('/new').touch(mode=Mode())\nprint(Path('/new').exists())", "True\n" };
        yield return new object[] { "import os\nprint([root for root,dirs,files in os.walk('/a',topdown=None)])", "['/a/sub', '/a']\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FilesystemProtocolsAwaitHostWork(string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            await host.WriteTextUtf8Async("/value", "/a"u8.ToArray(), default);
            await host.WriteTextUtf8Async("/keep", "original"u8.ToArray(), default);
            await host.MkDirAsync("/a", default);
            await host.MkDirAsync("/a/sub", default);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            { Assert.True(result.Success, result.Failure?.Message); Assert.Equal(expected, result.StandardOutput); }
            Assert.Equal(0, budget.ReservedBytes);
            if (host is DelayedLythonHost delayed) Assert.True(delayed.CompletedAsynchronously > 0);
        }
    }

    [Theory]
    [InlineData("Path(p).rename('/keep')")]
    [InlineData("Path(p).exists()")]
    [InlineData("Path(p).with_name(p)")]
    public async Task SubstantialPathWorkRefusesBeforeEffects(string operation)
    {
        var source = "from pathlib import Path\np='" + new string('x', 150000) + "'\ntry:\n " + operation + "\nexcept Exception:pass\nprint('continued')";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(32768);
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            await host.WriteTextUtf8Async("/keep", "original"u8.ToArray(), default);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            { Assert.False(result.Success); Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason); Assert.Empty(result.StandardOutput); }
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal("original", System.Text.Encoding.UTF8.GetString((await host.ReadTextUtf8Async("/keep", default)).Span));
        }
    }
}
