using System.Text;

namespace Lokad.Lython.Tests;

public sealed class SubprocessCompletionSubsystemTests
{
    [Fact]
    public async Task CompleteBufferedAsync_CapturesPipesWithOutputBound()
    {
        var result = await LythonSubprocessCompletion
            .CompleteBufferedAsync(
                Request(
                    standardOutput: LythonSubprocessStreamMode.Pipe,
                    standardError: LythonSubprocessStreamMode.Pipe,
                    maxOutputBytes: 3),
                7,
                Bytes("abcdef"),
                Bytes("stderr"),
                UnexpectedWriteAsync,
                UnexpectedWriteAsync,
                CancellationToken.None);

        Assert.Equal(7, result.ReturnCode);
        Assert.Equal("abc", Text(result.StandardOutputUtf8));
        Assert.Equal("std", Text(result.StandardErrorUtf8));
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
        Assert.Equal(string.Empty, stderr.Text);
    }

    private static LythonSubprocessRequest Request(
        LythonSubprocessStreamMode standardOutput,
        LythonSubprocessStreamMode standardError,
        long? maxOutputBytes = null) =>
        new(
            Args: ["tool"],
            Cwd: null,
            Environment: null,
            StandardInputUtf8: ReadOnlyMemory<byte>.Empty,
            StandardInput: LythonSubprocessStreamMode.Inherit,
            StandardOutput: standardOutput,
            StandardError: standardError,
            UseShell: false,
            TextMode: true,
            Encoding: null,
            Errors: null,
            TimeoutMilliseconds: null,
            MaxOutputBytes: maxOutputBytes);

    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.Span);

    private static ValueTask UnexpectedWriteAsync(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No inherited output was expected.");

    private sealed class Buffer
    {
        private readonly StringBuilder _text = new();

        public string Text => _text.ToString();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text.Append(SubprocessCompletionSubsystemTests.Text(utf8));
            return ValueTask.CompletedTask;
        }
    }
}
