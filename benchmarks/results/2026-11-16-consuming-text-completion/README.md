# Consuming completed text builders

R426/B822,2026-11-16UTC. Retain **810dafdd** versus fully qualified
**a2d20928 / 9d26d408**. Only the core differs between copied/full-hash-checked
images; benchmark worker, released0.4adapter and auxiliaries remain identical.

Thirteen terminal paths now use the existing consuming text finalizer: join,
replacement, translation, casefold, zfill/tabs, byte hex/representation, method
formatting, HTML rendering and match expansion. This removes their final exact
byte-array allocation/copy and temporary reservation before immediately releasing
the builder. No new builder, memory layer, policy, public API, provider or cache.
Actual JSON chunk iteration still snapshots its reused working buffer.

The immutable result inherits already-admitted backing capacity; metadata admission
still precedes its shell allocation. Builder detaches only after successful transfer.
Failure keeps input funded until cleanup; actual callbacks/users finish first.
Unused tail remains charged, so conservative retention can reject earlier. The
eliminated copy overlap can also allow a command which previously failed admission.
No promise of identical tiny-budget thresholds or transactional command rollback.
Host allowance resize/refund semantics are unchanged; no credit cache/batched lease.

## Complete-script source pair

Quiet F4as_v7/Ubuntu24.04.4, four AMD EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
ordinary tiering/natural workstationGC. Existing worker/protocol, two processes per
source,5s full-request warmups and five rotated approximately0.5s batches. Four
jobs complete in140.446s; all process-median pairings below, not confidence intervals.

Existing method report:1,024JSON records/189,441B. One new complete normalization
script reads52,224B/4,096universal-newline lines, normalizes CRLF/CR, expands tabs,
strips trailing whitespace, joins lines and writes54,269B/4,095lines. Unicode/emoji,
mixed newlines and blank lines are explicit. Expected rows/stdout/file bytes are
constructed independently and match CPython3.13.2 before either source runs. This
is a useful file-processing case, not evidence of a universal agent-workload mix.
Existing JSON helper control and local invoice control remain unchanged.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | ---: |
| File normalization / warm | +0.79..+2.36% | −0.64..+2.02% | 0.86/0.69% |
| Method report / warm | −3.26..−1.19% | −1.71..+0.53% | 1.40/0.74% |
| Method report / compile-to-result | −3.68..−0.23% | −4.78..+0.49% | 1.54/2.02% |
| JSON helpers / warm control | +0.40..+2.28% | −0.50..+2.19% | 1.63/0.24% |

Positive means slower. Report elapsed is modestly lower; CPU is mixed, not clean
support for a universal improvement. **Normalization elapsed regresses in all four
pairings**, and JSON is adverse/moving. Retain those debts for the simpler consistent
ownership/completion path and distinct allocation benefit; do not call them neutrality,
causally assign them to retained capacity, or claim broader performance completion.
No additional confirmation is selected without a concrete corrective hypothesis.

| Local complete warm diagnostic | Allocated B/invocation, reference → candidate | Allocation change | Accounted execution peak, reference → candidate |
| --- | ---: | ---: | ---: |
| Method report | 5,205,054 → 4,877,696 | −6.29% | 3,448,809 → 3,478,447 (+0.86%) |
| File normalization | 2,387,526 → 2,171,008 | −9.07% | 2,513,228 → 2,606,412 (+3.71%) |
| Invoice control | 4,198,990 → 4,198,990 | Flat | 9,432,323 → 9,432,323 |
| JSON helper control | 2,023,174 → 2,022,944 | Effectively flat | 4,060,103 → 4,060,103 |

Short local counters use>=4/.5s warmup,3x4 observations, natural process-wide CLR
allocation with no forcedGC. Peaks are conservative governor estimates, not heap/RSS
or a percentage-coverage claim. The new normalization allocation runs only its new
case; original three-case observations are preserved and not retimed.

Warm clocks include Run, mediated RAM I/O, full stdout/file goldens and prior-result
cleanup; exclude compilation/startup, reset/protocol and final cache teardown. Fresh
adds compilation and compiled-owner disposal. Wider CPU includes reset/protocol.
Local allocation also includes reset/validation/result cleanup. No CPython timing,
startup, pending-I/O, contention, heap/RSS or embedding-tail comparison is established.

## Correctness and delivery

Nonincremental Release solution build passes. Focused87internal/2,547public cases
pass each actual.NET11/10, including Unicode/text/bytes, hooks, HTML, shared-provider
semantics, snapshots/consumption, retained aliases, terminal refusal and cleanup.
Two new public lifetime cases preserve completed text through actual asynchronous
I/O, cancel/join/close before refund, and reuse the budget across executions.
All local complete output/file goldens and compilation-baseline/final-zero checks pass.

Original failures remain: four join assertions expected a copied9-byte payload
charge instead of metadata; two80KiB replace fixtures now succeed because final-copy
overlap disappeared; a new test incorrectly used Run with an asynchronous-only host.
Join metadata expectations were corrected with live-input/refusal assertions intact.
Replace keeps the same80KiB budget and expands output fourfold, preserving real
preflight refusal/no publication/terminal failure and adding explicit refund checks.
The new lifetime case uses RunAsync; existing focused cases exercise both APIs.
Independent CPython expected returned text/hex/repr/zfill values agree.

All image/catalog/golden/budget/quiet/Closed/exit/join/archive gates pass. Persistent
raw archive verifies under lease before teardown; no pending users or RAM fixtures.
Only two exact historical eee aliases and four redundant uncompressed JSON records
were retired after committed archive custody and fresh UID/path/hash/link/all-alias
proof. Current9d/810images, catalogs, compressed evidence and toolchains remain.
Initial local staging projection did not fit the unchanged floor; one additional
archived JSON replica was identified before launch. No collection restarted and
254/252MiB stage/archive floors remain. Terminal cached forever.

**Full platform/stress/installed-package qualification pending; publication held.**
Broader call/frame/operand/collection/frontend priorities remain active.
[All medians](summary.json),[initial allocation](allocation-summary.json),
[normalization allocation](normalization-allocation-summary.json),
[focused checks](focused-checks.json),[identity](identity.json),
[raw observations](observations.tgz),[source/binaries/procedures/original conditions](source-checks-and-procedure.tar.xz).
