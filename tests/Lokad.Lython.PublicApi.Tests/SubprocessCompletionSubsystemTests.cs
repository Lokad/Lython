using System.Text;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessCompletionSubsystemTests
{
    [Fact]
    public async Task CaptureRefusalPrecedesInheritedOutput()
    {
        var budget = new LythonMemoryBudget(32);
        var request = Request(LythonSubprocessStreamMode.Inherit, LythonSubprocessStreamMode.Pipe)
            with { MemoryBudget = budget };
        var writes = 0;
        var error = await Assert.ThrowsAsync<LythonHostException>(async () =>
            await LythonSubprocessCompletion.CompleteBufferedAsync(request, 0, Bytes("out"), Bytes("err"),
                (_, _) => { writes++; return ValueTask.CompletedTask; }, UnexpectedWriteAsync, default));
        Assert.Equal(LythonHostFailureKind.MemoryAdmission, error.Kind);
        Assert.True(error.RequestedBytes > 3);
        Assert.Equal(0, writes);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedOwnerSurvivesInheritedWritesAndUnwindsAfterTheirActualCompletion(bool cancel)
    {
        var budget = new LythonMemoryBudget(4096);
        var request = Request(LythonSubprocessStreamMode.Pipe, LythonSubprocessStreamMode.Inherit)
            with { MemoryBudget = budget };
        using var inputOwner = budget.CreateScope();
        Assert.True(inputOwner.TryReserve(128 + 1024 + 3, out _));
        var baseline = budget.ReservedBytes;
        var input = new byte[1024];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var pending = LythonSubprocessCompletion.CompleteBufferedAsync(request, 0, input, Bytes("err"),
            UnexpectedWriteAsync, async (bytes, token) =>
            {
                entered.TrySetResult();
                await release.Task;
                Assert.Equal("err", Text(bytes));
                Assert.Equal(cancellation.Token, token);
                token.ThrowIfCancellationRequested();
                throw new IOException("downstream closed");
            }, cancellation.Token).AsTask();
        Exception? failure = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes >= baseline + 1024);
            if (cancel) cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes >= baseline + 1024);
        }
        finally
        {
            release.TrySetResult();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else Assert.IsType<IOException>(failure);
        Assert.Equal(baseline, budget.ReservedBytes);
        inputOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CapturedMergeOwnsIndependentBytesUntilResultDisposal()
    {
        var budget = new LythonMemoryBudget(256);
        using var owner = budget.CreateScope();
        var request = Request(LythonSubprocessStreamMode.Pipe, LythonSubprocessStreamMode.StandardOutput)
            with { MemoryBudget = owner };
        var stdout = Encoding.UTF8.GetBytes("out");
        var stderr = Encoding.UTF8.GetBytes("err");
        var result = await LythonSubprocessCompletion.CompleteBufferedAsync(request, 0,
            stdout, stderr, UnexpectedWriteAsync, UnexpectedWriteAsync, default);
        using (result)
        {
            stdout[0] = stderr[0] = (byte)'x';
            Assert.Equal("outerr", Text(result.StandardOutputUtf8));
            Assert.True(budget.ReservedBytes >= result.StandardOutputUtf8.Length);
        }
        Assert.Equal(0, budget.ReservedBytes); // Parent owner is still alive.
        Assert.Throws<ObjectDisposedException>(() => result.StandardOutputUtf8);
        Assert.Throws<ObjectDisposedException>(() => result.StandardErrorUtf8);
    }

    [Fact]
    public async Task RefusedMergeDoesNotPublishOrRetainWorkingStorage()
    {
        var budget = new LythonMemoryBudget(32);
        using var owner = budget.CreateScope();
        var request = Request(LythonSubprocessStreamMode.Pipe, LythonSubprocessStreamMode.StandardOutput)
            with { MemoryBudget = owner };
        var failure = await Record.ExceptionAsync(async () =>
            await LythonSubprocessCompletion.CompleteBufferedAsync(request, 0,
                Bytes("out"), Bytes("err"), UnexpectedWriteAsync, UnexpectedWriteAsync, default));
        Assert.NotNull(failure);
        Assert.Contains("shared memory budget", failure.Message);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompleteBufferedAsync_InheritsAndClearsUncapturedOutput()
    {
        var stdout = new Buffer();
        var stderr = new Buffer();

        using var result = await LythonSubprocessCompletion
            .CompleteBufferedAsync(
                Request(
                    standardOutput: LythonSubprocessStreamMode.Inherit,
                    standardError: LythonSubprocessStreamMode.Inherit),
                0,
                Bytes("out"),
                Bytes("err"),
                stdout.WriteAsync,
                stderr.WriteAsync,
                CancellationToken.None);

        Assert.True(result.StandardOutputUtf8.IsEmpty);
        Assert.True(result.StandardErrorUtf8.IsEmpty);
        Assert.Equal("out", stdout.Text);
        Assert.Equal("err", stderr.Text);
    }

    [Fact]
    public async Task CompleteBufferedAsync_RedirectedStderrFollowsStdoutMode()
    {
        var stdout = new Buffer();
        var stderr = new Buffer();

        using var result = await LythonSubprocessCompletion
            .CompleteBufferedAsync(
                Request(
                    standardOutput: LythonSubprocessStreamMode.Inherit,
                    standardError: LythonSubprocessStreamMode.StandardOutput),
                0,
                Bytes("out"),
                Bytes("err"),
                stdout.WriteAsync,
                stderr.WriteAsync,
                CancellationToken.None);

        Assert.True(result.StandardOutputUtf8.IsEmpty);
        Assert.True(result.StandardErrorUtf8.IsEmpty);
        Assert.Equal("outerr", stdout.Text);
        Assert.Equal(2, stdout.WriteCount);
        Assert.Equal(string.Empty, stderr.Text);
    }

    [Fact]
    public async Task CompleteBufferedAsync_DiscardsRedirectedOutputWithoutCombiningIt()
    {
        using var result = await LythonSubprocessCompletion
            .CompleteBufferedAsync(
                Request(
                    standardOutput: LythonSubprocessStreamMode.DevNull,
                    standardError: LythonSubprocessStreamMode.StandardOutput),
                0,
                Bytes("out"),
                Bytes("err"),
                UnexpectedWriteAsync,
                UnexpectedWriteAsync,
                CancellationToken.None);

        Assert.True(result.StandardOutputUtf8.IsEmpty);
        Assert.True(result.StandardErrorUtf8.IsEmpty);
    }

    private static LythonSubprocessRequest Request(
        LythonSubprocessStreamMode standardOutput,
        LythonSubprocessStreamMode standardError) =>
        new(
            Args: ["tool"],
            Cwd: null,
            Environment: null,
            StandardInputUtf8: ReadOnlyMemory<byte>.Empty,
            StandardInput: LythonSubprocessStreamMode.Inherit,
            StandardOutput: standardOutput,
            StandardError: standardError,
            InvocationMode: LythonSubprocessInvocationMode.Direct,
            ContentMode: LythonSubprocessContentMode.Text,
            TextEncoding: LythonSubprocessTextEncoding.Utf8,
            TextErrorMode: LythonSubprocessTextErrorMode.Strict,
            Timeout: null);

    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.Span);

    private static ValueTask UnexpectedWriteAsync(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No inherited output was expected.");

    private sealed class Buffer
    {
        private readonly StringBuilder _text = new();

        public string Text => _text.ToString();

        public int WriteCount { get; private set; }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            _text.Append(SubprocessCompletionSubsystemTests.Text(utf8));
            return ValueTask.CompletedTask;
        }
    }
}

