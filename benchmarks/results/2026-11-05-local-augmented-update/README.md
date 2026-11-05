# Complete local augmented-update pipeline — retained, delivery pending

Measured 2026-11-05, `d946c4e8` plus the archived D61 patch; qualified
`13d69be6` production frozen before commit metadata is the reference. The
subsequent `20abf3cd` commit records that qualification and changes no production
source. See [identity](identity.json), [exact source](source-snapshot.tgz) and
[source contract recorded before code](source-contract.md). No public API change.

The existing operand-forwarding pass combines same-span `Augmented` and
`StoreLocal`. Five existing metadata ints carry the prepared operator, evaluated
sources, local destination and retired extent. Remove intermediate result
publication/take, separate store dispatch and repeated operator remapping. The
enum mapper moves; it is not duplicated. No extra analysis pass, array, frame
field, owner, strategy or scalar representation. Ordinary stores and augmented
global/nonlocal/member/subscript/slice routes retain their real callers.

Existing operator and binding protocols preserve old-LHS-before-RHS order, cells,
aliases, integer promotion and actual suspension. Inputs remain funded until
completion/store. Check token/first refusal before binding, then retire the
original operand range, never local slots. Preserve useful no-inline lifetime
fences and native helper returns. Exact former IR checkpoint counts are not a
contract; no cancellation is replaced with fuel or entry/exit-only checks.

## B291 whole workloads

166.96s, six jobs, unchanged worker `b94fa98c`, CPython3.13.16, independent
identical-current control, normal GC/tiering,5s warmup per role/job and seven
rotated ~150ms batches. Dedicated Standard_F4as_v7/Ubuntu24.04.4 x64/AMD9V45x4,
.NET11RC3. All quiet/source/fixture/library/golden/Closed/final-zero/process joins/
fixture-removal gates pass. [Raw archive](observations.tgz),
[receipt](archive-receipt.json), [summary](summary.json),
[conditions recorded before collection](pre-measurement-plan.md).

Times are milliseconds per invocation; change is current/reference minus one.
Negative means less elapsed time. Original adverse results remain visible:

| Workload / lane | Current / identical control, ms | Reference, ms | Elapsed change | Limits |
| --- | --- | --- | --- | --- |
| scripts.csv-summary-functions.large / warm | 4.945253 / 4.643745 | 4.684789 | +5.56% / -0.88% | 6.10% identical-current spread; confirmation below |
| scripts.text-summary-helpers.large / warm | 2.439022 / 2.417987 | 2.480830 | -1.69% / -2.53% | Small diagnostic change; no causal CPU claim |
| scripts.json-summary-helpers.large / warm | 4.699529 / 4.654234 | 4.712163 | -0.27% / -1.23% | Small diagnostic change; no causal CPU claim |
| scripts.report-render.large / warm | 3.628580 / 3.677189 | 3.691462 | -1.70% / -0.39% | Reference IQR11.55%; inconclusive |
| pure.integer-counter.10000 / warm | 0.549808 / 0.546598 | 0.654893 | -16.05% / -16.54% | Basic-loop elapsed improvement; not whole-script attribution |
| scripts.csv-summary-functions.large / compile-run | 5.009864 / 5.067777 | 5.066942 | -1.13% / +0.02% | Small diagnostic change; no causal CPU claim |

## B292 justified CSV confirmation

Initial warm CSV candidate+5.56% versus identical-current control−0.88%,
6.10% current spread, IQR3.26/4.93/3.70% current/control/reference. Fresh CSV
−1.13/+0.02% did not corroborate that large adverse result. One unchanged-binary
CSV warm confirmation uses8s warmup and eleven rotated~250ms batches,52.01s
within80s. All gates pass; [raw](confirmation/observations.tgz),
[receipt](confirmation/archive-receipt.json), [summary](confirmation/summary.json)
and [prior rationale](confirmation/pre-measurement-plan.md).

Current4.638213/4.648159ms versus reference4.602731ms: **+0.77/+0.99%**;
current spread0.21%, IQR1.86/0.53/0.95%. Retain possible small CSV regression
debt. This does not erase B291, prove the original adverse point environmental,
or establish a CSV benefit. Report remains inconclusive; no more retiming here.

Decision: retain the coherent transfer simplification and basic-loop elapsed
−16.05/16.54%, accepting the possible small CSV cost explicitly. Do not imply
a comparable benefit for all scripts. No allocation saving, causal CPU,
resident memory, startup or embedding-latency improvement was measured. These
source-pair results are separate from the primary representative CPython guide.
The broader whole-script performance objective remains open.

Warm includes invocation/full output/file checks/prior-result cleanup and actual
mediated /dev/shm IO where present. Compile-run adds compilation/program disposal.
Startup/staging/reset/transport/final cache/process/fixture teardown excluded;
final result remains funded through delivery. CPython module caching differs from
isolated Lython execution caches. Existing comparison host supplies efficient IO.

## Correctness and delivery

Clean actual11/10 builds:26.89s/6.40s and7.55s, zero warnings/errors.
Focus247internal/1592public per actual target pass. The existing three actual
protocol-lifetime cases plus one new suspended-refusal case pass both targets:
original failure identity, target unchanged, producer joined, frames unwound,
allowance back to zero. All13operator/reflection, alias, generator, promotion,
exception, cancellation and workspace tests remain. Architecture assertion now
checks local versus retired-operand domains, without checkpoint-count controls.

One wrong solution filename failed before compilation; [original failure](build-refusal.log)
and [corrected successful build](build-refusal-corrected.log) are preserved.
Full local4144internal/16004public per actual11/10 pass. Established Windows/Ubuntu,
original Stress and installed-consumer qualification remains separate/pending.
No unchanged previous qualification is replayed. Publication remains postponed.
