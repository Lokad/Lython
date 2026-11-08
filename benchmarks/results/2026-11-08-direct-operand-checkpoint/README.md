# Direct-operand checkpoint against CPython

2026-11-08. Committed production94cb8963, frozen d6254b2e; complete production
inventory matches the measured source. Core provenance and full hashes accompany
the raw observations. Same maintained26-job catalog as R90/ac5aea97, worker7cf37ca5,
optional0.4 adapter, CPython3.13.16/.NET11RC3, Ubuntu24.04.4/F4as_v7,
four EPYC9V45 vCPUs,16GiB/no swap. NormalGC/tiering, five-second warmup per
role/job, seven rotated150ms rounds, two identical Lython processes and one
isolated CPython process. No new benchmark framework or producer qualification.

All26jobs completed in539.873s. All Ready/core/auxiliary/
golden/quiet-before-and-after/Closed/exit/join/fixture/archive/local verification
gates pass. Guest RAM fixtures unmounted; no remaining users. Exact immutable
B457 core/archive reused, persistent raw observations, prior measured raw+archive
footprint with25% headroom above240MiB OSfloor. The payload is included in that
total, not counted a second time. No evidence/core/path deletion or deduplication.
Independent full Windows/Ubuntu actual11/10/originalStress/installed consumers
remain pending at94cb8963, CI42475420182.

Command-v2 boundaries unchanged. Compile includes construction/disposal; warm
reuses code and includes execution, full stdout/file checks, mediated RAM-file
I/O and prior-result cleanup. Compile-to-result includes compilation plus execution
inside an already-running host. Fixture staging/reset, startup, transport and final
cache teardown are excluded. CPython caches imports; Lython isolates executions.
No I/O decomposition, genuine suspended-I/O latency, startup, heap/RSS or embedding
tail measurement. Ratio=Lython elapsed/CPython elapsed; >1 means slower. Ranges are
two process medians, not confidence intervals; variable controls remain visible.

This combined source checkpoint establishes current external ratios, not causal
attribution to R92/R95/R96. It does not repay separately recorded R95 text or R96
possible report debt. All old guide tables and adverse controls remain preserved.
The substantial performance objective remains open.

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.525–5.531 ms | 4.453 ms | 1.24–1.24× | Diagnostic; 0.10% / 0.85% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.631–3.640 ms | 3.412 ms | 1.06–1.07× | Diagnostic; 0.24% / 0.68% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.926–1.970 ms | 1.353 ms | 1.42–1.46× | Variable; 2.27% / 4.97% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.920–3.924 ms | 4.234 ms | 0.93–0.93× | Diagnostic; 0.08% / 1.05% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.496–3.618 ms | 2.477 ms | 1.41–1.46× | Variable; 3.35% / 5.04% |
| Configuration read/edit/write | 16 paths / 181 B | 140.780–141.270 µs | 127.407 µs | 1.10–1.11× | Diagnostic; 0.35% / 2.90% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 297.292–298.083 µs | 215.173 µs | 1.38–1.39× | Variable; 0.27% / 4.62% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 389.312–390.919 µs | 263.534 µs | 1.48–1.48× | Variable; 0.41% / 3.23% |

Warm execution:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.292–5.309 ms | 4.261 ms | 1.24–1.25× | Diagnostic; 0.32% / 0.95% |
| CSV summary/ordinary helper | 3.448–3.456 ms | 3.229 ms | 1.07–1.07× | Diagnostic; 0.21% / 0.79% |
| Text summary/helpers | 1.828–1.832 ms | 1.161 ms | 1.57–1.58× | Diagnostic; 0.23% / 1.56% |
| JSON-lines/helpers | 3.710–3.737 ms | 4.111 ms | 0.90–0.91× | Diagnostic; 0.71% / 2.55% |
| JSON-to-Markdown report | 3.447–3.632 ms | 2.323 ms | 1.48–1.56× | Variable; 5.36% / 3.67% |
| Configuration read/edit/write | 79.429–80.714 µs | 61.981 µs | 1.28–1.30× | Variable; 1.59% / 3.92% |
| Protected file search | 228.579–228.835 µs | 136.445 µs | 1.68–1.68× | Diagnostic; 0.11% / 1.91% |
| Two-file copy/checksum/manifest | 244.533–247.022 µs | 155.159 µs | 1.58–1.59× | Diagnostic; 1.02% / 2.57% |
| Text summary/rsplit helper | 2.092–2.097 ms | 1.164 ms | 1.80–1.80× | Diagnostic; 0.23% / 1.23% |
| Text summary/guarded helper | 2.382–2.392 ms | 1.185 ms | 2.01–2.02× | Diagnostic; 0.42% / 0.95% |

Compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 71.620–71.710 µs | 65.617 µs | 1.09–1.09× | Variable; 0.12% / 5.93% |
| CSV summary/ordinary helper | 63.297–64.094 µs | 62.010 µs | 1.02–1.03× | Variable; 1.26% / 4.59% |
| Text summary/helpers | 47.859–48.069 µs | 43.748 µs | 1.09–1.10× | Diagnostic; 0.44% / 1.49% |
| JSON-lines/helpers | 53.142–53.574 µs | 50.465 µs | 1.05–1.06× | Variable; 0.81% / 6.28% |
| JSON-to-Markdown report | 42.581–43.106 µs | 37.786 µs | 1.13–1.14× | Variable; 1.23% / 3.27% |
| Configuration read/edit/write | 38.794–39.259 µs | 34.427 µs | 1.13–1.14× | Variable; 1.20% / 3.50% |
| Protected file search | 29.340–29.904 µs | 31.059 µs | 0.94–0.96× | Variable; 1.88% / 3.25% |
| Two-file copy/checksum/manifest | 73.565–73.971 µs | 56.161 µs | 1.31–1.32× | Diagnostic; 0.55% / 1.40% |

[Summary](summary.json), [raw observations](observations.tgz), [receipt](archive-receipt.json), [core provenance](core-source.json), [sizing](sizing.json), [previous guide](prior-BENCHMARKS.md).
