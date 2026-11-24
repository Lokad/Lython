# B1551 Typed Unicode CSV against CPython

Lython `b3c447420`, with short text retained in the typed reader's
admitted slots, and stock CPython 3.13.16 run the same 4,096-record,
223,267-byte meter fixture. Measurements ran November 24, 2026 UTC
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.

| Phase | Lython elapsed | CPython elapsed | Lython / CPython | Worker spread, Lython / CPython |
| --- | ---: | ---: | ---: | ---: |
| Warm | 3.36064–3.36989 ms | 3.09385–3.09655 ms | 1.085–1.089× | 0.28 / 0.09% |
| Compile to result | 3.42689–3.47493 ms | 3.30146–3.34740 ms | 1.024–1.053× | 1.40 / 1.39% |

Ratios divide Lython elapsed by CPython elapsed and span all four
pairings of independent worker medians. Warm reuses compiled code.
Both clocks include RAM-backed reads and writes, output verification,
and prior-result disposal. Compile to result also includes compilation
and compiled-owner disposal. Fixture reset and protocol sit outside
elapsed. Wider process CPU is reported separately. This checkpoint
measures warm and compile-to-result elapsed. The earlier combined
checkpoint records compilation alone for the explicit meter.
[B1549](../2026-11-24-combined-csv-cpython/README.md).

All 56 batches match the original goldens. The complete frozen image
is the retained B1550 candidate. Both machine gates and the persistent
archive verify after workers join and RAM fixtures are removed.
The matched earlier-Lython comparison measures the allocation and CPU
change separately. [B1550](../2026-11-24-typed-csv-text/README.md).

[Image and fixtures](identity.json) · [Timing summary](summary.json) ·
[Raw timing](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
