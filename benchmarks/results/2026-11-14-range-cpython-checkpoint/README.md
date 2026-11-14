# Representative CPython checkpoint after shared range iteration

2026-11-14 UTC, committed `fa40d674` / frozen `73a1b233`, stock CPython 3.13.16.
[Full platform/stress/installed-package qualification](https://github.com/Lokad/Lython/actions/runs/43625900419)
is pending independently. All 582 frozen inputs match the commit with explicit
newline normalization; independently rebuilt binary identity is not inferred.

Quiet F4as_v7 / Ubuntu 24.04.4 / four EPYC 9V45 / 16 GiB / no swap / .NET11RC3;
normal workstation GC/tiering, CPython GIL/GC. B747 completes nine jobs in
360.962 seconds. Four fresh processes/job, full-request
warmups (5 seconds; 12 text/report), five rotating half-second batches. Catalog,
workers/toolchains and boundaries unchanged. All image/catalog/golden/file/budget/
quiet/awaited-cleanup/persistent-archive gates pass. No users/fixtures remain.
Raw SHA256 `e221c7664e66f51e0c50f6a4b58c93b3de11655105ec6fcc3c2f9f1aaeafd62d`.

| Warm workload | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| CSV invoice / pricing helpers | 7.693–7.899 ms | 6.036–6.098 ms | 1.26–1.31× | Diagnostic; spreads 2.68/1.02%; max IQR 1.78% |
| Text / captured helper and counter | 1.298–1.315 ms | 878.121–892.451 µs | 1.45–1.50× | Diagnostic; spreads 1.34/1.61%; max IQR 2.30% |
| JSON-lines / aggregation helpers | 3.397–3.499 ms | 4.068–4.078 ms | 0.83–0.86× | Diagnostic; spreads 2.92/0.24%; max IQR 1.16% |
| File search / keyword-default helper | 257.239–266.542 µs | 157.790–159.979 µs | 1.61–1.69× | Variable; spreads 3.62/1.37%; max IQR 2.73% |
| JSON-to-Markdown report | 2.836–2.880 ms | 2.332–2.340 ms | 1.21–1.23× | Variable; spreads 1.56/0.32%; max IQR 4.82% |
| CSV fulfillment / pricing and validation | 8.540–8.604 ms | 6.652–6.783 ms | 1.26–1.29× | Diagnostic; spreads 0.75/1.92%; max IQR 1.90% |
| CSV validation / for-loop checksum | 5.441–5.579 ms | 4.210–4.219 ms | 1.29–1.33× | Diagnostic; spreads 2.47/0.21%; max IQR 1.30% |

Ratio above one means slower. Process medians/all pairings, not confidence
intervals. Variable observations are retained; a three-percent label is descriptive,
not a design acceptance veto. Invoice compilation: 100.618–102.258 µs | 93.686–93.839 µs | 1.07–1.09× | Diagnostic; spreads 1.63/0.16%; max IQR 0.97%.
Invoice compile-to-result: 7.769–7.841 ms | 6.242–6.283 ms | 1.24–1.26× | Diagnostic; spreads 0.92/0.66%; max IQR 1.30%.
No cross-collection attribution or extrapolation of the diagnostic counter gains
to these different complete algorithms. Broader performance objective stays open.

Warm includes mediated RAM I/O, full stdout/file validation and result cleanup;
excludes compile/startup, fixture reset/protocol and final cache disposal. Fresh
adds compilation and compiled-owner disposal. Wider CPU includes reset/protocol.
Startup, real pending-I/O latency, contention, heap/RSS peaks and embedding tail
latency remain unmeasured. No new core/catalog upload, deletion or altered floors.

Exact [sources/binary](../2026-11-14-range-iteration-stage/refined/sources-checks-and-observations.tar.xz),
[source proof](measured-source-proof.json), [all medians/controls](summary.json),
[identity](identity.json), [terminal](terminal-status.json),
[all collection tools/receipts/raw](observations.tar.xz), [previous guide](prior-BENCHMARKS.md).
Publication/tagging and affected final released-adapter checks remain postponed.
