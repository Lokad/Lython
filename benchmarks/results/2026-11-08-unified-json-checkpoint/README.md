# Unified-JSON checkpoint against CPython

2026-11-08. Committed production15c06863, frozen1674a06c; complete source
inventory matches the measured core.28jobs: the maintained26-job R97 catalog,
plus eager/streamed JSON normalization warm execution. Same worker7cf37ca5,
optional0.4 adapter, CPython3.13.16/.NET11RC3, Ubuntu24.04.4/AzureF4as_v7,
four EPYC9V45vCPUs,16GiB/noswap. NormalGC/tiering;5swarmup perrole/job,
7rotated150msrounds, two identical Lython processes and isolated CPython.
No new framework/producer qualification. JSON scripts independently regenerated
full stdout/file goldens before collection and are identical across both engines.

All28jobs completed in581.936s. Source/core/aux/Ready/
golden/quiet-before-and-after/Closed/exit/join/fixture/archive/local gates pass.
No remaining users/fixtures. B461 exact core/archive reused, persistent raw data,
12MiB payload/raw/archive allowance above240MiBOSfloor. Actual payload rounded
3260416B. No evidence/core/path deletion/deduplication during this checkpoint.
Full source qualification passes at [cached CI42484943603](../2026-11-08-unified-json-encoding/delivery-ci.json); its receipt is independent of external ratios.

Compile includes construction/disposal; warm reuses code and includes full
execution, mediated RAM I/O, stdout/file validation and prior-result cleanup.
Compile-to-result includes compilation plus one execution in an already-running
host. Startup, fixture staging/reset, protocol and final cached teardown excluded.
CPython caches imports; Lython isolates execution modules. No wall-I/O breakdown,
genuine pending-I/O latency, heap/RSS or embedding-tail claim. JSON eager/streamed
additional warm rows have no compile/compile-to-result measurements here.

Ratio=Lython elapsed/CPython elapsed; >1 means slower. Ranges span two independent
Lython medians, not confidence intervals. Diagnostic/Variable labels describe
short observations; controls/adverse cases remain visible. This combined
R98/R99/R102 checkpoint does not isolate causal gains, repay earlier R95/R96 debt
or the accepted R102 eager source-pair allocation/CPU regression. It does not
close the broader performance objective. Prior named-source guide is preserved.

The [bounded remaining-phase source gate](remaining-phase-source-gate.json) found
lexical facts/final call storage/native UTF8 caches already shared; no speculative
compiler/value/frame/reservation redesign follows merely from allocation ticks.
Dev55 package hashes remain unchanged; no repeat adapter qualification.

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.516–5.581 ms | 4.465 ms | 1.24–1.25× | Diagnostic; 1.19% / 0.82% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.611–3.651 ms | 3.395 ms | 1.06–1.08× | Diagnostic; 1.11% / 1.06% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.979–2.001 ms | 1.293 ms | 1.53–1.55× | Variable; 1.12% / 3.08% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.998–4.007 ms | 4.202 ms | 0.95–0.95× | Diagnostic; 0.22% / 0.93% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.498–3.552 ms | 2.511 ms | 1.39–1.41× | Variable; 1.55% / 4.55% |
| Configuration read/edit/write | 16 paths / 181 B | 141.382–144.008 µs | 123.058 µs | 1.15–1.17× | Diagnostic; 1.86% / 2.81% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 294.135–298.576 µs | 197.468 µs | 1.49–1.51× | Diagnostic; 1.51% / 1.12% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 385.993–387.224 µs | 263.334 µs | 1.47–1.47× | Diagnostic; 0.32% / 1.53% |

Warm execution:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.296–5.345 ms | 4.280 ms | 1.24–1.25× | Diagnostic; 0.93% / 0.94% |
| CSV summary/ordinary helper | 3.447–3.469 ms | 3.214 ms | 1.07–1.08× | Diagnostic; 0.62% / 0.69% |
| Text summary/helpers | 1.851–1.862 ms | 1.165 ms | 1.59–1.60× | Diagnostic; 0.57% / 1.58% |
| JSON-lines/helpers | 3.713–3.754 ms | 4.061 ms | 0.91–0.92× | Diagnostic; 1.10% / 1.31% |
| JSON-to-Markdown report | 3.420–3.500 ms | 2.342 ms | 1.46–1.49× | Variable; 2.27% / 3.02% |
| Configuration read/edit/write | 79.112–80.161 µs | 61.591 µs | 1.28–1.30× | Variable; 1.33% / 3.46% |
| Protected file search | 225.854–229.205 µs | 141.571 µs | 1.60–1.62× | Diagnostic; 1.48% / 1.76% |
| Two-file copy/checksum/manifest | 241.421–243.112 µs | 155.219 µs | 1.56–1.57× | Diagnostic; 0.70% / 1.86% |
| Text summary/rsplit helper | 1.836–1.866 ms | 1.158 ms | 1.59–1.61× | Diagnostic; 1.63% / 1.11% |
| Text summary/guarded helper | 2.349–2.390 ms | 1.186 ms | 1.98–2.02× | Diagnostic; 1.74% / 0.95% |
| JSON normalization/eager output | 18.373–18.568 ms | 12.928 ms | 1.42–1.44× | Diagnostic; 1.05% / 1.64% |
| JSON normalization/streamed output | 18.432–18.474 ms | 27.536 ms | 0.67–0.67× | Diagnostic; 0.22% / 1.30% |

Compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 70.423–72.024 µs | 65.501 µs | 1.08–1.10× | Variable; 2.22% / 1.28% |
| CSV summary/ordinary helper | 61.096–61.127 µs | 59.019 µs | 1.04–1.04× | Diagnostic; 0.05% / 1.72% |
| Text summary/helpers | 45.372–45.606 µs | 41.405 µs | 1.10–1.10× | Diagnostic; 0.52% / 1.12% |
| JSON-lines/helpers | 53.068–53.626 µs | 51.036 µs | 1.04–1.05× | Diagnostic; 1.04% / 1.67% |
| JSON-to-Markdown report | 42.128–42.225 µs | 37.903 µs | 1.11–1.11× | Diagnostic; 0.23% / 1.56% |
| Configuration read/edit/write | 38.481–38.681 µs | 34.764 µs | 1.11–1.11× | Diagnostic; 0.52% / 1.27% |
| Protected file search | 29.135–29.404 µs | 30.928 µs | 0.94–0.95× | Diagnostic; 0.92% / 0.97% |
| Two-file copy/checksum/manifest | 73.107–73.382 µs | 56.398 µs | 1.30–1.30× | Diagnostic; 0.38% / 2.29% |

[Summary](summary.json), [raw](observations.tgz), [receipt](archive-receipt.json), [core provenance](core-source.json), [sizing](sizing.json), [previous guide](prior-BENCHMARKS.md).
