# Combined runtime checkpoint against CPython

2026-11-08. Production91f15a3e, frozen4c072fed core, informational SourceLink71445454;
complete production inventory matches committed source. Unchanged worker7cf37ca5,
optional0.4 adapter, CPython3.13.16 and .NET11RC3 on the existing quiet Ubuntu24.04.4
F4as_v7 (four EPYC9V45vCPUs,16GiB/no swap). Normal GC/tiering, five-second warmup
per role/job, seven rotated150ms rounds, two identical Lython processes/one CPython.

26jobs/539.117seconds: eight maintained complete scripts compile/warm/compile-
to-result, plus two explicit ordinary helper text warm cases. Execution includes
mediated RAM-file I/O, complete stdout/files validation and prior-result cleanup;
compile includes construction/disposal. Startup/staging/reset/protocol/final cache
teardown excluded. Pending-I/O latency, RSS/embedding-tail and individual optimization
CPU attribution remain unmeasured. Imports persist in CPython; Lython execution
modules remain isolated. Filesystem mediation/path checks are included in Lython.

All source/core/auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture/
archive gates pass. Persistent evidence stays outside owned128MiB RAM fixtures;
workers and cleanup join, then fixtures are removed/unmounted, then archival is
verified under lease and independently locally.570s owner+30stop+30margin, no VM
data deletion. Focused332internal/155public per actual11/10 pass; final source
qualification CI42451722109 remains an independent delivery record.

Ratios are Lython elapsed/CPython elapsed; ranges span two Lython process medians,
not confidence intervals. Variable cases remain visible; labels are diagnostics,
not fixed design vetoes. This checkpoint does not repay earlier source-pair debt.

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.971–6.085 ms | 4.446 ms | 1.34–1.37× | Diagnostic; 1.91% / 2.33% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.954–3.983 ms | 3.398 ms | 1.16–1.17× | Diagnostic; 0.74% / 1.31% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.027–2.092 ms | 1.282 ms | 1.58–1.63× | Variable; 3.20% / 0.79% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.293–4.315 ms | 4.256 ms | 1.01–1.01× | Diagnostic; 0.51% / 1.05% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.530–3.688 ms | 2.478 ms | 1.42–1.49× | Variable; 4.31% / 3.18% |
| Configuration read/edit/write | 16 paths / 181 B | 142.254–142.958 µs | 124.868 µs | 1.14–1.14× | Variable; 0.49% / 3.11% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 298.866–300.182 µs | 211.686 µs | 1.41–1.42× | Diagnostic; 0.44% / 1.48% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 384.468–387.899 µs | 260.080 µs | 1.48–1.49× | Diagnostic; 0.88% / 1.27% |

Warm execution:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.726–5.887 ms | 4.327 ms | 1.32–1.36× | Variable; 2.72% / 0.52% |
| CSV summary/ordinary helper | 3.835–3.928 ms | 3.249 ms | 1.18–1.21× | Variable; 2.43% / 0.72% |
| Text summary/helpers | 1.887–1.961 ms | 1.203 ms | 1.57–1.63× | Variable; 3.92% / 2.14% |
| JSON-lines/helpers | 3.988–4.074 ms | 4.067 ms | 0.98–1.00× | Variable; 2.11% / 0.69% |
| JSON-to-Markdown report | 3.510–3.616 ms | 2.335 ms | 1.50–1.55× | Variable; 2.92% / 4.22% |
| Configuration read/edit/write | 79.865–79.924 µs | 62.897 µs | 1.27–1.27× | Diagnostic; 0.07% / 1.81% |
| Protected file search | 226.616–227.012 µs | 139.601 µs | 1.62–1.63× | Diagnostic; 0.17% / 1.14% |
| Two-file copy/checksum/manifest | 242.192–243.121 µs | 155.211 µs | 1.56–1.57× | Diagnostic; 0.38% / 1.42% |
| Text summary/rsplit helper | 2.527–2.583 ms | 1.160 ms | 2.18–2.23× | Variable; 2.19% / 0.99% |
| Text summary/guarded helper | 2.546–2.594 ms | 1.201 ms | 2.12–2.16× | Diagnostic; 1.84% / 1.06% |

Compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 70.268–71.335 µs | 65.395 µs | 1.07–1.09× | Diagnostic; 1.50% / 2.06% |
| CSV summary/ordinary helper | 61.180–61.386 µs | 59.353 µs | 1.03–1.03× | Diagnostic; 0.34% / 0.91% |
| Text summary/helpers | 44.740–45.281 µs | 41.415 µs | 1.08–1.09× | Diagnostic; 1.21% / 0.78% |
| JSON-lines/helpers | 51.942–52.730 µs | 50.842 µs | 1.02–1.04× | Diagnostic; 1.50% / 0.82% |
| JSON-to-Markdown report | 41.648–41.705 µs | 37.884 µs | 1.10–1.10× | Diagnostic; 0.14% / 1.54% |
| Configuration read/edit/write | 38.453–38.581 µs | 34.349 µs | 1.12–1.12× | Diagnostic; 0.33% / 1.62% |
| Protected file search | 29.125–29.228 µs | 30.886 µs | 0.94–0.95× | Diagnostic; 0.35% / 1.37% |
| Two-file copy/checksum/manifest | 71.368–72.327 µs | 56.088 µs | 1.27–1.29× | Diagnostic; 1.34% / 0.90% |

[Summary](summary.json), [full raw evidence](observations.tgz), [receipt](archive-receipt.json), [historical guide](prior-BENCHMARKS.md).
