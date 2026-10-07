using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class HtmlEntityInventoryTests
{
    [Fact]
    public void EmbeddedTableMatchesTheCompletePython313Inventory()
    {
        using var stream = typeof(HtmlEntityData).Assembly.GetManifestResourceStream(HtmlEntityData.ResourceName)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        Assert.Equal(36455, bytes.Length);
        Assert.Equal("0155D7C70A78DB0554D8482F86017AD4C2BA9AFEC237399819D1F560DEA33218", Convert.ToHexString(SHA256.HashData(bytes)));
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(2231, HtmlEntityData.NamedCount);
        foreach (var item in document.RootElement.EnumerateObject())
        {
            Assert.InRange(item.Name.Length, 1, 32);
            Assert.All(item.Name, ch => Assert.True(char.IsAsciiLetterOrDigit(ch) || ch == ';'));
            Assert.True(HtmlEntityData.TryGetNamed(item.Name.AsSpan(), out var value));
            Assert.Equal(item.Value.GetString(), value.Text.AsString());
            Assert.InRange(value.Scalars, 1, 2);
        }
    }
}
