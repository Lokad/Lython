# B1685 Retained runtime against CPython

This checkpoint measures `c1984f96b` against CPython 3.13.16 on the quiet
Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
The B1682 nonincremental core build matches the committed source patch.
All 32 image files and both toolchains verify. The comparison uses two
processes per engine and confirms all six complete outputs and file goldens.

Each phase warms the four processes for five seconds and runs seven rotated
batches with a 0.46-second target. Warm execution includes applicable
RAM-backed file I/O, verification and prior-result disposal. Lython uses
its mediated file host and active shared allowance. CPython uses native
file APIs. Compilation includes prior compiled-owner disposal. Time to
result includes compilation, execution and cleanup in a running process.
Fixture reset and request framing sit outside elapsed and inside wider CPU.
Process startup and final teardown sit outside these clocks.

| Workload | Phase | Lython | CPython | Elapsed ratio | Wider CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary generated definitions | Warm | 0.31987–0.32584 ms | 0.14894–0.14907 ms | 2.146–2.188× | 2.119–2.202× |
| Ordinary generated definitions | Compilation | 0.57609–0.57730 ms | 0.94939–1.10843 ms | 0.520–0.608× | 0.520–0.623× |
| Ordinary generated definitions | Compile to result | 0.90220–0.90802 ms | 1.11611–1.13688 ms | 0.794–0.814× | 0.783–0.815× |
| Defaulted rule pairs | Warm | 0.89607–0.93387 ms | 0.68145–0.69312 ms | 1.293–1.370× | 1.300–1.386× |
| Defaulted rule pairs | Compilation | 0.02185–0.02207 ms | 0.04137–0.04173 ms | 0.524–0.534× | 0.527–0.545× |
| Defaulted rule pairs | Compile to result | 0.88032–0.93097 ms | 0.72901–0.73349 ms | 1.200–1.277× | 1.185–1.276× |
| Eager annotated rules | Warm | 0.43268–0.43905 ms | 0.40411–0.41482 ms | 1.043–1.086× | 1.046–1.090× |
| Postponed annotated rules | Warm | 0.61825–0.66753 ms | 0.53118–0.53396 ms | 1.158–1.257× | 1.154–1.285× |
| Shipment callback report | Warm | 2.39388–2.39448 ms | 1.62757–1.63316 ms | 1.466–1.471× | 1.460–1.470× |
| Shipment callback report | Compile to result | 2.46506–2.53701 ms | 1.88636–1.90359 ms | 1.295–1.345× | 1.258–1.325× |
| Quote-heavy invoice | Warm | 6.26455–6.60593 ms | 6.28840–6.31990 ms | 0.991–1.050× | 0.986–1.058× |

Ratios divide Lython by CPython across all four process-median pairings.
Ordinary definitions take more than twice as long warm, with lower time to
result. CPython compile-only workers differ by 16.75% on that workload.
Defaulted-rule Lython workers differ by up to 5.75% and postponed workers
by 7.97%. Invoice spans parity. These are diagnostic observations. The
phases are measured independently and cannot be subtracted to attribute
time to an instruction or to file I/O. Earlier [B1677](../2026-11-25-ordinary-defaults-cpython/README.md)
ratios remain archived.

The initial service failed before timing because its collector expected
eight phases while the catalog specified eleven. The corrected collection
uses the same frozen images, inputs, phase list and goldens. It fixes the
phase assertion and sizes batches and the service deadline for eleven
phases. The original failure and all 308 corrected timed batches remain
available. Both quiet gates, worker joins, fixture removal and persistent
archive checks pass.

[Identity](identity.json) · [Timings and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Original failure](initial-failure.json) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
