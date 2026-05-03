using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class Utf8ValueBuilderPrimitiveTests
{
    [Fact]
    public void AppendsMixedInputsInOrder()
    {
        var builder = new Utf8ValueBuilder();

        builder.AppendAscii("ab");
        builder.Append((byte)'|');
        builder.AppendString("é");
        builder.Append((byte)'|');
        builder.Append(PyString.FromString("😀"));
        builder.Append((byte)'|');
        builder.Append("xyz"u8);

        Assert.Equal(14, builder.Length);
        Assert.Equal("ab|é|😀|xyz", builder.ToPyString().AsString());
        Assert.Equal(PyString.FromString("ab|é|😀|xyz"), PyString.FromUtf8(builder.ToArray()));
    }
}
