using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class HostOperationTests
{
    [Fact]
    public void TypedFailuresPreserveTheSafeMessageAndPrivateCauseSeparately()
    {
        var cause = new IOException("private host path");
        var hostFailure = new LythonHostException(LythonHostFailureKind.PermissionDenied, "permission denied", cause);
        var error = Assert.Throws<HostOperationException>(() => HostOperation.Invoke<int>(() => throw hostFailure,
            "read", null, new MemoryGovernor(null)));
        Assert.Equal("PermissionError", error.ExceptionType);
        Assert.Equal("permission denied", error.Message);
        Assert.Same(hostFailure, error.InnerException);
        Assert.Same(cause, error.InnerException!.InnerException);
    }

    [Fact]
    public void FirstHostRefusalStaysLatchedWhileNativeCleanupCanComplete()
    {
        var root = new LythonMemoryBudget(4096);
        using var owner = root.CreateScope();
        var governor = new MemoryGovernor(owner);
        var origin = new LythonHostException(LythonHostFailureKind.MemoryAdmission, "first refusal", requestedBytes: 8192);
        var first = Assert.Throws<LythonRuntimeException>(() => HostOperation.Invoke<int>(() => throw origin,
            "child", null, governor));
        var later = Assert.Throws<LythonRuntimeException>(() => HostOperation.Invoke<int>(
            () => throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "later refusal", requestedBytes: 16384), "later", null, governor));
        Assert.Same(first, later);
        Assert.Same(first, governor.AdmissionFailure);
        Assert.Same(origin, first.InnerException);
        Assert.Equal(8192, first.Attribution?.Requested);
        Assert.Equal(7, HostOperation.Invoke(() => 7, "native cleanup", null, governor));
        Assert.Same(first, Assert.Throws<LythonRuntimeException>(governor.ThrowIfAdmissionFailed));
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void Invoke_TranslatesTypedUnavailableCapabilitiesWithoutInspectingMessages()
    {
        var exception = Assert.Throws<LythonRuntimeException>(() => HostOperation.Invoke<int>(
            () => throw new LythonHostCapabilityUnavailableException("binary file I/O"),
            "read",
            span: null, new MemoryGovernor(null)));

        Assert.Equal("RuntimeError", exception.ExceptionType);
        Assert.Equal("host binary file I/O is not available in this host.", exception.Message);
    }

    [Fact]
    public void Invoke_TreatsOrdinaryNotSupportedFailuresAsHostErrors()
    {
        var hostException = new NotSupportedException("binary file I/O failed for C:\\secret\\path");
        var exception = Assert.Throws<HostOperationException>(() => HostOperation.Invoke<int>(
            () => throw hostException,
            "read",
            span: null, new MemoryGovernor(null)));

        Assert.IsAssignableFrom<LythonRuntimeException>(exception);
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

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
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
