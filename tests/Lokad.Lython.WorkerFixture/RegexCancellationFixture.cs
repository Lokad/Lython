using System.Text;
using System.Text.Json;
using Lokad.Lython;

// The parent owns/kills this process if a backend ignores cancellation. There
// is no abandoned matching task in the test host or the embedding runtime.
internal static class RegexCancellationFixture
{
    internal static int Run(string stage)
    {
        var budget = new LythonMemoryBudget();
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        var finished = false;
        var runningAtCancellation = false;
        var cancelled = false;
        var originalToken = false;
        var provider = Utf8RegexProvider.Instance;
        // Warm shared parser/operation metadata before arming the controller.
        using (var warmOwner = budget.CreateScope())
        {
            var warm = provider.Compile("(?:a|b){1,3}"u8.ToArray(), new(32, false), warmOwner, default);
            using var subject = warm.Prepare("a!"u8.ToArray(), warmOwner, default);
            _ = subject.Match(LythonRegexMatchMode.Search, default);
        }
        Action operation;
        using (var owner = budget.CreateScope())
        {
            ILythonRegexSubject? subject = null;
            ILythonRegexCursor? cursor = null;
            if (stage == "compile")
            {
                var pattern = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("(?:a|b){1,3}", 10000)));
                operation = () => provider.Compile(pattern, new(32, false), owner, cancellation.Token);
            }
            else
            {
                var pattern = provider.Compile("(a+)+$"u8.ToArray(), new(32, false), owner, default);
                subject = pattern.Prepare(Encoding.UTF8.GetBytes(new string('a', 128) + "!"), owner, default);
                if (stage == "cursor") cursor = subject.Find(default);
                operation = stage switch
                {
                    "match" => () => subject.Match(LythonRegexMatchMode.Search, cancellation.Token),
                    "cursor" => () => cursor!.Next(cancellation.Token),
                    "substitute" => () => subject.Substitute("x"u8.ToArray(), 0, cancellation.Token),
                    "split" => () => subject.Split(0, cancellation.Token),
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
            }
            finally
            {
                Volatile.Write(ref finished, true);
                controller.Join();
                cursor?.Dispose(); subject?.Dispose();
            }
        }
        if (!runningAtCancellation || !cancelled || !originalToken || budget.ReservedBytes != 0)
            throw new InvalidOperationException($"{stage}: running={runningAtCancellation}, cancelled={cancelled}, originalToken={originalToken}, balance={budget.ReservedBytes}");
        Console.Write(JsonSerializer.Serialize(new { stage, runningAtCancellation, cancelled, originalToken, balance = budget.ReservedBytes }));
        return 0;
    }
}
