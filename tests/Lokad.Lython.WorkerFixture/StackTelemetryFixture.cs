using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

// A separate process gives this check genuinely cold native resolver state.
// Reflection stays in the trusted fixture; no runtime API is exposed to guests.
internal static class StackTelemetryFixture
{
    internal static int Run(string libraryPath)
    {
        var bin = Path.GetDirectoryName(Path.GetFullPath(libraryPath))!;
        AssemblyLoadContext.Default.Resolving += (_, name) => Assembly.LoadFrom(Path.Combine(bin, name.Name + ".dll"));
        var library = Assembly.LoadFrom(libraryPath);
        var guards = library.GetType("Lokad.Lython.Runtime.ExecutionGuards", throwOnError: true)!;
        var ruler = guards.GetNestedType("StackRuler", BindingFlags.NonPublic)!;
        var remaining = ruler.GetMethod("RemainingBytes", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<long>>();
        const int count = 32;
        using var ready = new CountdownEvent(count);
        using var start = new ManualResetEventSlim();
        var first = new long[count];
        var second = new long[count];
        var errors = new string?[count];
        var threads = Enumerable.Range(0, count).Select(i => new Thread(() =>
        {
            ready.Signal();
            start.Wait();
            try
            {
                first[i] = remaining();
                Thread.Sleep(100);
                second[i] = remaining();
            }
            catch (Exception ex) { errors[i] = ex.ToString(); }
        }, 16 * 1024 * 1024) { IsBackground = true }).ToArray();
        foreach (var thread in threads) thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Telemetry threads did not become ready.");
        start.Set();
        foreach (var thread in threads)
            if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Telemetry thread did not stop.");
        Console.WriteLine(JsonSerializer.Serialize(new { first, second, errors }));
        return errors.Any(x => x is not null) ? 2 : 0;
    }
}
