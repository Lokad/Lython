using BenchmarkDotNet.Running;
using Lokad.Lython.Benchmarks.Comparison;
using System.Runtime.CompilerServices;

if (args.Length > 0 && args[0] == "--compare")
{
    Environment.ExitCode = ComparisonCatalogCommand.Run(args[1..]);
    return;
}

RunBenchmarks(args);

// Keep BenchmarkDotNet type resolution off the comparison startup path.
[MethodImpl(MethodImplOptions.NoInlining)]
static void RunBenchmarks(string[] arguments) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments);

