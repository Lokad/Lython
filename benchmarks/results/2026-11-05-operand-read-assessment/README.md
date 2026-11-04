# Checked operand-read assessment â€” withdrawn

2026-11-05 Europe/Paris; UTC timestamps are retained in every collection.
Qualified baselinef6e51d1c/frozendf206f47. All production variants are withdrawn;
none describes current HEAD performance. Keep seven independent public failure-
order/span/cleanup regressions, restoring the old internal compiler-shape assertion.
Publication remains held; the whole-script performance objective remains open.

## Source/lifetime proposal

The existing compile-only B132 census was adapted for five maintained whole scripts.
Four helpers already have native roots; ordinary modules106/85/96/65/79instructions.
Fold adjacent checked local/constant reads into fixed-arity consumers, preserving
original spans/left-right order and real packets across pending raw type getters.
Calls retain contiguous evaluated arguments. Remove initialized-local/capture
forwarding inventory; preserve native proof, receiver snapshots, iterator ownership,
actual cancellation/refusal and awaited cleanup. No new engine/value/owner policy.
[Original plan](pre-code-plan.md), [static summary](source-map-summary.json), [all maps and census source](source-map-evidence.tgz).
Static counts fall14/9/11/6/7instructions; conservative operand capacity unchanged.
These are code reductions, not CPU forecasts or admitted-peak/RSS measurements.

## Preserved outcomes

| Source/collection | Complete observations | Decision/limitation |
| --- | --- | --- |
| Compact origins0d6767cb,218s/eight jobs | Textwarm+3.96â€“4.13%,oldspread0.16%; textasync+3.87â€“5.23%; CSVslightlybetter; runallocationflat | Adverse text established; compiler allocation+0.5â€“0.85%; no retention |
| Getter-only968439c2,28s/one job versus initial | Textwarm+1.43/âˆ’0.99%,unchanged initialspread2.45% | Inconclusive; no demonstrated getter benefit/CPU cause |
| Direct spans6a488d04,54s/two jobs | Textwarmâˆ’2.36â€“2.43%,oldspread0.07%; CSVâˆ’0.75â€“2.54%,spread1.84% | Useful narrow result; full-source pair still required |
| Same direct source6a488d04,217s/eight jobs | Textwarm+0.42/âˆ’1.38%,spread1.83%; textasync+2.96â€“4.66%; reportasync+2.64â€“3.28%; CSVwarmâˆ’1.37â€“2.71% | Broader result mixed/adverse; withdraw entire source |

Final direct source compileCSV+4.11â€“5.10%,report+5.09â€“5.92%; old spreads0.95%/0.79%.
Compilation allocation+5.00%/4.86%. Freshconfig+0.96â€“1.39%,allocation+2.09â€“2.11%.
Run allocation remains near unchanged. AsyncCSVâˆ’2.88â€“6.70% has4.10%oldspread;
its magnitude is uncertain. All adverse/moving controls remain, without adding
percentages or attributing a cause. The earlier warm-text gain is not generalized.

The compact shape kept40B instructions by adding negative span-origin tags and
constant-pool metadata. A bounded getter-only refinement was inconclusive. The
direct shape removes that encoding/pool/getter machinery and uses ordinary original-
span references, consciously reconsidering a pre-code memory-percent rejection.
Managed arrays verify1024instructions40,984â†’57,368B (40â†’56B/elements). Even with
fewer instructions, module instruction storage grows about21â€“28%,~0.7â€“1KiB/script.
It avoids the compact invariant but does not justify overall CPU/compile/storage
debt on current complete-workload evidence. No further local shape/hint series.
This is not a permanent design veto; a future design needs a distinct cost/lifetime
proof. No tagged-origin/direct-span scaffolding or altered bytecode remains in core.

## Correctness and collection boundary

All three source shapes pass996internal/4,203public focused checks per actual11/10.
Seven new public failure cases run both APIs, preserve original failure substrings,
precede protocol effects and return the borrowed budget to baseline after cleanup.
[Independent isolatedCPython3.13.2 messages](cpython-error-goldens.json). Existing
pending getter rebinding, callback/cancellation/refusal, aliases and shared-budget
cases are unchanged. [Completed prototype logs](completed-prototype-validation.tgz),
[initial log snapshot](validation-logs.tgz), [diagnostic corrections](diagnostic-corrections.md).
Initial exact-shape failure and private census launcher/glob corrections are explicit.

Existing three-image driver, two unchanged baseline roles,6s warmup/image,2s settle,
9rotated200ms samples; normalGC/tiering/no overrides, quietAzureF4as_v7/.NET11RC3.
Every loaded library/CoreLib/hash/full independentCPython golden/quiet/balance/
worker join/owned-fixture cleanup gate passes. All units are terminalsuccess/PID0,
fixtures gone/sharedleasefree. Warm includes public invocation, mediated RAM I/O,
full checks/prior-result cleanup; async uses completed I/O, real suspension tested
separately. Fresh includesCompile/Dispose; compile-only has no guest execution/I/O.
Staging/reset/startup/hash/finalcachedteardown excluded. Process allocation includes
reset/background noise; initial untimed warm-admission peaks are not fresh overlap,
physical retained heap/RSS or embedding latency. No new-source full platform/package
qualification for discarded code; the unchanged production runtime remains green.

## Immutable source and raw evidence

Each phase includes the exact core/source identities, source patch/snapshot, plan
before timing, all metrics and raw observations:

- [initial metrics](initial/assessment.json), [identity](initial/candidate-identity.json), [source](initial/source-snapshot.tgz), [raw](initial/observations.tgz); SHA256 `fe074458a23cd99cab4b7f34e00df02f2a26542c36ccf15e2ce019b8c919f3a9`.
- [native-origin-confirm metrics](native-origin-confirm/assessment.json), [identity](native-origin-confirm/candidate-identity.json), [source](native-origin-confirm/source-snapshot.tgz), [raw](native-origin-confirm/observations.tgz); SHA256 `b3e195bc7fa0f62e50a815cfced94a78dfb8e2a6142cd7e687bcb55ec14ceec3`.
- [direct-span-confirm metrics](direct-span-confirm/assessment.json), [identity](direct-span-confirm/candidate-identity.json), [source](direct-span-confirm/source-snapshot.tgz), [raw](direct-span-confirm/observations.tgz); SHA256 `c3751a3e53b1e0020d506840d87c54f7e4cc0e8162a125b02bf223edac81f80c`.
- [direct-span-final metrics](direct-span-final/assessment.json), [identity](direct-span-final/candidate-identity.json), [source](direct-span-final/source-snapshot.tgz), [raw](direct-span-final/observations.tgz); SHA256 `a88ad615759d260477428a0db688d44ba8ce8c515f0b4b003b1cd997ee743557`.

The original native-origin confirmation's derived purpose string is stale; the
actual C# raw purpose/hashes/job count are authoritative and preserved. Production source is exactly restored; nonincremental Release34.01s/zero warnings
and17public checks per actual11/10 pass. [Restoration](restored-validation.json),
[logs](restored-validation-logs.tgz). No original/adverse observation is overwritten.
