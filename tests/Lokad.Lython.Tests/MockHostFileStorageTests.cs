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
        await host.AppendBytesAsync("/source", "!"u8.ToArray(), default);
        await host.AppendTextUtf8Async("/source", "😀"u8.ToArray(), default);
        var expected = Encoding.UTF8.GetBytes("é!😀");
        Assert.Equal(expected, host.ReadBytes("/source"));
        Assert.Equal(expected, (await host.ReadTextUtf8Async("/source", default)).ToArray());
        Assert.Equal(expected.Length, (await host.StatAsync("/source", default)).Size);
        await host.CopyAsync("/source", "/copied", default);
        await host.WriteBytesAsync("/source", new byte[] { 0xff }, default);
        Assert.Equal("é!😀", host.ReadText("/copied"));
        Assert.Equal(new byte[] { 0xff }, (await host.ReadTextUtf8Async("/source", default)).ToArray());
        Assert.Equal(1, (await host.StatAsync("/source", default)).Size);
        await host.MoveAsync("/copied", "/moved", default);
        Assert.False(host.Exists("/copied"));
        await host.ReplaceAsync("/moved", "/source", default);
        Assert.False(host.Exists("/moved"));
        Assert.Equal(expected, host.ReadBytes("/source"));
        host.FailNextFileWrite("/source", "denied");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await host.AppendBytesAsync("/source", "lost"u8.ToArray(), default));
        Assert.Equal(expected, host.ReadBytes("/source"));
        await host.RemoveFileAsync("/source", false, default);
        Assert.False((await host.StatAsync("/source", default)).Exists);
        Assert.DoesNotContain("source", await host.ListDirAsync("/", default));
    }
}
