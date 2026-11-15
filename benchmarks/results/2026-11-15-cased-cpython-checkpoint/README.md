# Affected text CPython checkpoint

2026-11-15 UTC, committed **1b82cd46 / frozen0edc0c1c**, stock **CPython3.13.16**.
Focused actual11/10 checks pass; full43744260423 qualification is pending at capture.
The frozen591 inputs comprise586production,4build inputs and1changed test, matching
committed source after explicit newline normalization. Rebuilt binary identity is
not inferred. Other CPython rows retain their actual earlier source revisions.

| Representative workload / phase | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Text / captured helper and counter / warm Run | 3,072 lines / 35,840 B | 1.213–1.236 ms | 0.891–0.893 ms | 1.36–1.39× | Diagnostic; spreads 1.84/0.19%; IQR ≤1.21% |
| Text / captured helper and counter / compile and execute once | 3,072 lines / 35,840 B | 1.267–1.301 ms | 1.034–1.042 ms | 1.22–1.26× | Diagnostic; spreads 2.67/0.74%; IQR ≤1.04% |
| Text / inline / warm Run | 3,072 lines / 35,840 B | 0.818–0.851 ms | 0.802–0.804 ms | 1.02–1.06× | Diagnostic; spreads 3.91/0.21%; IQR ≤1.71% |

Ratio is Lython elapsed/CPython elapsed, every process-median pairing, not a
confidence interval. Three jobs finish in189.024s on the quiet Azure F4as_v7,
Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,.NET11RC3 workstation GC/normal
tiering. Two independent processes/engine,12s full-request warmup, five rotated
0.5s batches. Maximum unchanged Lython spread3.91% remains visible. All output,
budget, quiet, join, archive and cleanup gates pass; terminal B797 is cached.
No workers or RAM fixtures remain. Original archive SHA is independently verified.

Warm includes synchronous Run, efficient mediated RAM I/O, stdout/file goldens
and prior-result cleanup; excludes compilation/startup, reset/protocol and final
cache teardown. Compile-to-result adds compilation and compiled-owner disposal.
Phase medians are independently warmed and not additive. Wider process CPU also
includes reset/protocol. CPython uses native buffered files without equivalent
host capability/shared-budget checks. No startup, actual pending I/O, heap/RSS,
contention or embedding-tail comparison is established.

The helper version also updates a nonlocal per-line counter and asserts its final
value; its difference from the inline script is not isolated function-call cost.
These are external baseline observations, not causal cross-revision improvements.
The separate [R408 source pair and accepted uncertainty](../2026-11-15-fused-cased-preparation/confirmation/README.md)
retain all earlier adverse controls and the wider4×/6×scratch/earlier denial.

[All process medians](summary.json),[exact image](identity.json),
[raw observations](observations.tgz),[archive custody](archive-receipt.json),
[procedure](procedure.tar.xz),[preceding guide](prior-BENCHMARKS.md).
