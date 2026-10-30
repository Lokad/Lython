using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.BinaryFileHandle;

namespace Lokad.Lython.Tests;

public sealed class BinaryFileWriteCallBoundaryTests
{
    // Complete records frozen on92f98863 before runtime edits.
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var exposed in new[] { false, true })
        foreach (var shape in new[] { "small", "empty", "wide", "bad", "closed", "readonly", "rebind" })
            yield return [asynchronous, exposed, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CallsPreserveEveryCheckpointAndPublishedPrefix(bool asynchronous, bool exposed, string shape)
    {
        var source = exposed ? "method=file.write\nreturn method(argument())" : "return file.write(argument())";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        string outcome;
        string final;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var fixture = Prepare(owner, shape);
            try
            {
                var result = await Run(code, fixture.Context, asynchronous);
                Assert.DoesNotContain(shape, new[] { "bad", "closed", "readonly" });
                Assert.Equal(new BigInteger(fixture.Payload.Length), Assert.IsType<BigInteger>(result));
                outcome = "written:" + result;
            }
            catch (LythonRuntimeException error)
            {
                Assert.Contains(shape, new[] { "bad", "closed", "readonly" });
                Assert.Equal(shape == "bad" ? "TypeError" : shape == "closed" ? "ValueError" : "UnsupportedOperation", error.ExceptionType);
                outcome = error.ExceptionType + ":" + error.Message;
            }
            finally { Close(fixture); }
            Assert.Equal(1, fixture.Argument.Calls);
            final = DescribeOutput(fixture, shape, complete: true);
            AssertUnwound(fixture.Context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedWritePreservesInputAndPreviouslyPublishedBytes(bool asynchronous, bool exposed)
    {
        const long limit = 4 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var fixture = Prepare(owner, "wide");
            fixture.Host.SeedBytes("/out", [42]);
            var input = Assert.IsType<PyBytes>(fixture.Argument.Value);
            var code = Compile(exposed);
            // Admit private frame metadata, leaving less than the first write
            // buffer so refusal still exercises publication, not frame entry.
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 1024, out var padding));
            try
            {
                using (padding)
                {
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, fixture.Context, asynchronous));
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                    Assert.Same(fixture.Context.MemoryGovernor.AdmissionFailure, error);
                    Assert.True(fixture.Context.MemoryGovernor.LastDeniedReservationBytes >= 4096);
                    Assert.Equal(1, fixture.Argument.Calls);
                    Assert.Equal(fixture.Payload, input.Bytes.ToArray());
                    Assert.Equal(new byte[] { 42 }, fixture.Host.ReadBytes("/out"));
                    AssertUnwound(fixture.Context);
                }
            }
            finally { Close(fixture); }
            // Releasing padding after terminal refusal cannot revive publication.
            Assert.Equal(new byte[] { 42 }, fixture.Host.ReadBytes("/out"));
            Assert.True(budget.ReservedBytes >= input.Length);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SuspendedArgumentKeepsTheEvaluatedReceiverFunded(bool exposed, bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var fixture = Prepare(owner, "small");
            var other = await Handle.OpenAsync("/other", LythonRuntime.TextFileOperation.Write, fixture.Context, Span, false);
            fixture.Argument.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var funded = budget.ReservedBytes;
            var pending = Run(Compile(exposed), fixture.Context, true).AsTask();
            try
            {
                Assert.False(pending.IsCompleted);
                Assert.Equal(1, fixture.Argument.Calls);
                Assert.True(budget.ReservedBytes >= funded);
                fixture.Context.Variables["file"] = other;
                if (cancel)
                {
                    fixture.Argument.Release.SetCanceled();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                }
                else
                {
                    fixture.Argument.Release.SetResult();
                    Assert.Equal(new BigInteger(fixture.Payload.Length), await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                }
                AssertUnwound(fixture.Context);
            }
            finally
            {
                fixture.Argument.Release.TrySetCanceled();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
                finally
                {
                    try { Close(fixture); }
                    finally { other.Exit(); }
                }
            }
            Assert.Equal(cancel ? Array.Empty<byte>() : fixture.Payload, fixture.Host.ReadBytes("/out"));
            Assert.Empty(fixture.Host.ReadBytes("/other"));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(bool exposed)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(
            exposed ? "method=file.write\nreturn method(argument())" : "return file.write(argument())",regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private static Fixture Prepare(ILythonMemoryBudget owner, string shape)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/input", [7, 8, 9]);
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var handle = Handle.OpenAsync(shape == "readonly" ? "/input" : "/out",
            shape == "readonly" ? LythonRuntime.TextFileOperation.Read : LythonRuntime.TextFileOperation.Write, context, Span, false).GetAwaiter().GetResult();
        var other = shape == "rebind" ? Handle.OpenAsync("/other", LythonRuntime.TextFileOperation.Write, context, Span, false).GetAwaiter().GetResult() : null;
        if (shape == "closed") handle.Exit();
        var payload = shape == "empty" ? Array.Empty<byte>() : shape == "wide"
            ? Enumerable.Range(0, 4097).Select(i => (byte)(i % 251)).ToArray() : new byte[] { 97, 0, 10 };
        var argument = new Argument(shape is "bad" or "closed" or "readonly" ? PyNone.Instance
            : new PyBytes(payload, context.MemoryGovernor, Span), other);
        context.Variables["file"] = handle;
        context.Variables["argument"] = argument;
        return new(context, host, handle, other, argument, payload);
    }

    private static void Close(Fixture fixture)
    {
        try { fixture.Handle.Exit(); }
        finally { fixture.Other?.Exit(); }
    }

    private static string DescribeOutput(Fixture fixture, string shape, bool complete)
    {
        var bytes = fixture.Host.ReadBytes(shape == "readonly" ? "/input" : "/out");
        if (shape == "readonly") Assert.Equal(new byte[] { 7, 8, 9 }, bytes);
        else
        {
            Assert.Equal(fixture.Payload.Take(bytes.Length), bytes);
            if (complete) Assert.Equal(shape is "bad" or "closed" ? 0 : fixture.Payload.Length, bytes.Length);
        }
        if (fixture.Other is not null) Assert.Empty(fixture.Host.ReadBytes("/other"));
        return bytes.Length + ":" + Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed record Fixture(Context Context, MockLythonHost Host, Handle Handle, Handle? Other, Argument Argument, byte[] Payload);

    private sealed class Argument(object value, Handle? other) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal TaskCompletionSource? Release;
        internal object Value => value;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (other is not null) context.Variables["file"] = other;
            return value;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var result = Invoke(arguments, span, context);
            if (Release is not null) await Release.Task.ConfigureAwait(false);
            return result;
        }
    }
}
