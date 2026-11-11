# R223 — current ordinary and captured-helper text versus CPython

2026-11-11, qualified committed `4625d83e`, frozen core `cb03fd47…`, all568
production-source hashes match. [Source](source-identity.json). This is the R221
source, not later R222/HEAD. Combined Windows/Ubuntu, actual.NET11/.NET10, original
stress and installed Release consumers are GREEN in
[42942735331](../2026-11-11-incoming-lexical-stores/delivery-ci.json); no repeat query.

Six complete-script jobs finish in **164.378 seconds**. All identity, output/file
digests, stable shared allowance, quiet, Closed/zero-exit/join, RAM cleanup and
archive-SHA gates pass. [All medians, IQR and wider CPU](summary.json),
[raw workers/catalog/observations](observations.tgz), [receipt](archive-receipt.json).

| Complete 3,072-line /35,840 B script | Phase | Lython | CPython | Lython / CPython | Status; L/P control /max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| Ordinary text helpers | Compile | 44.57–44.83 µs | 41.35–41.36 µs | 1.08–1.08× | Diagnostic;0.58%/0.03% /0.93% |
| Ordinary text helpers | Warm | 1.650–1.658 ms | 1.158–1.169 ms | 1.41–1.43× | Diagnostic;0.48%/0.98% /1.91% |
| Ordinary text helpers | Compile-to-result | 1.774–1.784 ms | 1.272–1.310 ms | 1.35–1.40× | Variable;0.54%/2.94% /2.48% |
| Captured helper + nonlocal counter | Compile | 44.16–44.60 µs | 53.65–53.65 µs | 0.82–0.83× | Diagnostic;1.00%/0.01% /2.10% |
| Captured helper + nonlocal counter | Warm | 2.303–2.308 ms | 0.8825–0.8839 ms | 2.61–2.62× | Diagnostic;0.21%/0.15% /1.84% |
| Captured helper + nonlocal counter | Compile-to-result | 2.395–2.441 ms | 1.031–1.046 ms | 2.29–2.37× | Diagnostic;1.95%/1.35% /2.10% |

Ratio is elapsed Lython/CPython; above one means slower. Ranges span independent
process medians per engine and all four pairings, not confidence intervals.
Diagnostic/Variable report observed stability, not fixed design acceptance rules.

Ordinary text's source/fixture/output hashes equal the earlier e543 checkpoint;
[exact proof](prior-case-proof.json). The second script is the R221 labelled
main-wrapped helper/counter variant with a final observable progress assertion,
the same complete input and output files, and ordinary whitespace splitting.
Each engine runs identical source and checks. It differs from the ordinary module-
scope script: wrapping changes locality and the counter changes helper work. Their
cross-script difference is not an isolated counter-cost comparison.

The captured helper still has a substantial external gap despite R221's separately
demonstrated allocation and within-Lython elapsed reductions. Compilation does not
explain this warm gap. Existing ordinary-text attribution does not establish the
changed helper's costs. The next bounded question is complete call/frame/operand
construction on this actual script, rather than assuming the old native-helper
profile applies or extending its native proof with another narrow special case.
No whole-script objective is declared complete.

## Machine and execution boundary

Existing quiet Azure F4as_v7, Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,16GiB/no swap.
Actual.NET11RC3 and stock isolated CPython3.13.16, ordinaryGC/tiering; fixedfec79afd
worker, unchanged optional adapter0.4 and host/protocol. Four independent engine/
control processes per job,5swarmups and7rotated150msrounds. No producer promotion.

Warm and compile-to-result include mediated RAM-file I/O, complete stdout/file
validation and prior-result cleanup. Fixture reset, separate verification/protocol,
startup and final script/cache teardown are excluded. Compilation includes code
construction/disposal under the shared allowance. Phase medians need not add.
Wider process CPU includes surrounding work and coarse ticks. These are embedding
scripts, not isolated CPU kernels; no pending-I/O, RSS or embedding-tail claim.
Previous [e543 text observations](../2026-11-10-construction-cpython-checkpoint/README.md)
and all R221 adversity survive. This state comparison cannot attribute one change.

## Ownership and original setup conditions

The210sowner/30stop/30margin collection was observed only after its bound. Terminal
service success/zero PID, all workers including CPython joined and fixtures removed
are cached; do not query again. [Terminal](terminal-status.json). Final free space
262,844,416 B preserves240MiBfloor. Setup preserved12MiBheadroom.

Only three compressed remote duplicates (B566/B567/B463) were retired after fresh
idle lease, path/UID/link/size/SHA checks against committed canonical archives;
protected qualified B565/reference and B566/candidate binaries are unchanged.
[Exact archive copies](exact-copies.json), [recovery](space-recovery.json).
Those retired remote archive paths are not probed again.

Original local preparation used the wrong existing source-map field, followed by
a quoting/mkdir correction failure. The first idle check matched its own inline
Python command. These failures precede worker launch and remain in preparation
with their correction: use the canonical568hash map; exclude only the inventory's
own PID. No timing/image/threshold changes or collection restart. Original failed
scripts/stderr and all later guards remain preserved.

[Preparation, source proof, recipes and original failures](preparation.tar.xz),
[archive hashes](archive-identity.json). Publication remains postponed; R222's
later diagnostic fix and the independent regex final-producer track stay separate.
