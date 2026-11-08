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

Release actual.NET11/.NET10 each:189internal lifetime/ownership/cancellation cases,
229public semantics/protocol/refusal/cleanup cases, plus21internal managed-stack/
guard and60public isolated recursion/rendering/cycle cases. Probes built explicitly
for each matching framework/current core. All pass. Existing GC/lifetime assertions
remain unchanged; no tests deleted, weakened or retimed. Public APIs are unchanged.
No pending-user, retained-alias or stack-protection dependence on a fence was found
by these checks. Full platform/stress/package qualification remains pending a
coherent delivery checkpoint; R125's separate in-flight CI does not qualify R126.

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
rather than claim a measured speedup. A bounded quiet-VM comparison of text/report/
adverse JSON with independent controls is the next measurement gate; do not repeat
the unchanged desktop round. It may justify refinement of individual JIT choices,
without restoring a blanket GC-liveness policy. No percentage veto or permanent
design closure. Final delivery/publication is not claimed.

The broader call/frame/operand and complete-script objective stays active. Source
inspection confirms the existing linear caller-return engine, shared parameter
layouts, inline ordinary presence bits, borrowed locals/tuple unpack and adopted
CSV row arrays. Proposed duplicate contexts, local arrays and row-copy phases do
not exist. Generic variadic staging is real but absent from the profiled primary
helper; it is not a demonstrated dominant cost. Existing named-source text CPU
evidence remains separate from current HEAD; no extra capture selected.

[Source identity](identity.json), [diagnostic](assessment-local.json),
[focused validation](focused-validation.json),
[source/conditions/binaries/raw checks](source-and-diagnostics.tar.xz).
