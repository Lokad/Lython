using BenchmarkDotNet.Running;
using Lokad.Lython.Benchmarks.Comparison;

if (args.Length > 0 && args[0] == "--compare")
{
    Environment.ExitCode = ComparisonCatalogCommand.Run(args[1..]);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

