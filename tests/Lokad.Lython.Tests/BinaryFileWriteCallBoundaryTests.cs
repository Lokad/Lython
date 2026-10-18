using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.BinaryFileHandle;

namespace Lokad.Lython.Tests;

public sealed class BinaryFileWriteCallBoundaryTests(ITestOutputHelper output)
{
    // Complete records frozen on92f98863 before runtime edits.
    private const string FrozenGuards = """{"True:True:bad":"7408C3B5A8EC56143175CC1C33F44A95883C8D379A7348D463602A0E1BD48515","True:False:small":"030AD2976BDBC335009700AFAC2D9A3160DF1F74A1D2D58ACD3C402D9DDDDDD4","False:True:closed":"180995FFD313FD51689ADC4AE44E7088309F24DBD55C06B9487791393269C4EA","True:False:wide":"EE3B3D561B36728CD79F0254AE12450664CFE2CA44C726B21BB23B473B4FFE5B","True:True:empty":"4478B13C9C3DEE72682C612E80E353CDD2CD4E18ECB56E064D51B15AB024FBE2","False:True:readonly":"1AD6925CD05331D57A462DDE802F7A1A045B05C44231B985CA68B52CAFAC7289","False:True:small":"5E6F8A6519C889DBE3CF32CD1F585F574F768C2042621F15372A56A2D723B842","False:True:bad":"9207B9F2CAE4CD9127D582E450BBB655A5DFFE52C2A7AD841C10F92D1543304A","False:True:rebind":"EC16B4B86FAC794536EBBCFDFB5E2E2935C803DFA1C533C0E64AE12EE16A07B9","False:False:bad":"9945418D3183E4B9DAA285B5C9B965876B026574AD4D54225E1F747BAEF79801","False:False:rebind":"9C2A9DBC6ED4CCC3893F3036D93551FFC2B0540E2913F145E0770BB8C1D4E040","False:False:readonly":"0C32BEEA6A526BAFC4A29898DBD1DF605B3E6430DB367A43CBE74494F794DC1E","False:False:wide":"6D95F29C2215436B31CBA7FCE88341B8A425683135FB43781CD39BF1DC4DD2F3","True:False:readonly":"46D56F73449A9FC2E9BDEFEC6D13EF9F37F83871EDAFC7A0DC92CE497C1476C5","True:False:rebind":"1195B74D7B4B00B8D4BE784D6FECAD2FD650F7E51912D9FDD3A2E163B4D4113E","True:True:wide":"C60E198E96B7D3087A0527F859FFB6CF24CD32CA31A7DD22D17AF5411BAAB4FA","False:False:closed":"7503D9F5A9044FB81B5BA08000BCA9B87A2C57C2D36C4664101EAAC94D55F834","True:True:rebind":"28F48E7D4D20192F9A3C8A0339807CE15C444823D05B10BC8C83AE338B3A1167","False:False:small":"71CD40B81C3AAF9ED69763E2880317B326DF32C2E7AA732929886FA48D04AB10","True:False:empty":"5446CA2F3BD3535CFC1C344BFA87731D1D7639C79430176C4559F2F9404BC434","True:True:closed":"DFDD580F14837A96258E8670DE816EEB96448413AD0E7F96E93D8862F45BBEAA","True:True:readonly":"87A0470963EF5BEE3B4C5ABB56BAF0699CBBB271245F9FBA0C82A73312C4C2C3","False:True:empty":"7755D9CBC0A3F582EE7C6D95FDD033D2363CA43DC64567928FFB3FEAB1E9D479","False:False:empty":"FDD20F673FFD45F642AC3C7677E6E9B9E9C8E3FC406B50DC62CB5A4C0AA5462F","True:True:small":"A54FB795D7C574F2FF9B4F3D369785E68B758C328C2269AD483B3BFDFF489D07","True:False:bad":"873FF4B5784BFAC9EBDC3FE384792DBD616FED37B7FF883D7700B62A84DCAB73","True:False:closed":"8C92D678D9999A5D4B416D5243020D1965B183F744AB25154ACBA64ED558F257","False:True:wide":"D73FC2EE13A20192F97D88245337C622906E110331BE181688241B6A1006BC05"}""";
    private const int MaximumSteps = 10000;
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
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        long steps;
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
            finally { steps = fixture.Context.Limits.ExecutionStepCount; Close(fixture); }
            Assert.Equal(1, fixture.Argument.Calls);
            final = DescribeOutput(fixture, shape, complete: true);
            AssertUnwound(fixture.Context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var fixture = Prepare(owner, shape);
                fixture.Context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                string location;
                try
                {
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, fixture.Context, asynchronous));
                    Assert.Equal(LythonFailureReason.ExecutionStepLimit, error.Attribution?.Reason);
                    Assert.Equal(MaximumSteps + 1, fixture.Context.Limits.ExecutionStepCount);
                    location = error.Span is { } span ? $"{span.Start},{span.Length},{span.Line},{span.Column}" : "-";
                    AssertUnwound(fixture.Context);
                }
                finally { Close(fixture); }
                checkpoints.Add($"{location}:{fixture.Argument.Calls}:{DescribeOutput(fixture, shape, complete: false)}");
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        var record = JsonSerializer.Serialize(new { asynchronous, exposed, shape, steps, outcome, final, checkpoints });
        output.WriteLine("BINARY_WRITE_GUARDS:" + record);
        using var frozen = JsonDocument.Parse(FrozenGuards);
        Assert.Equal(frozen.RootElement.GetProperty($"{asynchronous}:{exposed}:{shape}").GetString(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record))));
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
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 256, out var padding));
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
            exposed ? "method=file.write\nreturn method(argument())" : "return file.write(argument())").Script!)).EntryPoint;

    private static Fixture Prepare(ILythonMemoryBudget owner, string shape)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/input", [7, 8, 9]);
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
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
        context.Limits.ExecutionStepCount = 0;
        return new(context, host, handle, other, argument, payload);
    }

    private static void Close(Fixture fixture)
    {
        // Reset injected fuel only for native fixture cleanup, after guest work
        // has ended. Both handles are closed before their owner is disposed.
        fixture.Context.Limits.ExecutionStepCount = 0;
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
