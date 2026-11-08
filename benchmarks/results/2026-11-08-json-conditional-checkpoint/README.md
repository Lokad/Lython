# Retained JSON construction and helper checkpoint against CPython

2026-11-08. Committed production `7265538c`, frozen core `1aa68ce9`; complete
source inventory matches the measured binary. Same28-job R103 catalog, scripts,
fixtures/full stdout/file goldens, worker7cf37ca5 and optional0.4 adapter. No new
framework or producer qualification. Two identical independent Lython processes
and isolated CPython3.13.16, actual.NET11RC3, Ubuntu24.04.4/AzureF4as_v7,
four EPYC9V45vCPUs,16GiB/no swap. NormalGC/tiering, five-second warmup perrole/job,
seven rotated150msrounds. Full source qualification passes in [cached CI42496463028](../2026-11-08-native-conditional-helpers-final/delivery-ci.json); qualification is distinct from external ratios.

All28jobs completed in581.729s. Source/core/aux/Ready/full
golden/quiet-before-and-after/Closed/exit/join/fixture/archive/local gates pass.
No remaining users/fixtures. Reuse the exact B465core/archive; persistent evidence
separate from owned128MiBRAM fixtures, archived under lease and locally verified.
Fresh exact-byte hardlinks recovered27807744B, preserving seven original paths,
all alias/content checks and every archive.240MiBOSfloor+12MiBpayload/raw/archive
headroom; actual payload rounded3260416B. No evidence deletion.

Compilation includes construction/disposal. Warm reuses code and includes execution,
mediated RAM-file I/O, complete output validation and prior-result cleanup.
Compile-to-result adds compilation to one execution in an already-running host.
Startup, fixture staging/reset, protocol and final cached teardown excluded.
CPython caches imports; Lython isolates execution modules. Four additional helper/
JSON-normalization warm rows have no compile/compile-to-result measurement here.
No wall-I/O decomposition, genuine pending-I/O latency, heap/RSS or embedding-tail
claim. Independent phase medians must not be subtracted as causal components.

Ratio=Lython elapsed/CPython elapsed; above1 means slower. Ranges span two process
medians, not confidence intervals. Diagnostic/Variable labels describe short
observations; keep controls and adverse cases. This combines retained R105/R106
milestones, not isolated speedup attribution or repayment of old regression debt.
The broader complete-script objective stays open; prior named-source guide retained.
The [current report profile](../2026-11-08-current-report-attribution/README.md)
finds useful formatting/JSON/dictionary work, not dominant context construction.
Preserve R26's correctness removal of upfront native formatting proofs and the
slower arena/control lessons. No speculative value/cache/second-engine project.

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.547–5.551 ms | 4.454 ms | 1.25–1.25× | Diagnostic; 0.06% / 1.00% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.570–3.633 ms | 3.418 ms | 1.04–1.06× | Diagnostic; 1.73% / 0.65% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.937–1.944 ms | 1.280 ms | 1.51–1.52× | Diagnostic; 0.38% / 0.74% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.937–3.951 ms | 4.237 ms | 0.93–0.93× | Diagnostic; 0.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.433–3.539 ms | 2.489 ms | 1.38–1.42× | Variable; 3.01% / 2.84% |
| Configuration read/edit/write | 16 paths / 181 B | 141.705–142.868 µs | 122.668 µs | 1.16–1.16× | Diagnostic; 0.82% / 2.92% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 296.982–299.900 µs | 199.115 µs | 1.49–1.51× | Diagnostic; 0.97% / 1.77% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 384.150–385.131 µs | 260.100 µs | 1.48–1.48× | Diagnostic; 0.26% / 1.43% |

Warm execution:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.314–5.359 ms | 4.370 ms | 1.22–1.23× | Diagnostic; 0.83% / 0.85% |
| CSV summary/ordinary helper | 3.530–3.543 ms | 3.184 ms | 1.11–1.11× | Diagnostic; 0.36% / 1.88% |
| Text summary/helpers | 1.810–1.898 ms | 1.169 ms | 1.55–1.62× | Variable; 4.85% / 0.54% |
| JSON-lines/helpers | 3.776–3.815 ms | 4.103 ms | 0.92–0.93× | Diagnostic; 1.03% / 1.00% |
| JSON-to-Markdown report | 3.335–3.520 ms | 2.329 ms | 1.43–1.51× | Variable; 5.55% / 2.52% |
| Configuration read/edit/write | 78.349–78.497 µs | 61.938 µs | 1.26–1.27× | Diagnostic; 0.19% / 2.88% |
| Protected file search | 227.063–228.463 µs | 135.112 µs | 1.68–1.69× | Diagnostic; 0.61% / 2.78% |
| Two-file copy/checksum/manifest | 241.782–242.153 µs | 156.779 µs | 1.54–1.54× | Diagnostic; 0.15% / 1.54% |
| Text summary/rsplit helper | 1.827–1.876 ms | 1.181 ms | 1.55–1.59× | Variable; 2.70% / 0.84% |
| Text summary/guarded helper | 2.079–2.106 ms | 1.182 ms | 1.76–1.78× | Diagnostic; 1.33% / 1.05% |
| JSON normalization/eager output | 16.462–16.721 ms | 13.384 ms | 1.23–1.25× | Diagnostic; 1.58% / 2.09% |
| JSON normalization/streamed output | 17.061–17.136 ms | 27.784 ms | 0.61–0.62× | Diagnostic; 0.44% / 2.65% |

Compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 72.002–72.299 µs | 65.506 µs | 1.10–1.10× | Diagnostic; 0.41% / 0.96% |
| CSV summary/ordinary helper | 61.390–61.473 µs | 59.259 µs | 1.04–1.04× | Diagnostic; 0.13% / 0.85% |
| Text summary/helpers | 44.963–45.409 µs | 41.244 µs | 1.09–1.10× | Diagnostic; 0.99% / 1.12% |
| JSON-lines/helpers | 52.781–53.146 µs | 51.136 µs | 1.03–1.04× | Diagnostic; 0.69% / 0.53% |
| JSON-to-Markdown report | 42.538–42.996 µs | 37.917 µs | 1.12–1.13× | Diagnostic; 1.07% / 1.47% |
| Configuration read/edit/write | 38.855–38.904 µs | 34.574 µs | 1.12–1.13× | Diagnostic; 0.13% / 1.81% |
| Protected file search | 29.648–29.656 µs | 30.872 µs | 0.96–0.96× | Diagnostic; 0.03% / 0.81% |
| Two-file copy/checksum/manifest | 73.293–74.183 µs | 56.767 µs | 1.29–1.31× | Diagnostic; 1.20% / 0.90% |

[Summary](summary.json), [raw](observations.tgz), [receipt](archive-receipt.json), [core provenance](core-source.json), [sizing](sizing.json), [deduplication proof](dedup-proof.json), [previous guide](prior-BENCHMARKS.md).
