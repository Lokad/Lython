# R218 — current JSON/report checkpoint against CPython

Measured 2026-11-11 on the quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45
vCPUs, 16 GiB/no swap; actual .NET 11 RC3 and isolated CPython 3.13.16, ordinary
GC/tiering. Runtime **e122bdf6787bdf46e600a328b4e73ddd00766a5c**, fresh forced postcommit
build **7714144f228f01e5290207b178b6c795639bd17c**, core SHA256
**9dadd59bd75528186c66a9a95e2efcf411e611e0ccaa718ff38c4facdb191e33**, assembly 0.11.0.
All 568 normalized production sources match the committed runtime. Both target
builds pass without warnings; focused JSON checks pass 193 internal / 811 public per
actual .NET 11/10. [Source proof](source-identity.json). Full platform qualification is
pending against 7714144f in [CI 42931921242](https://github.com/Lokad/Lython/actions/runs/42931921242), independently of these observations.
An earlier mistaken dispatch 42928757271 targeted b8b05ccc; cancellation was requested and
does not qualify this source. Original measurement identities retain their then
pending run number; no raw observation was rewritten.

Six jobs complete in **165.178 seconds**: the two unchanged
R197 scripts in compile, warm and compile-run phases. Four independent processes
per job (two per engine), 5 s warmups and seven rotated 150 ms rounds. Worker fec79afd,
optional adapter 0.4 and the existing protocol remain fixed. Every identity,
golden, ownership balance, quiet endpoint, Closed/exit/join, fixture-removal and
archive/SHA gate passes. No workers or RAM fixtures remain; terminal is cached.

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| JSON-to-Markdown report / compile | 46.5–46.9 µs | 41.1–41.4 µs | 1.12–1.14× | `e122` | Diagnostic; L0.96%, P0.66% / 2.48% |
| JSON-to-Markdown report / warm | 3.312–3.356 ms | 2.739–2.796 ms | 1.18–1.23× | `e122` | Variable; L1.31%, P2.10% / 6.35% |
| JSON-to-Markdown report / compile-run | 3.309–3.322 ms | 2.950–3.049 ms | 1.09–1.13× | `e122` | Variable; L0.39%, P3.35% / 6.61% |
| JSON-lines/helpers / compile | 53.2–55.2 µs | 51.4–52.0 µs | 1.02–1.08× | `e122` | Variable; L3.81%, P1.18% / 5.75% |
| JSON-lines/helpers / warm | 3.775–3.862 ms | 4.322–4.389 ms | 0.86–0.89× | `e122` | Variable; L2.32%, P1.54% / 3.28% |
| JSON-lines/helpers / compile-run | 3.861–3.939 ms | 4.470–4.495 ms | 0.86–0.88× | `e122` | Variable; L1.97%, P0.56% / 5.66% |

Report input: 3,072 records / 189,441 B; JSON-lines: 4,096 records / 110,592 B. Time ranges
span two process medians per engine; ratios include all four pairings, not
confidence intervals. Ratio above one means Lython slower. Diagnostic/Variable
describe observations, not design vetoes. Report remains slower and JSON-lines
faster in this diagnostic checkpoint; variation limits precision. This is not
single-change attribution, a universal CPU gain or resolution of earlier adversity.

Warm/compile-run include mediated RAM-file I/O, complete per-invocation stdout/file
checks and prior-result cleanup. Compile-run includes compilation in an already
running host; compile measures construction/disposal. Fixture staging/reset,
separate controller verification, protocol, process startup and final cache
teardown are excluded. External process CPU includes surrounding work and coarse
ticks. Independent phase medians need not add. Pending I/O, RSS and embedding tail
latency remain unmeasured.

[Raw observations and exact loaded images](observations.tgz),
[medians, clocks, IQR and all pairings](summary.json),
[receipt](archive-receipt.json), [cached terminal](terminal-status.json),
[preparation and final frozen core](preparation.tar.xz),
[archive hashes](archive-identity.json).
The inherited retrieval print incorrectly said “nine-job”; payload, assertions,
receipt and raw records consistently contain six. Original print preserved in
preparation; local recipe corrected without rerunning timing.

[Recovery](space-recovery.json) preserves the fixed 240 MiB floor / 12 MiB setup
headroom. The qualified baseline advances to exact b8b05ccc/696 bytes only after
[committed archive and source/config equivalence proof](qualified-baseline-promotion.json).
Superseded 0e aliases and pre-cleanup measured JSON prototype aliases, plus the B565
compressed duplicate, are retired only after canonical/all-alias/UID/path/size/SHA
checks. Do not probe retired paths again. Final free space 266,706,944 B.

The [prior guide](prior-BENCHMARKS.md) preserves replaced 8d44 measurements and
their uncertainty. CSV/text and other historical rows retain their own sources.
Publication remains postponed; whole-script runtime and independent regex work
continue. Final adapter qualification is not implied.
