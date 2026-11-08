# Canonical-method checkpoint against CPython

2026-11-08. Committed productionac5aea97, frozen6a098198 core, informational
SourceLinkaaa9cb09; complete production inventory matches the measured source.
Unchanged worker7cf37ca5/optional0.4 adapter, CPython3.13.16/.NET11RC3, quiet
Ubuntu24.04.4 F4as_v7 (four EPYC9V45vCPUs,16GiB/no swap). NormalGC/tiering,
five-second warmup per role/job, seven rotated150ms rounds, two independent
identical Lython processes and one isolated CPython process.

26jobs/538.681s: unchanged eight representative scripts with
compilation/warm/compile-to-result separated, plus existing ordinary rsplit/guarded
helper warm cases. Execution includes mediated RAM I/O/full stdout/files validation/
prior-result cleanup; compilation includes construction/disposal. Startup/staging/
reset/protocol/final cached teardown excluded. CPython imports persist; Lython
execution modules remain isolated, filesystem mediation/path checks included.
These small fixtures do not establish large-file throughput. Actual suspension,
RSS and embedding-tail latency remain unmeasured. No single-change CPU attribution
or repayment of earlier regression debt.

All source/core/auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture/
archive gates pass. Workers joined and owned128MiB RAM fixtures removed/unmounted
before archival under lease and independent local verification.570sowner+30stop+
30margin. Existing immutable B451 core/archive reused with exact proof; historical
measured footprint+25% fits7MiB above240MiB OSfloor. No evidence deletion/dedup.
Full sourceac5aea97 Windows/Ubuntu actual11/10/originalStress/installed delivery is
separately green and cached asCI42463155006; it does not follow from timing.

Ratios=Lythonelapsed/CPythonelapsed; above1 means slower. Ranges span two Lython
process medians, not confidence intervals. Diagnostic/Variable retain uncertainty,
not design vetoes. Guarded helper remains2.15x warm, ordinary rsplit1.80x; allocation/
architecture/source-pair CPU conclusions remain separate from this external baseline.

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 6.003–6.019 ms | 4.501 ms | 1.33–1.34× | Diagnostic; 0.27% / 0.98% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.026–4.058 ms | 3.395 ms | 1.19–1.20× | Diagnostic; 0.81% / 1.14% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.996–2.015 ms | 1.258 ms | 1.59–1.60× | Diagnostic; 0.98% / 1.07% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.312–4.319 ms | 4.218 ms | 1.02–1.02× | Diagnostic; 0.15% / 1.21% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.580–3.680 ms | 2.455 ms | 1.46–1.50× | Variable; 2.71% / 3.77% |
| Configuration read/edit/write | 16 paths / 181 B | 143.494–145.169 µs | 125.252 µs | 1.15–1.16× | Diagnostic; 1.17% / 1.69% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 297.297–311.737 µs | 209.917 µs | 1.42–1.49× | Variable; 4.86% / 1.63% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 382.472–384.928 µs | 258.757 µs | 1.48–1.49× | Diagnostic; 0.64% / 1.38% |

Warm execution:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.718–5.985 ms | 4.355 ms | 1.31–1.37× | Variable; 4.67% / 1.28% |
| CSV summary/ordinary helper | 3.861–3.888 ms | 3.230 ms | 1.20–1.20× | Diagnostic; 0.69% / 0.55% |
| Text summary/helpers | 1.857–1.903 ms | 1.178 ms | 1.58–1.62× | Variable; 2.50% / 0.75% |
| JSON-lines/helpers | 4.047–4.172 ms | 4.038 ms | 1.00–1.03× | Variable; 3.00% / 0.80% |
| JSON-to-Markdown report | 3.510–3.531 ms | 2.350 ms | 1.49–1.50× | Variable; 0.60% / 4.27% |
| Configuration read/edit/write | 79.649–80.620 µs | 61.833 µs | 1.29–1.30× | Diagnostic; 1.22% / 1.81% |
| Protected file search | 225.616–230.023 µs | 137.392 µs | 1.64–1.67× | Diagnostic; 1.92% / 1.95% |
| Two-file copy/checksum/manifest | 244.281–244.449 µs | 154.699 µs | 1.58–1.58× | Diagnostic; 0.07% / 2.83% |
| Text summary/rsplit helper | 2.129–2.135 ms | 1.184 ms | 1.80–1.80× | Diagnostic; 0.30% / 0.92% |
| Text summary/guarded helper | 2.557–2.563 ms | 1.190 ms | 2.15–2.15× | Diagnostic; 0.23% / 0.75% |

Compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 69.648–71.979 µs | 65.551 µs | 1.06–1.10× | Variable; 3.35% / 4.53% |
| CSV summary/ordinary helper | 60.209–60.560 µs | 59.245 µs | 1.02–1.02× | Diagnostic; 0.58% / 1.63% |
| Text summary/helpers | 44.772–45.612 µs | 41.519 µs | 1.08–1.10× | Diagnostic; 1.88% / 1.26% |
| JSON-lines/helpers | 51.985–52.289 µs | 51.083 µs | 1.02–1.02× | Diagnostic; 0.58% / 0.99% |
| JSON-to-Markdown report | 41.707–42.009 µs | 37.921 µs | 1.10–1.11× | Diagnostic; 0.72% / 0.86% |
| Configuration read/edit/write | 38.179–39.154 µs | 34.355 µs | 1.11–1.14× | Variable; 2.49% / 0.70% |
| Protected file search | 29.395–29.495 µs | 30.740 µs | 0.96–0.96× | Diagnostic; 0.34% / 2.32% |
| Two-file copy/checksum/manifest | 72.170–73.486 µs | 56.331 µs | 1.28–1.30× | Diagnostic; 1.79% / 1.62% |

[Summary](summary.json), [raw evidence](observations.tgz), [receipt](archive-receipt.json), [core provenance](core-source.json), [historical guide](prior-BENCHMARKS.md).
