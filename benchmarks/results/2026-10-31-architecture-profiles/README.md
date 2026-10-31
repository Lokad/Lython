# Runtime and frontend attribution at 07c150a6

Fixed diagnostic capture, 2026-10-31 UTC, source
`07c150a6d2c3085206622e16dd32793e6333e558`. This is attribution evidence,
not a new CPython comparison or a speedup measurement. The primary comparison
remains [the performance guide](../../../docs/BENCHMARKS.md).

The dedicated Ubuntu 24.04.4 F4as_v7 VM has four AMD EPYC 9V45 vCPUs and 16 GiB.
The frozen worker uses .NET 11 RC3 (`11.0.100-rc.3.26522.148`). Exact loaded
assembly hashes, process identities, inputs and complete stdout/file verification
are in each `record.json` and [representative.json](representative.json).

One 46-second collection held the existing exclusive VM lease. Its before/after
quiet gates passed. EventPipe allocation sampling and Linux on-CPU sampling ran
separately on the same CSV/text/JSON helper scripts; text compilation had its own
capture. Warm captures exclude compilation/startup. Each allocation capture has
64 executions; each CPU capture has 256. Compilation captures use 512 and 4,096
compilations respectively. These are profiler observations, not timing samples.

| Warm script | On-CPU samples | Interpreter-frame allocation ticks | Temporary-reservation allocation ticks | Context allocation ticks |
| --- | ---: | ---: | ---: | ---: |
| CSV aggregation | 351 | 73.3 MB | 56.3 MB | 47.6 MB |
| Text report | 267 | 60.0 MB | 35.1 MB | 29.7 MB |
| JSON-lines aggregation | 371 | 79.1 MB | 79.3 MB | 45.2 MB |

Allocation ticks are approximate sampled bytes over the complete capture, **not
exact allocation counts or retained bytes**. Strings, byte/object arrays and
boxed integers are also prominent. Instruction dispatch appears in 35/41/46 CPU
leaf samples. Monitor enter/exit account for 37 CSV and 46 JSON samples, but this
does not attribute all lock time to memory admission. Text also has 28 samples in
Unicode sigma-property lookup. These modest sample counts select investigations;
they do not establish exact CPU shares or a predicted gain.

Text compilation allocates approximately 51.3 MB in `EnumerateChildren` iterator
ticks and 17.6 MB in direct-expression traversal ticks. Lexer work, syntax walks
and instruction construction remain separate frontend candidates. The current
first runtime candidate is reusable activation working storage across ordinary
helper calls, with fresh lexical contexts and independently retained closures.

## Execution, I/O and cleanup boundary

The reused success-only reflection diagnostic ran each of six unchanged complete
scripts through internal synchronous phases and public synchronous, completed-I/O
async and delayed-I/O async lanes: eight warmups and sixteen observations per
lane, 576 checked invocations total. The delayed lane suspends at its first real
I/O operation and joins cleanup. All complete outputs and post-cleanup allowance
balances pass. The [source](phases/Program.cs) adds no production instrumentation.

**Its preflight quiet gate failed after building the diagnostic (4.45% idle CPU).**
That evidence is retained in [phase-machine-before.json](phase-machine-before.json).
The intended observations ran once for diagnostic attribution; there was no quiet
retry or timing qualification. The following medians therefore indicate only
where to investigate. Host elapsed is subtracted within each observation before
taking a median; subtracting independent medians would be incorrect.

| Script | Execution excluding nested host elapsed | Context initialization | Resource cleanup |
| --- | ---: | ---: | ---: |
| Configuration edit | 183 us | 30.5 us | 0.3 us |
| File search / early exit | 2,865 us | 30.1 us | 0.3 us |
| CSV helpers | 19,375 us | 41.8 us | 0.9 us |
| Text helpers | 15,053 us | 57.2 us | 0.6 us |
| JSON-lines helpers | 11,261 us | 43.9 us | 0.3 us |
| File manifest helpers | 103 us | 15.1 us | 0.2 us |

Reflection/decorator/stopwatch overhead is included. Host property access is not
timed; actual file reads/writes/flush/disposal are. Allocation phases are
current-thread only; public allocation observations are process-wide. Compilation
is separately profiled; startup is not measured here. These limitations preclude
CPU-share, external-baseline or latency claims. They do support investigating
invocation-internal costs before fixed setup or cleanup for large aggregations.

## Preserved evidence

[profile-evidence.tar.gz](profile-evidence.tar.gz) contains original EventPipe and
perf captures, saved JIT maps, process/verification records, machine gates, phase
observations and collection/analysis sources. Disposable ETLX conversion caches
and built binaries are excluded. Readable summaries and sources are also exposed
alongside this document. Each saved JIT map has one truncated trailing record;
the offline parser skips and reports it, without recapturing data. Unknown CPU
leaves remain visible. EventPipe's `ThreadPollGC` sampling leaves are not treated
as actual on-CPU attribution; the independent perf samples provide that view.

This bounded attribution slice is complete. Retain these observations as controls
for a coherent redesign; do not repeat collection to select favorable samples.
