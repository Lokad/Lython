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
        var hostException = new NotSupportedException("binary file I/O failed for C:\\secret\\path");
        var exception = Assert.Throws<LythonRuntimeException>(() => HostOperation.Invoke<int>(
            () => throw hostException,
            "read",
            span: null));

        Assert.Equal("RuntimeError", exception.ExceptionType);
        Assert.Equal("Host read failed.", exception.Message);
        Assert.Same(hostException, exception.InnerException);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RedactsHostFailureMessagesFromGuestFailures()
    {
        var host = new Harness.MockLythonHost();
        host.FailListDir("/", "cannot list C:\\secret\\root with token=abc");

        var result = new LythonEngine().Run(
            "import os\nos.listdir('/')\n",
            host);

        Assert.False(result.Success);
        var failure = result.Failure.RequireNotNull();
        Assert.Equal("RuntimeError", failure.ExceptionType);
        Assert.Equal("Host listdir failed.", failure.Message);
        Assert.DoesNotContain("secret", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", failure.Message, StringComparison.Ordinal);
    }
}
