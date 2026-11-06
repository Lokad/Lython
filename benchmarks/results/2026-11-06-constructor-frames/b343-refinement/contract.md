# R10 canonical constructor frames / bounded comparison

Hypothesis: reusing the existing linear executable runner removes recursive
constructor-to-initializer entry and duplicate type operand dispatch on ordinary
class-record scripts. Dynamic/descriptor/keyword construction retains one general
binder and shared allocation/initialization stages; no new runner/frame/owner.
Source gate and implementation contract are preserved beside this collection.

Source acceptance: PyType operand overloads, dual argument representation and
object/type switch in operand wrappers removed; instance allocated once even if
an admission hook changes initializer eligibility. Preserve post-allocation
metadata lookup, Python None return checks, direct receiver binding, genuine
suspension, first refusal/cancellation, funded retained arrays and escaped results.
Both-runtime constructor/subscription/native-return/function/stack/ownership gates
must pass before collection. No unsafe/native stack operations or new policies.

One existing four-job comparison: class-record warm/fresh, unchanged CSV/text
warm controls; qualified installed933a7fff reference, two identical candidate
roles, CPython3.13.16, frozen b94fa98c worker/protocol2, normal GC/tiering. Seven
rotating rounds/5s warmup/.15s batches; total supervisor160s. Preserve full raw
warmups, adverse results, actual loaded-core Ready hashes and source snapshot.
No broad CPython table refresh, allocation/causalCPU/RSS or pending-I/O claim.
Bounded confirmation permitted only for a demonstrated source/measurement defect.

Timing includes mediated RAM fixture I/O, complete output/file validation and
prior result cleanup; fresh includes compile/program disposal. Startup, fixture
staging/reset/transport and final teardown excluded. CPython imports persist;
Lython imports are execution-isolated. All workers must acknowledge Closed/join,
all fixture directories disappear, and quiet/funding gates pass before evidence
is archived/hash-verified in persistent storage. Keep the existing VM lease and
expanded-payload disk guard. Publication/tagging postponed.

# B342 adverse result / justified R10 refinement

B342 completed113.649s/four jobs/all gates; archive1dfa63debb65c8b47d11e9709256a62186e66ea0345421548fb85adc1efb8ca6. Preserve all original source, raw observations and adverse conditions. Class warm+1.54/+1.58%, fresh+2.17/+3.96% versus qualified933; unchangedCSV aboutflat, text+0.87/+0.97%. No speedup or allocation claim.

Source inspection identifies one unnecessary semantic phase: eligibility looked
up __init__ before admitted allocation, then selected it again after allocation.
The post-allocation lookup is authoritative. Prepare any proven sealed allocator
under the same existing metatype/JSON restrictions, then select initializer once.
Compiled init uses the runner; all other forms use the already implemented shared
init stage on the same instance. No new state, type whitelist, cache or owner.
This removes work and makes allocation-before-init selection consistent instead
of predicting eligibility from metadata that admission can change.

Add an explicit regression precondition proving initial compiled init eligibility;
the existing mutation test then proves transition to generic init without restart.
Repeat affected semantics/cancellation/ownership both actual runtimes because
no-init/default/dynamic forms now share allocation. One bounded confirmation with
the same four workloads/worker/boundaries versus933 is allowed after those pass.
Keep B342 and confirmation separate; decide residual regression debt explicitly.
Do not compound deltas, open another profiling campaign or claim whole-script
objective complete from this constructor milestone.

B343 reuses the hash-verified installed933 reference core retained in B342.
Only the new core and source/evidence are transferred. The existing post-setup
24MiB free guard is also applied after expanded-payload admission, rather than
reserving32MiB for the earlier double-core transfer; output/archive size from
B342 is below1MiB. No original evidence, source image or dependency is removed.
Both transfer size and expanded size are checked before collection.
