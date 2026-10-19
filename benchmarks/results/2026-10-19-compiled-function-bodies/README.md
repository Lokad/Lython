# Compiled function body retention

Immutable `e42d2d8f` stops retaining a redundant lowered body in executable
function bindings. After executable code is constructed, its existing binding
keeps declaration metadata and leading docstring text. The same runtime helper
admits and creates the guest docstring at definition execution. The record copy
preserves already-built generator code. Original source syntax and genuine
lowered class, annotation and suspended-preparation consumers remain available.
Coarse compilation admission stays conservative and unchanged.

87 focused white-box and 345 public Release checks pass. Public controls cover
ordinary/annotated/generic functions, generators, methods, closures, yielded
definition preparation, Unicode docstrings and metadata mutation. Delayed host I/O
checks cancellation and retained results across another execution and compiled
script disposal, with funding until cleanup and allowance returning to baseline.
The [eight pure docstring controls](cpython.json) also match CPython 3.13.2.

One 8.061-second capture on 2026-10-19 uses the maintained complete
`scripts.text-summary-helpers.large` job: 3,072 lines in 35,840 UTF-8 input bytes,
including an annotated helper, file iteration, word aggregation and a report.

| Engine | Fresh compile and complete job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `eb3f674c` | 14.657 ms | 221.25% | Before; inconclusive |
| Lython `e42d2d8f` | 16.496 ms | 151.02% | After; inconclusive |
| CPython 3.13.2 | 6.907 ms | 6.42% | External context on this machine only |

The very large Lython spreads prevent a useful speedup, regression or multiplier
inference. No physical retained-heap, allocation or embedding-latency measurement
was made. The demonstrated removal is the redundant body reference and runtime
docstring-tree inspection. Do not retime this removal as another qualification
campaign or replace the dedicated Linux snapshots in [BENCHMARKS.md](../../../BENCHMARKS.md).

Each invocation compiles afresh in a running host. Timing includes disk staging,
reset, full work, exact stdout/output-file checks and compilation/result cleanup;
startup and worker protocol are excluded. Compilation alone and warm execution
were not separated. Seven alternating rounds follow warmup on shared workstation
LOKAD-0399, x64 Windows build 26200, 28 logical processors, .NET 10.0.13,
workstation GC. Every engine matched exact outputs before and after timing.
The owned process and workers exited successfully. No benchmark framework changed.

Evidence: [raw samples and identities](micro.json), [process receipt](process.receipt.json),
[baseline identity](baseline.json), [scope and hashes](scope.json), and the unchanged
[shared sources, fixtures and independent goldens](../2026-10-19-json-scalar-completion/catalog.json).
