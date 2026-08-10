using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class HostOperationTests
{
    [Fact]
    public void Invoke_TranslatesTypedUnavailableCapabilitiesWithoutInspectingMessages()
    {
        var exception = Assert.Throws<LythonRuntimeException>(() => HostOperation.Invoke<int>(
            () => throw new LythonHostCapabilityUnavailableException("binary file I/O"),
            "read",
            span: null));

        Assert.Equal("RuntimeError", exception.ExceptionType);
        Assert.Equal("host binary file I/O is not available in this host.", exception.Message);
    }

    [Fact]
    public void Invoke_TreatsOrdinaryNotSupportedFailuresAsHostErrors()
    {
        var exception = Assert.Throws<LythonRuntimeException>(() => HostOperation.Invoke<int>(
            () => throw new NotSupportedException("binary file I/O failed for another reason"),
            "read",
            span: null));

        Assert.Equal("RuntimeError", exception.ExceptionType);
        Assert.Equal("Host read failed: binary file I/O failed for another reason", exception.Message);
    }
}
