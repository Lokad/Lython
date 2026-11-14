# Representative CPython checkpoint after native primitive storage

2026-11-14 UTC, committed `98acaa5b` / frozen `a4bfbfd8`, versus stock
CPython 3.13.16. [Full platform/stress/package qualification](https://github.com/Lokad/Lython/actions/runs/43605198037)
is pending independently. The [current guide](../../../docs/BENCHMARKS.md) leads
with the representative table; this artifact preserves the exact measured source.

Quiet Azure Standard F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs, 16 GiB/no
swap, .NET 11 RC3, ordinary GC/tiering and CPython GIL/GC. B736 completes all nine
jobs in 360.622 seconds with unchanged catalog, workers, toolchains and timing
boundaries. Four fresh processes/job, full-request warmups and five rotating
0.5-second batches. Every image, catalog, stdout/file golden, budget cleanup,
quiet endpoint, joined-user and archive gate passes. No workers or RAM fixtures
remain. Raw SHA256:
`755c49043e2bfe85b6b17c55cd65178a3e496a13f5302c81e7f90ee5aa127f51`.

Invoice warm 1.22–1.24× CPython; compile alone 1.09–1.10×; compile-to-result
1.22–1.23×. Captured text 1.48–1.49×, JSON 0.83–0.87× (variable), file search
1.64–1.68×, report 1.21–1.23× (variable), fulfillment 1.25–1.26×, maintained
for-loop CSV validation 1.29–1.32×. Ratio is Lython elapsed / CPython elapsed.
JSON's Lython process spread is 4.04%; report maximum IQR is 4.13%. These remain
uncertainty, not omitted cases or failed designs. No cross-collection attribution,
universal gain or claim the separate diagnostic CSVwhile speedup applies to the
different maintained algorithm. The broad performance objective remains open.

Warm elapsed includes mediated RAM-file I/O, complete stdout/file validation and
result cleanup; excludes compilation, startup, fixture reset/protocol and final
cache disposal. Compile-to-result adds compilation and compiled-owner disposal.
Wider process CPU includes reset/protocol. Ranges are process medians/all pairings,
not confidence intervals. Startup, real pending I/O, shared-budget contention,
RSS/heap peaks and embedding tail latency remain unmeasured.

All 582 frozen input files match the measured commit with explicit newline
normalization; this does not infer independently rebuilt binary byte identity.
Exact [runtime sources/binary](../2026-11-14-native-primitive-storage/refined/sources-checks-and-observations.tar.xz)
and [source proof](measured-source-proof.json) are preserved. Only one duplicate
completed old B435 raw archive was retired after exact committed canonical-byte,
path/kind/UID/link proof under a fresh idle lease. All current images, catalogs,
toolchains and 254/252 MiB floors remain; no new core/catalog upload or rerun.

[All medians/controls](summary.json), [identity](identity.json),
[terminal status](terminal-status.json), [prospective plan](prospective.json),
[all tools/receipts/raw](observations.tar.xz), [prior guide](prior-BENCHMARKS.md).
Publication/tagging and final adapter qualification remain separate and postponed.
