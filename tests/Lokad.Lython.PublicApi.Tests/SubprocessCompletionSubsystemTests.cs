using System.Text;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessCompletionSubsystemTests
{
    [Fact]
    public async Task CapturedMergeRemainsFundedUntilItsOwnerEnds()
    {
        var budget = new LythonMemoryBudget(256);
        using var owner = budget.CreateScope();
        var request = Request(LythonSubprocessStreamMode.Pipe, LythonSubprocessStreamMode.StandardOutput)
            with { MemoryBudget = owner };
        var result = await LythonSubprocessCompletion.CompleteBufferedAsync(request, 0,
            Bytes("out"), Bytes("err"), UnexpectedWriteAsync, UnexpectedWriteAsync, default);
        Assert.Equal("outerr", Text(result.StandardOutputUtf8));
        Assert.True(budget.ReservedBytes >= result.StandardOutputUtf8.Length);
        result = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
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

        var result = await LythonSubprocessCompletion
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

        var result = await LythonSubprocessCompletion
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
        var result = await LythonSubprocessCompletion
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

