using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class MockHostFileStorageTests
{
    [Fact]
    public async Task TextAndBinaryOperationsShareOneFileAndItsMetadata()
    {
        var host = new MockLythonHost();
        host.SeedFile("/source", "é");
        await FixtureFiles.WriteAsync(host, "/source", "!"u8.ToArray(), default, append: true);
        await FixtureFiles.WriteAsync(host, "/source", "😀"u8.ToArray(), default, append: true);
        var expected = Encoding.UTF8.GetBytes("é!😀");
        Assert.Equal(expected, host.ReadBytes("/source"));
        Assert.Equal(expected, (await FixtureFiles.ReadAsync(host, "/source", default)).ToArray());
        Assert.Equal(expected.Length, (await host.StatAsync(new LythonPathRequest("/source", "/"), default)).Size);
        await host.CopyAsync(new LythonPathRequest("/source", "/"), new LythonPathRequest("/copied", "/"), default);
        await FixtureFiles.WriteAsync(host, "/source", new byte[] { 0xff }, default);
        Assert.Equal("é!😀", host.ReadText("/copied"));
        Assert.Equal(new byte[] { 0xff }, (await FixtureFiles.ReadAsync(host, "/source", default)).ToArray());
        Assert.Equal(1, (await host.StatAsync(new LythonPathRequest("/source", "/"), default)).Size);
        await host.MoveAsync(new LythonPathRequest("/copied", "/"), new LythonPathRequest("/moved", "/"), default);
        Assert.False(host.Exists("/copied"));
        await host.ReplaceAsync(new LythonPathRequest("/moved", "/"), new LythonPathRequest("/source", "/"), default);
        Assert.False(host.Exists("/moved"));
        Assert.Equal(expected, host.ReadBytes("/source"));
        host.FailNextFileWrite("/source", "denied");
        var denied = await Assert.ThrowsAsync<LythonHostException>(async () =>
            await FixtureFiles.WriteAsync(host, "/source", "lost"u8.ToArray(), default, append: true));
        Assert.Equal(LythonHostFailureKind.Transport, denied.Kind);
        Assert.Equal(expected, host.ReadBytes("/source"));
        await host.RemoveFileAsync(new LythonPathRequest("/source", "/"), false, default);
        Assert.False((await host.StatAsync(new LythonPathRequest("/source", "/"), default)).Exists);
        Assert.DoesNotContain("source", await host.ListDirAsync(new LythonPathRequest("/", "/"), default));
    }
}
