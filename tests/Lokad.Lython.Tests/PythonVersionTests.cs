namespace Lokad.Lython.Tests;

public sealed class PythonVersionTests
{
    [Fact]
    public void VersionFamily_IsPinned()
    {
        Assert.Equal("Python 3.13", LythonPythonVersion.VersionFamily);
    }
}
