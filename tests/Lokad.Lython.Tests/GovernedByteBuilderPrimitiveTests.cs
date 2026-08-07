using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class GovernedByteBuilderPrimitiveTests
{
    [Fact]
    public void AppendsMixedInputsInOrder()
    {
        var builder = new GovernedByteBuilder();

        builder.AppendAscii("ab");
        builder.Append((byte)'|');
        builder.AppendString("é");
        builder.Append((byte)'|');
        builder.Append(PyString.FromString("😀"));
        builder.Append((byte)'|');
        builder.Append("xyz"u8);

        Assert.Equal(14, builder.Length);
        Assert.Equal(PyString.FromString("ab|é|😀|xyz"), PyString.FromUtf8(builder.WrittenMemory));
        Assert.Equal("ab|é|😀|xyz", builder.ToPyStringAndRelease().AsString());
        Assert.Equal(0, builder.Length);
    }
}
