# Ordinary operation-result transport — refined design retained

Measured2026-11-05. Source `a63708a7` plus archived D62 changes; frozen
qualified D61 `6c669799` is the reference, built before commit metadata. D61's
Windows/Ubuntu actual11/10/original Stress/installed consumers are GREEN/all8
in cached42081657838. That qualification does not cover this changed source.
The later `1a4d95ab` only records D61 qualification. Publication remains postponed.

## Current design and removals

Ordinary single-guest-result producers can complete into an adjacent local
binding. The existing forwarding pass marks them by complementing their original
extent int; decoding recovers the same physical positions. The original store
retains its target and independently meaningful source span. No instruction
widening, frame fields, side table, layout pass, owner, value representation,
public option, second dispatcher or async completion stage.

Reads, arithmetic, calls (including executable children), subscript/slices,
containers, successful iteration and prepared lambda/generator/comprehension/
formatted/call results share completion. Actual private multi-slot resources,
receiver packets and yield/delegation/control retain their existing behavior.
Native helper programs still prepare before forwarding and keep their own spans.

Retire original inputs above the consumed slot in the original order. If cleanup
is pending, keep the existing funded result operand and following store until
the unchanged driver awaits disposal. Otherwise forget the consumed input (as
the old result overwrite did), check token/first refusal at the original store
span, bind through StoreLocalValue and skip that store. Pending inputs/results
remain funded. No early alias publication, cleanup-error rerouting, refusal retry
or memory-policy change. Necessary materialized result transport stays in use.

Removed: dedicated AugmentedLocal opcode/handler/file, its special forwarding
case/getter, duplicate augmented enum, both direction-mapping switches and runtime
wrapper. The syntax operator now flows directly through the pipeline. Ordinary
stores still serve uncombined work and pending cleanup. Source field/deletion
[proof](field-deletion-proof.json), original [contract](source-contract.md),
[representation refinement before first build](representation-refinement.md),
[initial source](source-snapshot.tgz) and [retained source](refinement/source-snapshot.tgz).

Retaining D61's store as metadata costs one extra instruction per previously
combined augmented pair; no compiled-storage saving is claimed. Compile-only
measurement was added before collection specifically to check that tradeoff.

## Retained B294 result

One193.25s/sevenjob comparison after the documented fence-placement refinement,
within240s. Dedicated Standard_F4as_v7/Ubuntu24.04.4 x64/AMD9V45x4,
.NET11RC3, CPython3.13.16, unchanged workerb94, normal GC/tiering. Four independent
process roles including identical-current control,5s warmup/role/job and seven
rotated~150ms batches. All quiet/source/fixture/library/golden/Closed/finalzero/
worker-join/fixture-removal gates pass. [Raw](refinement/observations.tgz),
[receipt](refinement/archive-receipt.json), [summary](refinement/summary.json),
[pre-collection conditions](refinement/pre-measurement-plan.md).

Times are milliseconds per invocation; elapsed change is current/reference−1.
Negative means less elapsed time. These are Lython source-pair observations,
separate from the primary representative CPython guide.

| Complete workload / lane | Current / identical control, ms | Reference, ms | Elapsed change | Current spread | IQR current/control/reference |
| --- | --- | --- | --- | --- | --- |
| scripts.csv-summary-functions.large / warm | 4.805689 / 4.817944 | 4.790143 | +0.32% / +0.58% | +0.26% | 1.32/4.46/2.78% |
| scripts.text-summary-helpers.large / warm | 2.187946 / 2.287847 | 2.253273 | -2.90% / +1.53% | +4.57% | 0.93/0.25/0.90% |
| scripts.json-summary-helpers.large / warm | 4.411815 / 4.365178 | 4.453179 | -0.93% / -1.98% | -1.06% | 0.64/0.79/1.19% |
| scripts.report-render.large / warm | 3.518042 / 3.490013 | 3.501745 | +0.47% / -0.34% | -0.80% | 4.73/3.28/4.50% |
| pure.integer-counter.10000 / warm | 0.509528 / 0.506544 | 0.522337 | -2.45% / -3.02% | -0.59% | 0.53/0.39/0.21% |
| scripts.csv-summary-functions.large / compile-run | 4.879231 / 4.869729 | 4.841447 | +0.78% / +0.58% | -0.19% | 1.14/0.88/1.22% |
| scripts.csv-summary-functions.large / compile | 0.071880 / 0.072336 | 0.073708 | -2.48% / -1.86% | +0.63% | 0.68/2.48/1.72% |

Decision: retain the general transport invariant and deleted mechanisms. Basic
loop−2.45/3.02% is consistent in this block; JSON−0.93/1.98% is smaller than the
initial block's~7.6%. CSVwarm+0.32/+0.58% and freshCSV+0.78/+0.58% leave possible
small regression debt. Text−2.90/+1.53%, with4.57% identical-current spread, remains
inconclusive despite low per-process dispersion. Report also varies. Compilation
−2.48/1.86% does not establish a general frontend gain or storage reduction.

Do not generalize the initial larger JSON/text observations: unchanged reference
medians also differ materially across blocks. Cause remains unmeasured. No new
retiming campaign is justified for this delivery. No general whole-script CPU,
allocation, startup, RSS or embedding-latency improvement is claimed. Preserve
D61 and older debts; source-pair percentages are not additive. The substantial
whole-script performance objective remains open.

## Initial B293 and justified refinement

Original193.43s/sevenjobs within240s, all the same gates. Source frozen14780388;
[raw](observations.tgz), [receipt](archive-receipt.json), [summary](summary.json)
and [conditions recorded before collection](pre-measurement-plan.md). Every
adverse point and original source is retained:

| Complete workload / lane | Current / identical control, ms | Reference, ms | Elapsed change | Current spread | IQR current/control/reference |
| --- | --- | --- | --- | --- | --- |
| scripts.csv-summary-functions.large / warm | 4.740501 / 4.664865 | 4.642017 | +2.12% / +0.49% | -1.60% | 0.58/0.49/0.93% |
| scripts.text-summary-helpers.large / warm | 2.236236 / 2.211714 | 2.354742 | -5.03% / -6.07% | -1.10% | 0.59/0.60/1.69% |
| scripts.json-summary-helpers.large / warm | 4.366038 / 4.370974 | 4.728940 | -7.67% / -7.57% | +0.11% | 1.14/1.60/1.13% |
| scripts.report-render.large / warm | 3.558611 / 3.471216 | 3.653826 | -2.61% / -5.00% | -2.46% | 6.68/2.87/5.13% |
| pure.integer-counter.10000 / warm | 0.529889 / 0.527092 | 0.519809 | +1.94% / +1.40% | -0.53% | 0.55/1.02/1.65% |
| scripts.csv-summary-functions.large / compile-run | 4.822414 / 4.903185 | 4.851734 | -0.60% / +1.06% | +1.67% | 0.87/0.82/1.01% |
| scripts.csv-summary-functions.large / compile | 0.073777 / 0.072061 | 0.072723 | +1.45% / -0.91% | -2.33% | 1.60/2.42/2.92% |

Initial implementation forced NoInlining for ordinary operand publication even
when no destination was forwarded. One source-justified refinement restores a
small inlineable ordinary wrapper; direct local completion retains its no-inline
lifetime fence. Other existing store/worker barriers remain. [Rationale before
refinement](refinement/source-contract.md), [exact refined patch](refinement/source.patch).
This is an architectural/code-generation correction, not proof of causal CPU gain.

## Measurement boundary and validation

Warm includes invocation, mediated actual /dev/shm file IO, full output/file checks
and prior-result cleanup. Compile-run adds compilation/program disposal. Compile-
only does not execute except its independent initial equivalence verification.
Startup/staging/reset/transport/final cache/program/result/process/fixture teardown
excluded; final result stays funded through delivery. CPython module caching and
isolated Lython execution caches differ. Existing comparison host supplies efficient
IO. No new benchmark framework, controls or runtime timing policy were added.

Initial builds27.26s/4.16s, refined26.75s, both actual targets/zero warnings/errors.
Initial focus282internal/1592public plus25 result/iterator cleanup cases per target
pass. Final focus303internal/1592public per actual11/10 pass. Six new actual
suspended native/child/subscript result cases preserve target while pending,
alias/result identity after completion, current cancellation/first refusal,
awaited producer cleanup and budget recovery. Existing augmented/reflected,
promotion, cell/namespace, generator/comprehension, formatting, exception and
resource checks remain. No test-only fuel/checkpoint counting or synthetic timing.

Full local4150internal/16004public per actual11/10 pass. Established Windows/Ubuntu,
original Stress and installed-consumer checkpoint remains separate/pending.
No earlier unchanged qualification is replayed. See current PLAN for the next
bounded redesign gate; this experiment does not complete the performance objective.
