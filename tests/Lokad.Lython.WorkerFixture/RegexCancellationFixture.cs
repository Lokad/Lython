using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython;

// The parent owns/kills this process if a backend ignores cancellation. There
// is no abandoned matching task in the test host or the embedding runtime.
internal static class RegexCancellationFixture
{
    internal static int Run(string stage)
    {
        var libraries = new[]
        {
            typeof(LythonEngine).Assembly,
            typeof(Utf8RegexProvider).Assembly,
            typeof(Lokad.Utf8Regex.Utf8Regex).Assembly,
            typeof(Lokad.Utf8Regex.PythonRe.Utf8PythonRegex).Assembly,
        }.ToDictionary(assembly => assembly.GetName().Name!,
            assembly => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))));
        var budget = new LythonMemoryBudget();
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        var finished = false;
        var runningAtCancellation = false;
        var cancelled = false;
        var originalToken = false;
        var backendInterrupted = false;
        var provider = Utf8RegexProvider.Instance;
        // Warm shared parser/operation metadata before arming the controller.
        using (var warmOwner = budget.CreateScope())
        {
            using var warm = provider.Compile("(?:a|b){1,3}"u8.ToArray(), new(32, false), warmOwner, default);
            _ = warm.TryMatch("a!"u8.ToArray(), new(0, 2, 0, 2), LythonRegexMatchMode.Search,
                new LythonRegexCapture?[warm.CaptureSlotCount], out _, warmOwner, default);
        }
        Action operation;
        using (var owner = budget.CreateScope())
        {
            ILythonRegexPattern? pattern = null;
            ILythonRegexSubject? subject = null;
            ILythonRegexCursor? cursor = null;
            if (stage == "compile")
            {
                var source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("(?:a|b){1,3}", 10000)));
                operation = () => { using var compiled = provider.Compile(source, new(32, false), owner, cancellation.Token); };
            }
            else
            {
                // Search for a consuming match over distinct alternatives.
                // The group count selects the adapter's one-shot route.
                var source = stage switch
                {
                    "value-match" => "(?:ab|ac)+",
                    "grouped-match" => "((ab|ac)+)",
                    _ => "(?P<value>(?:ab|ac)+)",
                };
                pattern = provider.Compile(Encoding.UTF8.GetBytes(source), new(32, false), owner, default);
                var input = new byte[1024 * 1024];
                for (var offset = 0; offset < input.Length; offset += 4)
                    "abac"u8.CopyTo(input.AsSpan(offset));
                var window = new LythonRegexWindow(0, input.Length, 0, input.Length);
                if (stage == "cursor")
                {
                    subject = pattern.Prepare(input, window, owner, default);
                    cursor = subject.Find(default);
                }
                var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
                operation = stage switch
                {
                    "match" or "value-match" or "grouped-match" =>
                        () => pattern.TryMatch(input, window, LythonRegexMatchMode.Search, captures, out _, owner, cancellation.Token),
                    "cursor" => () => cursor!.TryNext(captures, out _, cancellation.Token),
                    _ => throw new ArgumentException("Unknown cancellation stage.", nameof(stage)),
                };
            }
            var controller = new Thread(() =>
            {
                entered.Wait();
                Thread.Sleep(5);
                runningAtCancellation = !Volatile.Read(ref finished);
                cancellation.Cancel();
            }) { IsBackground = true };
            controller.Start();
            try
            {
                entered.Set();
                operation();
            }
            catch (OperationCanceledException error)
            {
                cancelled = true;
                originalToken = error.CancellationToken == cancellation.Token;
                backendInterrupted = new StackTrace(error).GetFrames().Any(frame =>
                {
                    var assembly = frame.GetMethod()?.DeclaringType?.Assembly;
                    return assembly == typeof(Lokad.Utf8Regex.Utf8Regex).Assembly ||
                        assembly == typeof(Lokad.Utf8Regex.PythonRe.Utf8PythonRegex).Assembly;
                });
            }
            finally
            {
                Volatile.Write(ref finished, true);
                controller.Join();
                cursor?.Dispose(); subject?.Dispose(); pattern?.Dispose();
            }
        }
        if (!runningAtCancellation || !cancelled || !originalToken || budget.ReservedBytes != 0)
            throw new InvalidOperationException($"{stage}: running={runningAtCancellation}, cancelled={cancelled}, originalToken={originalToken}, balance={budget.ReservedBytes}");
        Console.Write(JsonSerializer.Serialize(new { stage, runningAtCancellation, cancelled, originalToken, backendInterrupted,
            balance = budget.ReservedBytes, libraries }));
        return 0;
    }
}
