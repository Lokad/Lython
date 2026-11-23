# B1525 supplied arithmetic after removing the early caller boxes

Retained `faf86299e` and stock CPython 3.13.16 run the same 178-byte
script with 2,048 fully supplied arithmetic calls. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm | 0.18920–0.18999 ms | 0.20765–0.21190 ms | 0.893–0.915× |
| Compile to result | 0.20860–0.21017 ms | 0.23223–0.24230 ms | 0.861–0.905× |

Ratios divide Lython elapsed by CPython elapsed across four pairs of
process medians. Warm worker spreads are 0.41% for Lython and 2.05% for
CPython. Time-to-result spreads are 0.75% and 4.34% respectively.
Warm execution reuses code and includes output verification and prior
result disposal. Time to result includes compilation and code disposal.
The workload makes no file calls. Startup and request framing sit outside
both clocks. Independent workers measured the two phases.

Two workers per engine contribute seven rotated batches after eight seconds
of warmup each. Both images match the golden output. All 56 batches,
both quiet gates, full image hashes, worker joins, fixture cleanup and the
archive verify. The initial staging attempt stopped before workers started
when it checked the older reference image. The procedures preserve that
failure and the corrected image selection.

[B1524](../2026-11-23-supplied-operand-window/README.md) measures the
runtime change against its immediate Lython reference and shows the controls.
[B1523](../2026-11-23-integer-call-update-cpython/README.md) preserves the
earlier external results. The new result applies to the measured module
script. The function-scoped variant and other workloads need their own
measurements.

[Identity](identity.json) · [Summary](summary.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
