# B1974 CPython comparison for the current shipment scripts

Lython `486bad1ee` and stock CPython 3.13.16 ran on November 28, 2026
UTC on Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The Lython
worker uses the optional signed PythonRe 0.5.0 adapter. Both scripts read
2,048 CSV records in 27,350 bytes.

| Workload | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary helper and generator reduction | Warm sync | 1.93244–1.93536 ms | 1.99218–2.00428 ms | 0.964–0.971× | 0.957–0.970× |
| Ordinary helper and generator reduction | Warm async | 2.06745–2.07664 ms | 1.96441–1.98017 ms | 1.044–1.057× | 1.046–1.080× |
| Repeated explicit tuple loop | Warm async | 1.08045–1.32569 ms | 1.60263–1.61390 ms | 0.669–0.827× | 0.695–0.853× |
| Ordinary helper and generator reduction | Sync compile to result | 1.94945–1.95413 ms | 2.06975–2.07501 ms | 0.939–0.944× | 0.930–0.940× |

Ratios divide Lython by CPython and span pairings of medians from two
workers per engine. Warm elapsed includes RAM-backed I/O, output and
file verification, and previous-result disposal. Lython mediates I/O
and uses shared admission. CPython uses native file APIs with
`-I -S -X utf8`. CPU also includes fixture reset and request framing.
Compile to result includes compilation and compiled-owner cleanup.
All workers use warmed processes. The explicit-loop Lython workers
differ by 22.70% in elapsed, leaving its exact ratio uncertain.
The generator rows have at most 0.44% Lython elapsed spread.

Both quiet gates, both output and file goldens, and all 112 batches pass.
Staging reuses the verified 32-file B1973 image, whose source patch matches
`486bad1ee`. The workers join, the RAM-backed fixtures are removed, and
the persistent archive verifies. The matched B1972 and B1973 comparisons
preserve the individual changes and the adverse controls.
[B1972](../2026-11-28-fused-private-generator-sum/README.md) ·
[B1973](../2026-11-28-private-record-comparison-yield/README.md).

[Timing summary](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
