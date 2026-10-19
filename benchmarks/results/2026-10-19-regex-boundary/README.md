# Regex provider integration diagnostic

2026-10-19 UTC. Immutable before revision `0691201b`, after `ad49175d`.
This is a Lython-only integration-cost check. CPython remains the primary
external baseline in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Snapshot | Warm time per invocation | Managed allocation per invocation | Status |
| --- | ---: | ---: | --- |
| Before `0691201b` | 0.626 ms | 420,427 B | Diagnostic |
| After `ad49175d` | 0.701 ms | 488,096 B | Diagnostic |
| Identical before control | 0.612 ms | 420,416 B | Diagnostic |

Each invocation performs **100 searches**, using the existing
`RegexScanBenchmarks` compiled-pattern-reuse script and host, with unnamed
captures. The boundary includes warm public `RunAsync`, a return-value check
(`100`) and result disposal. Compilation, startup and transport are excluded.
All five setup scripts are disposed after the capture. The driver calls the
public API directly so it can dispose each result deterministically.

The after median is nominally 1.12–1.15× the two before medians; allocation is
about 16.1% higher. Timing is **unqualified**: one development-machine capture,
no quiet-machine/noise qualification, visible round spread and reflection
in both invocation paths. There is no qualified speedup/regression, CPython
ratio, embedding-latency claim or attribution to one allocation mechanism.
The added boundary has a cost in this small frequent-match workload. Stop this
round here; this does not justify another benchmark campaign.

Machine: Microsoft Windows 10.0.26200, 28 logical processors, .NET 10.0.13.
This was a local development machine, not the dedicated comparison VM.
Tiered compilation was disabled. Both snapshots use default standalone owners
and no cancellation token; the optional adapter's unavailable backend
cancellation capability is documented separately. No local builds/tests
overlapped collection. Each process did 4 warmups, 7 rounds of 5 invocations,
checked the same golden, and exited successfully in 0.829–1.309 s including
startup/compilation/cleanup. No full lane ran.

Raw rounds, allocation totals, runtime/machine details and loaded-assembly hashes
are in [before](before.json), [after](after.json) and
[identical control](before-control.json). Their process receipts record exit,
command, executable identity and elapsed time. The [one-off driver](Program.cs)
reuses the maintained benchmark source; it adds no runtime/framework API.

To reproduce, build immutable Release benchmark outputs for both revisions,
build `RegexDiagnostic.csproj` with .NET 10, then invoke its DLL with the absolute
benchmark DLL path and revision. Set `DOTNET_TieredCompilation=0`; use an owned
30-second process cap and await cleanup. Run one before/after/control round,
not an unbounded timing loop. For the production contract and separately
qualified adapter capabilities see [adapter documentation](../../../src/Lokad.Lython.Utf8Regex/README.md).
