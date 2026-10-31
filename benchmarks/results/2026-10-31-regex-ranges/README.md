# Byte-range provider integration diagnostic

One fixed local before/after/identical-before capture on 2026-10-31. Baseline is
`07c150a6`; the changed source is that revision plus [source.patch](source.patch).
Loaded assembly hashes are recorded in every observation. This checks the Lython
integration with the same pinned PythonRe 0.4.0 adapter, **not standalone backend
performance or certification of a native range-only backend**.

| Snapshot | Warm time / invocation | Managed allocation / invocation | Status |
| --- | ---: | ---: | --- |
| Before | 1.021 ms | 515,488 B | Diagnostic |
| After range contract | 1.146 ms | 516,288 B | Diagnostic |
| Identical before control | 1.227 ms | 515,488 B | Diagnostic |

The before-control spread makes elapsed comparisons inconclusive. The allocation
increase is 800 B (0.16%) per invocation. No speedup is claimed. The structural
result is removal of capture-value transport and the adapter's dense Unicode
coordinate map; the unchanged workload is ASCII and cannot establish the latter's
benefit. Sparse scalar projection, borrowed original windows and Unicode captures
are covered separately by contract tests and independent CPython semantic goldens.
Detailed backend snapshots still exist inside the development adapter.

Each invocation performs 100 searches from the unchanged `RegexScanBenchmarks`
compiled-pattern reuse script, using its host. The boundary is warm public
`RunAsync`, golden return value `100`, and result disposal. Compilation/startup
are excluded; all five setup scripts are disposed. No builds/tests overlapped
these captures. Four warmups precede seven rounds of five invocations in each
fresh process. The owned processes finish within the 30-second cap; no matching
work is abandoned. Tiered compilation is disabled for all three captures.

This reuses the [existing one-off driver](../2026-10-19-regex-boundary/Program.cs),
built unchanged for net11.0. Machine: Windows 10.0.26200, 28 logical processors,
.NET 11 RC3. No quiet-machine gate was run, so these are local diagnostics only.
Raw samples, machine/runtime and assembly hashes are in [before](before.json),
[after](after.json) and [control](before-control.json). This bounded slice is
complete; do not recapture to select a more favorable timing result.
