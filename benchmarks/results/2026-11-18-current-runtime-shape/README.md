# Floating and helper-text CPU attribution

R537/B969 measured the retained R536 production source at f7b1c056d, frozen
core 9eed03c9, on the quiet Azure F4as_v7 VM on 2026-11-18 UTC. The same
32-file worker and catalog had passed output and allowance-reuse checks.
The producer remained the optional pinned 0.4.0 adapter. Each complete
script used eight seconds of guest warmup and approximately five seconds
of CPU sampling with managed perf maps and normal GC and tiering.

| Complete script | All-thread samples | Unmapped leaves | Guest capture |
| --- | ---: | ---: | ---: |
| Floating JSON report | 1,032 | 155 | 4.89 s |
| Helper text normalization | 1,019 | 175 | 4.81 s |

The floating capture includes 180 nearest instruction-prefix samples,
96 for JSON construction, 62 for sequence indexing and 37 for numeric
conversion. Helper text includes 106 nearest native-call samples and 53
for `RStrip`. Both scripts include useful work inside dispatch and native
calls. The samples cover wider worker CPU, including fixture reset and
result validation. Inlined code and overlapping groups prevent interpreting
these counts as removable percentages or a before/after comparison.

The source gate leaves call frames unchanged. The guest trim wrapper already
funds custom character sets. Its remaining separable work is choosing default
versus custom direction through separate routes and checking ownership again
after the primitive has either returned a funded slice or reused its input.
R538 tests one bound trim entry with scratch owned by the primitive and the
same admission estimate. Benchmark complete text scripts before retention.

Both quiet checks passed. The worker closed, all users joined, RAM traces
were archived and hash-checked before unmount, and the local archive hash
matched. One B967 raw working replica was retired after matching its exact
committed archive member and all remote alias checks.

[Full attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Ownership](archive-receipt.json) · [Procedures](procedures.tar.xz)
