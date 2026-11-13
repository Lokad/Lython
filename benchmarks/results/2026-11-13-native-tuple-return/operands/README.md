# Direct caller operands: allocation gain and remaining report debt

Uncommitted883c8699 versus fully qualified14dd45b7/93a6d134. Tuple returns write
already-admitted final caller operands; live extent tracks all output references
before writes, including wider-than-input results. Exact targets retain existing
reverse operand order. Remove fixed caller scratch, shared buffer declaration and
duplicate span publisher. Escaping/mismatched/starred/suspended results stay normal.
110internal/181public Release11 focused checks plus7public overlap scenarios pass;
the latter includes2new wider/narrower cases, both APIs and actual delayed reads.
Eight full sync scripts pass goldens/shared balances/cleanup. Invoice allocation
−6.41..−6.43%, rich variant−5.05%; other allocation controls effectively flat.
Desktop elapsed remains diagnostic with moving/adverse controls.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Invoice warm |−1.31..−0.43%|−1.92..−1.24%|0.62/0.27%;3.38%|
| Invoice compile + run |−1.61..+1.39%|−1.64..+0.30%|1.26/1.78%;1.56%|
| Captured text warm |−3.05..+2.71%|−3.86..+2.40%|4.62/1.24%;2.12%|
| Report warm |+1.77..+3.04%|+2.68..+4.20%|0.75/0.49%;2.78%|

B668 four jobs180.434s,2026-11-13UTC, same quietF4as_v7/Ubuntu24.04.4/fourEPYC9V45
vCPUs/16GiB/no swap, ordinary.NET11RC3 GC/tiering, fixedfec79worker/adapter0.4.
Two processes/image/five rotating500ms rounds,5swarmup(12captured). Ranges span
four paired medians, not confidence intervals. Warm includes mediated RAM-file
I/O, full stdout/file checks and prior-result cleanup; fresh adds compilation/
compiled-owner disposal. Startup/reset/staging/final cache cleanup are outside;
widerCPU includes reset/protocol. No inferred CPython or embedding-latency claim.

All identity/golden/balance/quiet/join/persistent-SHA archive gates pass; no users
or RAM fixtures remain. To preserve existing floors, retire exact obsolete75 and
historical6b VM working-copy aliases after verifying their frozen bytes in tracked
canonical archives and every inode/UID/hash/all-alias/fresh idle lease. Historical6b
is qualified; its qualification and exact canonical binary remain, and can restore
the retired VM copies. Current93 and published-comparison2e images remain. No raw
evidence or canonical file is deleted; initial missing withdrawn-R300 and alias-
inspection conditions are corrected before staging. Final VM free270684160B.

**Decision: one final state simplification before retain/withdraw.** Direct operands
remove caller scratch but retain an output-span field in every native execution
state. Report debt remains2–3%, while fresh/text are uncertain. Replace that borrowed
span with a scalar requested count/tuple operand start; the synchronous return
wrapper alone receives/copies the caller destination before native cleanup. Keep
the hot instruction loop independent of the result borrow, without another engine,
pool or dispatch layer. This is a source-grounded hypothesis about state/transport,
not proven attribution of the whole regression. Focused semantics/cancellation/
ownership/managed-stack checks then one changed-source comparison must produce an
explicit retention/debt or withdrawal decision. Earlier adverse results survive.

[Scratch phase](../scratch/README.md), [raw](observations.tgz),
[summary](summary.json), [exact sources/bits/checks](source-checks.tar.xz).
