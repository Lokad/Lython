# Executable handler liveness policy

R126, 2026-11-09 Paris / 2026-11-08 UTC. Committed R125 source3109/core004e8446
versus working core85e68bf6. Remove five explicit NoInlining attributes and their
CLR-temporary-liveness comments/imports from input, comparison, result publication,
formatted operations and local stores:17lines deleted, no algorithm/dispatch change.
Thread, managed-stack/admission and recursive-empty-storage boundaries remain.
No owner, carrier, option, evaluator, accounting policy or fallback is added.

Ownership follows admitted operand slots, actual users/continuations and awaited
retirement/cleanup. CLR collection is independent of cooperative batch accounting.
The previous text source gate assumed the input fence must remain; this prototype
reassesses that premise explicitly. It does not interpret all input/prefix samples
as removable dispatch cost or predict a fixed CPU improvement. CLR inlining remains
heuristic; removing an attribute does not prove a helper actually became inlined.

Offline PE verification checks25,019method identities/signatures/attributes and
24,797executable IL bodies, locals, exception regions and max-stack metadata:
all unchanged; implementation flags differ only by those five NoInlining removals.
This isolates the policy change without proving identical JIT code or performance.

Release actual.NET11/.NET10 each:189internal lifetime/ownership/cancellation cases,
229public semantics/protocol/refusal/cleanup cases, plus21internal managed-stack/
guard and60public isolated recursion/rendering/cycle cases. Probes built explicitly
for each matching framework/current core. All pass. Existing GC/lifetime assertions
remain unchanged; no tests deleted, weakened or retimed. Public APIs are unchanged.
No pending-user, retained-alias or stack-protection dependence on a fence was found
by these checks. Full Windows/Ubuntu Release actual.NET11/.NET10, original stress on both targets
and installed-core consumers now pass [cached CI42538659198](delivery-ci.json),
observed Nov9 00:35:33UTC. It qualifies source9f2e/core85e; later R127 is separate.

Five unchanged complete scripts, both APIs, same worker/optional0.4 and isolated
images sharing CoreLib/processGC. Normal GC/tiering. Warm public Run/awaitedRunAsync,
full goldens, budget balances and cleanup; compilation/startup excluded. Allocation
effectively neutral, all differences within0.064%. All correctness/ownership/image
isolation/cleanup gates pass. Desktop elapsed remains diagnostic:

| Complete script | Sync elapsed change vs both reference images | Async elapsed change |
| --- | ---: | ---: |
| CSV validation/helpers | −7.68..−3.97% | −11.52..+4.00% |
| CSV summary/helpers | −7.46..−2.57% | −9.13..−1.12% |
| Text/helpers | −6.04..−5.51% | −7.83..+9.55% |
| JSON-lines/helpers | +5.69..+8.16% | −1.39..+1.29% |
| Report rendering | −8.96..−7.93% | −4.24..−1.98% |

Original adverse JSON sync is retained; old control spread2.33% does not explain it
fully. Async controls drift up to17.54%; sync text/report controls0.56%/1.13% are
closer. This cannot establish CPU gains or universal neutrality. Retain the
mechanism deletion as development simplification with explicit regression debt,
rather than claim a measured speedup. Original adverse evidence stays visible.

B486 quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 confirmation: three jobs87.001s,
four independent processes/job,5swarmup/role,7rotated150msrounds, normalGC/tiering.
Same existing scripts/worker/auxiliaries, warm public execution with mediated
RAM-file I/O/full goldens/prior-result cleanup; compile/startup/reset/protocol/
final cache teardown excluded. All Ready/core/auxiliary/golden/budget/quiet/Closed/
joined-user/RAM-removal/archive-under-lease/local-verification gates pass:

| Complete sync script | Elapsed change vs both reference processes | Process CPU change | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| Text/helpers | −7.23..−2.16% | −8.54..−1.25% |5.27% /0.19% |
| Report rendering | −4.15..+1.32% |0.00..+3.99% |4.50% /1.15% |
| JSON-lines/helpers | −2.07..+0.94% | −2.50..+6.25% |1.67% /1.33% |

The adverse desktop JSON elapsed result does not reproduce. Text supports a
possible improvement; reference spread5.27% and coarse CPU ticks prevent a precise
gain claim. Report remains inconclusive with possible CPU debt, not established
neutrality. Retain the policy deletion for simplification and passing focused
semantics/ownership/stack checks; no further unchanged timing or speculative
boundary restoration selected. Full coherent platform/stress/package qualification now passes as above. No percentage veto, permanent closure or publication.

Before launch, a fresh idle lease verified complete inode aliases/SHA/ownership/
writable parents for two redundant compiler-assembly extents. The persisted plan
preceded atomic exact-byte hardlink replacement;14,016,512bytes recovered. Every
historical path/byte preserved, no evidence deletion or floor reduction. Full proof
and receipt travel with the archive. B486 terminal cached; no users/fixtures remain.

The broader call/frame/operand and complete-script objective stays active. Source
inspection confirms the existing linear caller-return engine, shared parameter
layouts, inline ordinary presence bits, borrowed locals/tuple unpack and adopted
CSV row arrays. Proposed duplicate contexts, local arrays and row-copy phases do
not exist. Generic variadic staging is real but absent from the profiled primary
helper; it is not a demonstrated dominant cost. Existing named-source text CPU
evidence remains separate from current HEAD; no extra capture selected.

[Source identity](identity.json), [diagnostic](assessment-local.json),
[focused validation](focused-validation.json),
[source/conditions/binaries/raw checks](source-and-diagnostics.tar.xz),
[IL verification](il-proof.json), [quiet comparison](assessment.json),
[receipt](archive-receipt.json), [raw quiet evidence](observations.tgz).
