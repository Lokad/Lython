# Canonical constructor frames — 2026-11-06

Retain the shared executable frame policy and deletion of the R09 type operand
shim, with measured performance debt. This is an architectural milestone; it does
not meet the whole-script performance objective or establish an allocation gain.

The existing linear runner owns ordinary positional compiled initialization,
retains one admitted instance through completion, checks None and publishes it.
Sealed allocation is proved before work; initializer selection occurs once after
allocation. Dynamic selection continues on the same instance without retry.
The shared generic driver remains for real dynamic forms. Type operand overloads,
dual array/operand transport, positional-copy conversion and object/type operand
switches were removed. Retaining dynamic hooks get admitted owned argument packets.
No new runner, activation object, frame/cache/owner or public option was introduced.

| Collection / workload | Candidate / identical control vs qualified933 |
| --- | --- |
| B342 original class-record warm | +1.58% / +1.54% |
| B342 original class-record fresh | +3.96% / +2.17% |
| B342 unchanged CSV warm | −0.51% / +0.04% |
| B342 unchanged text warm | +0.97% / +0.87% |
| B343 final class-record warm | +3.47% / +2.75% |
| B343 final class-record fresh | +3.44% / +1.91% |
| B343 unchanged CSV warm | +0.39% / −1.38% |
| B343 unchanged text warm | +1.41% / +0.52% |

Positive means slower:100×(candidate/reference−1), medians of seven rotating
rounds. Both adverse collections are preserved. B342 prompted a source-justified
refinement: remove the redundant pre-allocation initializer lookup and use the
existing post-allocation handoff for all proven sealed allocators. B343 does not
establish a benefit from that refinement; the runs are separate source pairs.
Accept the small remaining debt at this milestone for canonical frame behavior,
owned dynamic packets and removed duplicate transport. Do not compound timings,
claim earlier R06/Q02 debt paid, or continue constructor-only retiming without a
new demonstrated defect. The broader roadmap remains active.

Final source is 8afe8bb1 plus the seven exact production files/patch in B343's
identity and raw archive; a later delivery commit changes metadata only unless
explicitly recorded. Reference933a7fff is verified against installed Release
package F1EE6721. Frozen worker b94fa98c/protocol2 loads final core 1d1eff12;
original B342 loaded 08afa873. Actual Ready hashes are recorded per worker.

Azure quiet VM, .NET 11.0.100-rc.3.26522.148 runtime 11 and CPython 3.13.16; ordinary
GC/tiering. Warm compiles once outside clocks. Fresh includes compilation/program
disposal. Both include mediated RAM I/O, complete output/file validation and prior
result cleanup; startup/staging/reset/transport/final teardown are outside clocks.
CPython imports persist; Lython imports are isolated per execution. This diagnostic
class workload does not replace the primary representative guide. No allocation,
causalCPU, RSS, embedding-process or genuinely pending-I/O latency measurement.

B342 113.65s and B343 113.42s, four jobs each. Golden/quiet/ownership/Closed/join/
fixture gates passed; both persistent archives were hash-verified, with no live
collection or guest fixture left. Original failed fixture observation (result
not exposed through globals) is retained in focused-checks.tgz, then corrected.
VM storage checks reused the existing reference binary for B343; only verified
duplicate transfer archives were removed. Source images/dependencies remain.

Focused final checks: 5,732 public/925 internal per actual .NET 11/.NET 10, including
native return/type subscription, stack, closure/generator, refusal/cancellation,
retained results and real pending constructor hooks. Six new internal cases prove
post-admission initializer mutation cannot restart allocation and retaining native
initializers keep positional/keyword packets after caller-pool retirement.
Final delivery status belongs in validation.json; previous933 platform qualification
does not qualify this source. Publication remains postponed.

Committed source88f12099: final local4,255internal/16,176public pass; both actual installed Release consumers pass (package908B7184, core dependencies empty, adapter nonpackable). Exact-source terminal CI42208020544 is GREEN/all eight Windows/Ubuntu/runtime/originalStress/package gates; validation.json and delivery-ci.json record it. Later private dev40 adapter prototypes are excluded. No publication.
