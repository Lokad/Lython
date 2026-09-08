namespace Lokad.Lython.PublicApi.Tests;

public sealed class PythonVersionTests
{
    [Fact]
    public void VersionFamily_IsPinned()
    {
        Assert.Equal("Python 3.13", LythonPythonVersion.VersionFamily);
        Assert.Equal("3.13.0", LythonPythonVersion.Version);
        Assert.Equal(3, LythonPythonVersion.Major);
        Assert.Equal(13, LythonPythonVersion.Minor);
        Assert.Equal("lython-3.13", LythonPythonVersion.CacheTag);
    }
}

