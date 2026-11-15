# Closed-word CPython checkpoint

2026-11-15 UTC, committed `ec6c0416` / frozen `6164a933` versus stock CPython
3.13.16; Ubuntu 24.04.4, Azure F4as_v7, four EPYC 9V45 vCPUs, 16 GiB/no swap,
.NET 11 RC3, ordinary workstation GC/tiering and CPython GIL/GC.

| Complete workload / input | Lython warm | CPython warm | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Captured text /3,072 lines,35,840 B |1.244–1.249 ms|.897–.908 ms|1.370–1.392×|Diagnostic; process spreads .43/1.17%, max IQR2.45%|
| JSON-lines helpers /4,096 records,110,592 B |3.447–3.458 ms|4.149–4.159 ms|.829–.833×|Diagnostic; process spreads .32/.22%, max IQR1.24%|

Ratio above one means Lython slower. Two independent processes per engine, five
rotating rounds/all four median pairings, not confidence intervals. Warm clocks
include mediated RAM I/O, full stdout/file verification and prior-result cleanup;
exclude compilation, startup, fixture staging/reset, protocol and final cache
teardown. Wider process CPU includes fixture reset/protocol. Captured-text warmup
is12 seconds/worker, JSON5; full-request batch sizing is unchanged. Compilation
and compile-to-result retain separately named [older CF/957 observations](../2026-11-15-shared-slots-cpython-checkpoint/README.md),
not estimates for this image. No startup, heap or embedding-tail claim.

Both jobs complete100.252 seconds, quiet before/after, exact source/fixture/output/
image identity, all goldens, joined users and removed RAM fixtures. Persistent raw
archive verified under the owned lease. The only VM deletion was an exact committed,
archived duplicate B767 JSON leaf; compressed evidence/all images remain,254/252MiB
floors unchanged. Full local/Git/remote/member custody is in the receipt.

All585 runtime inputs match committed source with explicit newline normalization.
Full43669461343platform/stress/installed-consumer qualification is running, not
claimed green here. Publication remains held. The [R376 source pair](../2026-11-15-closed-word-segment/README.md)
retains JSON+.80..+2.70%elapsed debt and inconclusive fresh magnitude; this external
baseline cannot erase or attribute those observations. All original raw, CPU and
machine observations remain in observations.tgz; no cross-collection causal gain.
