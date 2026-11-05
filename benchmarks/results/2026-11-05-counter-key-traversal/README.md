# Counter key traversal — retained simplification and corrections

2026-11-05, source `f66427f1` plus E3a; reference frozen `793586f5`, final
core `48f4690c`. [Source verification and focused validation](validation.json).
The current Counter source passes 187 public/104 internal focused Release tests
per actual .NET 11/10. Subsequent combined Windows/Ubuntu, actual11/10, original
Stress and installed consumers passed: [cached terminal42119563175](delivery-ci-status.json), all8 gates. The preceding `f66427f1` delivery is fully
qualified separately; its green result does not qualify E3a.

Four temporary key HashSets, three unions and seen-key storage disappear.
Comparison visits both live sources, resolving keys through Python dictionary
protocols; count equality runs even for aliased values and preserves NaN behavior.
Ordinary Counter ordering now reaches the implemented semantics; greater ordering
preserves operand and iteration order. Async value comparison/truth can suspend.
The active source backing stays conservatively funded through callbacks and
awaited cleanup, including after a callback clears it. This can require another
backing's worth of shared allowance even without mutation. No new policy, owner,
storage backend or reclamation mechanism is introduced.

## Bounded comparison

Quiet Azure VM, Ubuntu24.04.4, four exposed AMD EPYC9V45 CPUs, ordinary tiering/GC;
CPython3.13.16 GIL release and .NET11 RC3. Existing frozen comparison worker
`b94fa98c`, protocol2. Each of two jobs uses five seconds warmup per role and seven
rotated150ms batches: candidate, independent identical-current control, prior
Lython and CPython. Full output/file checks, quiet gates, successful worker joins
and fixture cleanup pass. The prototype took59.03s; final identity-equality
correction took59.12s. All original measurements remain preserved.

| Final warm workload | Prior Lython | Final Lython | Current control | CPython | Candidate/control versus prior |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native Counter algebra,128 keys,8 helper iterations |1.172280ms|1.047765ms|1.030455ms|0.570109ms|−10.62%/−12.10%|
| Complete CSV helper summary,4,096 records |4.950345ms|4.991871ms|4.922969ms|3.387749ms|+0.84%/−0.55%|

Counter current-role spread1.65%, candidate/control/prior IQR0.35%/0.95%/0.77%.
The final Lython/CPython ratio is1.84 for this diagnostic Counter script and1.47
for CSV. The prototype Counter improvement was−13.20%/−12.91%; it predates the
required equality correction and is not the final result. Prototype CSV−2.98%/
−2.77% changes sign in the final block while the unchanged reference moves2.76%.
Treat complete CSV as mixed; do not attribute it to Counter work. No broad CPU,
allocation, startup, compilation, RSS or embedding-latency claim is established.

Warm clocks include invocation, mediated RAM I/O, full validation and prior-result
cleanup. They exclude compilation/startup/staging/reset/transport and final
program/result/process/fixture teardown. Incorrect custom-callback baselines are
excluded from performance claims; the native-key algebra output is equivalent.
This does not replace the representative fresh CPython checkpoint.

[Prototype observations](prototype/observations.tgz), [final observations](final/observations.tgz),
[final summary](final/summary.json), [contract and implementation discoveries](final/source-contract.md).
Archives contain exact source snapshots, identities, workers, catalogs, all rounds
and machine/service receipts. Raw hashes appear in their archive receipts.

## Correctness history and remaining work

[Correctness history](correctness-history.tgz) preserves the frontend LA3143
rejection, independent equality-only baseline proof, first missing-context binary
prototype, runtime-ordering failure, build corrections and final six CPython3.13.2
goldens. Regressions cover equal custom keys, both effectful comparison passes,
source mutation, missing counts, ordering direction, aliased counts and NaN.
Six true-suspension cases check comparison/truth clearing both inputs, retained
backing funding, cancellation and awaited cleanup. Native swallowed refusal and
cancellation cannot escape through a false comparison; refusal before traversal
prevents comparison effects and restores the borrowed balance.

E3b remains open: mapping-rest and view comparisons still use structural semantic
sets, and public compound hashes need proof before changing global key storage.
Structural dataclass storage can still repeat attribute reads or reject a valid
custom hash with structurally unhashable fields. This bounded Counter migration
removes one dependency; it does not close the storage redesign or whole-script
performance objective. Publication remains postponed.
