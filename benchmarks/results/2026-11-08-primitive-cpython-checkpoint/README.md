# Current representative CPython checkpoint

2026-11-08. Committed and fully qualifiedb1e17b64/frozen93e926fe against isolated
CPython3.13.16, actual.NET11RC3, dedicated AzureF4as_v7/Ubuntu24.04.4/four EPYC9V45
vCPUs/16GiB/no swap. Production inventory matches; full Windows/Ubuntu actual11/10,
original Stress both and installed core consumers pass [cached delivery](delivery-ci.json).

Fifteen jobs311.431s: five unchanged maintained scripts, separately compilation,
warm execution and compile-to-result. ReturnedCSVvalidation, CSVhelpers, ordinary
texthelpers, JSON-lineshelpers and JSON-to-Markdownreport; same R109catalog/full
stdout/file goldens, worker7cf/optional0.4/protocol. Two independent Lython processes
and isolated CPython, normal GC/tiering,5swarmup/role/job,7rotated150msrounds.
No new framework, tuned scripts, producer qualification or per-edit full campaign.

# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.502–5.587 ms | 4.463 ms | 1.23–1.25× | Diagnostic; 1.52% / 1.13% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.590–3.624 ms | 3.386 ms | 1.06–1.07× | Diagnostic; 0.93% / 1.59% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.930–1.931 ms | 1.273 ms | 1.52–1.52× | Diagnostic; 0.03% / 1.23% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.886–4.088 ms | 4.247 ms | 0.92–0.96× | Variable; 4.92% / 0.88% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.500–3.590 ms | 2.460 ms | 1.42–1.46× | Variable; 2.59% / 4.96% |

**2026-11-08, source `b1e17b64`, CPython 3.13.16, .NET 11 RC3.**
The table measures **compilation plus one execution in an already-running host**,
including mediated RAM-file I/O, complete output validation and prior-result
cleanup. Worker/engine startup and fixture staging/reset are excluded.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges span
two independent Lython process medians, not confidence intervals. Control is their
absolute difference; max IQR includes CPython. Diagnostic/Variable describe short
observations, not design acceptance thresholds. Variable cases remain visible.

Frozen core `93e926fe` matches the measured production inventory. Windows/Ubuntu
Release actual.NET11/.NET10, original stress both and installed core consumers pass
[cached CI42512127664](delivery-ci.json).
Source qualification and performance evidence are separate. The [R117 checkpoint](README.md)
reuses five unchanged scripts, catalog/goldens and workers:15jobs/311.431s, all
quiet/output/ownership/cleanup/archive gates pass. No remaining VM users/fixtures.

## Warm execution, compilation and startup

Warm execution reuses the compiled script and excludes compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.327–5.355 ms | 4.328 ms | 1.23–1.24× | Diagnostic; 0.52% / 0.67% |
| CSV summary/helpers | 3.427–3.553 ms | 3.320 ms | 1.03–1.07× | Variable; 3.55% / 1.11% |
| Text summary/helpers | 1.875–1.960 ms | 1.186 ms | 1.58–1.65× | Variable; 4.32% / 0.70% |
| JSON-lines/helpers | 3.747–4.073 ms | 4.135 ms | 0.91–0.98× | Variable; 8.01% / 1.01% |
| JSON-to-Markdown report | 3.367–3.438 ms | 2.319 ms | 1.45–1.48× | Variable; 2.07% / 4.04% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 71.4–72.0 µs | 66.2 µs | 1.08–1.09× | Diagnostic; 0.82% / 1.03% |
| CSV summary/helpers | 61.2–61.7 µs | 59.8 µs | 1.02–1.03× | Diagnostic; 0.76% / 1.12% |
| Text summary/helpers | 44.9–45.2 µs | 41.5 µs | 1.08–1.09× | Diagnostic; 0.69% / 2.12% |
| JSON-lines/helpers | 52.8–54.5 µs | 51.2 µs | 1.03–1.07× | Variable; 3.19% / 4.32% |
| JSON-to-Markdown report | 42.0–42.7 µs | 37.8 µs | 1.11–1.13× | Diagnostic; 1.61% / 2.67% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.



Variable cases and all roles are retained. Ratios compare complete workloads and
do not attribute isolated optimization gains or repay older source-pair debt.
Config/search/manifest and four additional helper/normalization variants keep
historical726labels; no inference they describeb1e17b64. Startup/RSS/pending-I/O/
embedding-tail gaps remain. Current guide and this snapshot use the same raw data.

All source/core/aux/Ready/golden/quiet-before-and-after/Closed/exit/join/fixture/
unmount/archive/local verification gates pass. Live360sowner captured, all15jobs
complete within311.431s; no remaining users or RAM fixtures. Persistent evidence,
already-preserved B475core/archive reused. Fresh exact-byte/inode/alias hardlinks
recovered5.03MB, all old paths/bytes/archives retained. OS240MiB/evidence12MiB/
actualpayload preflight strengthened to count payload separately; no relaxed guard.
Copied summarizer's28-job assertion corrected to15 before first post-processing,
original helper retained, no remeasurement or changed observations.

[Summary](summary.json), [identity](identity.json), [receipt](archive-receipt.json),
[prior guide](prior-BENCHMARKS.md), [sizing](sizing.json), [alias proof](dedup-proof.json),
[all raw/source/corrections](observations.tar.xz).
