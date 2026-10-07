using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OsWalkSlicePruningTests
{
    [Theory]
    [InlineData("import os", "os.walk", true)]
    [InlineData("import os", "os.walk", false)]
    [InlineData("import os as filesystem", "filesystem.walk", true)]
    [InlineData("import os as filesystem", "filesystem.walk", false)]
    [InlineData("from os import walk as traverse", "traverse", true)]
    [InlineData("from os import walk as traverse", "traverse", false)]
    [InlineData("import os\ntraverse = os.walk", "traverse", true)]
    [InlineData("import os\ntraverse = os.walk", "traverse", false)]
    public async Task DirectSlicesPreserveMutableWalkComponents(string imports, string walk, bool topdown)
    {
        var source = imports + "\nfor root, dirs, files in " + walk +
            "('/tree', topdown=" + (topdown ? "True" : "False") + "):\n" +
            "    assert isinstance(dirs, list) and isinstance(files, list)\n" +
            "    dirs[:] = [name for name in dirs if name != 'skip']\n" +
            "    files[:] = [name for name in files if name != 'ignore.txt']\n" +
            "    print(root, dirs, files)\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var expected = topdown
            ? "/tree ['keep'] ['root.txt']\n/tree/keep [] ['a.txt']\n"
            : "/tree/keep [] ['a.txt']\n/tree/skip [] ['b.txt']\n/tree ['keep'] ['root.txt']\n";
        var syncHost = new MockLythonHost();
        Seed(syncHost);
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.StandardOutput);

        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/tree/root.txt", "root");
        delayedHost.SeedFile("/tree/ignore.txt", "ignore");
        delayedHost.SeedFile("/tree/keep/a.txt", "a");
        delayedHost.SeedFile("/tree/skip/b.txt", "b");
        var asynchronous = await script.RunAsync(delayedHost);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.StandardOutput);
        Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task SliceAssignmentThroughUnknownParametersRemainsValid()
    {
        var source = """
            def replace(value):
                value[:] = [1, 2]
            value = []
            replace(value)
            print(value)
            """;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[1, 2]\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("value = 'abc'\nvalue[:] = []")]
    [InlineData("value = b'abc'\nvalue[:] = []")]
    [InlineData("value = (1, 2)\nvalue[:] = []")]
    public void KnownImmutableSequencesKeepSliceAssignmentDiagnostics(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3158");
    }

    private static void Seed(MockLythonHost host)
    {
        host.SeedFile("/tree/root.txt", "root");
        host.SeedFile("/tree/ignore.txt", "ignore");
        host.SeedFile("/tree/keep/a.txt", "a");
        host.SeedFile("/tree/skip/b.txt", "b");
    }
}
