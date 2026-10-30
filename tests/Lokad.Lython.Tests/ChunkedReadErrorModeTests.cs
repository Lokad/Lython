using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG01/MG21: damaged bytes past the first window fail lazily with the same
/// error modes as whole-buffer decoding. Damage in the first window also fails
/// on consumption, independently of successful opening.
/// </summary>
public sealed class ChunkedReadErrorModeTests
{
    private static byte[] DamagedPayload(int window)
    {
        var prefix = System.Text.Encoding.UTF8.GetBytes("A\n" + new string('z', window + 5));
        var suffix = System.Text.Encoding.UTF8.GetBytes("B\n");
        var payload = new byte[prefix.Length + 1 + suffix.Length];
        Buffer.BlockCopy(prefix, 0, payload, 0, prefix.Length);
        payload[prefix.Length] = 0xFF;
        Buffer.BlockCopy(suffix, 0, payload, prefix.Length + 1, suffix.Length);
        return payload;
    }

    private static LythonRuntime.ExecutionContext.ChunkedTextFileReadState OpenState(
        MockLythonHost host, string path, int window,
        LythonRuntime.TextEncodingMode encoding, LythonRuntime.TextErrorMode errors,
        LythonRuntime.TextNewlineMode newline)
    {
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var file = new OpenedByteFile(context.Services);
        file.OpenAsync(path, new LythonFileOpenOptions(LythonFileAccess.Read), null, false).GetAwaiter().GetResult();
        var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(
            file, context, encoding, errors, newline, window);
        return state;
    }

    [Fact]
    public void DamagePastFirstWindowDecodesPerMode()
    {
        foreach (var window in new[] { 1, 2, 3, 5, 9 })
        {
            var expectedZ = new string('z', window + 5);

            var host = new MockLythonHost();
            host.SeedRawTextUtf8("/e.txt", DamagedPayload(window));
            using var replace = OpenState(host, "/e.txt", window,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Replace,
                LythonRuntime.TextNewlineMode.TranslateUniversal);
            Assert.Equal("A\n" + expectedZ + "\ufffdB\n", replace.Read(-1).AsString());

            var host2 = new MockLythonHost();
            host2.SeedRawTextUtf8("/e.txt", DamagedPayload(window));
            using var ignore = OpenState(host2, "/e.txt", window,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Ignore,
                LythonRuntime.TextNewlineMode.TranslateUniversal);
            Assert.Equal("A\n" + expectedZ + "B\n", ignore.Read(-1).AsString());

            var host3 = new MockLythonHost();
            host3.SeedRawTextUtf8("/e.txt", DamagedPayload(window));
            using var backslash = OpenState(host3, "/e.txt", window,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.BackslashReplace,
                LythonRuntime.TextNewlineMode.TranslateUniversal);
            Assert.Equal("A\n" + expectedZ + "\\xffB\n", backslash.Read(-1).AsString());
        }
    }

    [Fact]
    public void DamagePastFirstWindowFailsStrictOnTouch()
    {
        foreach (var window in new[] { 1, 2, 3, 5, 9 })
        {
            var host = new MockLythonHost();
            host.SeedRawTextUtf8("/e.txt", DamagedPayload(window));
            using var strict = OpenState(host, "/e.txt", window,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal);
            Assert.Equal("A\n", strict.ReadLine(-1).AsString());
            var failure = Assert.Throws<LythonRuntimeException>(() => strict.Read(-1));
            Assert.Equal("UnicodeDecodeError", failure.ExceptionType);
            Assert.Contains("codec can't decode byte 0xff", failure.Message, System.StringComparison.Ordinal);
            Assert.EndsWith("invalid start byte", failure.Message, System.StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DamageInsideFirstWindowFailsOnConsumption()
    {
        var host = new MockLythonHost();
        host.SeedRawTextUtf8("/e.txt", new byte[] { (byte)'A', 0xFF, (byte)'\n' });
        using var state = OpenState(host, "/e.txt", 16,
            LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.TranslateUniversal);
        var failure = Assert.Throws<LythonRuntimeException>(() => state.Read(1));
        Assert.Equal("UnicodeDecodeError", failure.ExceptionType);
    }
}
