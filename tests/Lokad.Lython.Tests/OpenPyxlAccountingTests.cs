using System.IO.Compression;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R02/R36: the package load session releases charges it never transfers, so
/// failed loads return the governor to baseline instead of leaking. Reflection
/// reaches the private session type; renames fail loudly here by design.
/// </summary>
public sealed class OpenPyxlAccountingTests
{
    private static string FixturePath(string caseId, string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Fixtures", "openpyxl", "cases", caseId, file);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("OpenPyXL fixture not found: " + caseId + "/" + file);
    }

    private static object NewLoadSession(ZipArchive archive, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var sessionType = package.GetNestedType("OpenPyxlLoadSession", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlLoadSession not found.");
        var ctor = sessionType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(ZipArchive), typeof(LythonRuntime.ExecutionContext), typeof(LythonSourceSpan)])
            ?? throw new InvalidOperationException("Session constructor not found.");
        return ctor.Invoke([archive, context, span]);
    }

    private static byte[]? InvokeGetPartBytes(object session, string path)
    {
        var method = session.GetType().GetMethod("GetPartBytes", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetPartBytes not found.");
        return (byte[]?)method.Invoke(session, [path]);
    }

    private static void InvokeFinish(object session, bool preserve)
    {
        var method = session.GetType().GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Finish not found.");
        method.Invoke(session, [preserve]);
    }

    [Fact]
    public void FailedLoadReleasesSessionCharges()
    {
        var bytes = File.ReadAllBytes(FixturePath("excel-basic", "input.xlsx"));
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        Assert.NotNull(InvokeGetPartBytes(session, "xl/styles.xml"));
        Assert.NotNull(InvokeGetPartBytes(session, "xl/workbook.xml"));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 0, "expected session charges");
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void FinishedSessionDisposeIsBalanced()
    {
        var bytes = File.ReadAllBytes(FixturePath("excel-basic", "input.xlsx"));
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        Assert.NotNull(InvokeGetPartBytes(session, "xl/styles.xml"));
        InvokeFinish(session, preserve: false);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void SessionDomChargeCoversConsumption()
    {
        var bytes = File.ReadAllBytes(FixturePath("excel-basic", "input.xlsx"));
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var payload = InvokeGetPartBytes(session, "xl/workbook.xml") ?? throw new InvalidOperationException("workbook part missing.");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var document = InvokeLoadOptionalXmlDocument(session, "xl/workbook.xml");
        Assert.NotNull(document);
        // The DOM outlives its parse reservation: 16 bytes per payload byte
        // (XmlDocumentBytesPerByte) stay committed while the load consumes it.
        Assert.Equal(before + 16L * payload.Length, context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static object? InvokeLoadOptionalXmlDocument(object session, string path)
    {
        var method = session.GetType().GetMethod("LoadOptionalXmlDocument", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("LoadOptionalXmlDocument not found.");
        return method.Invoke(session, [path]);
    }

}
