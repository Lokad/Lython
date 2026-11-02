# Direct final CSV row construction — withdrawn B135

2026-11-03. Prototype removed; retained runtime remainsfd7bc7e4. Source845b63c8
contains the same production code plus evidence. Baseline core547825f9, candidate
29efc91294aaeaaeaee5e792a5c8597c8f493a9b2eca36b6247d48466c23f301.
No public API/contract change or new CPython ratio is delivered.

The [exact prototype/test patch](source.patch.gz) replaces reusable List<object>
staging with the final governed PyList, built lazily then detached into the ready
queue. It removes the final copy, staging capacity growth and1024-cell trim policy,
using existing unpublished rollback for partial rows. Ready queues, header/dictionary
construction, current tokens and shared-budget owners remain. No new infrastructure.

The earlier allocation can deny before the first field completes. Completed rows
never clear/refund on parser cleanup; unpublished backing does. Existing99 internal/
425 public semantic/cancellation/refusal/ownership tests pass on each actual.NET11RC3/
10.0.8. Four initial failures are preserved: two old staging-capacity assertions
removed, two denial fixtures moved to actual new-row admission and8→9 growth while
preserving terminal origin, queued content, owned-prefix funding and cleanup.
These adaptations are withdrawn with the prototype; no regression was found in
the retained runtime that requires them independently.

## Complete workload result

Quiet F4as_v7 Ubuntu24.04.4/.NET11RC3, normal workstation GC/tiering. Existing
worker/driver,4s warmup/image,9 interleaved~200ms batches, independent original/
candidate/identical-original processes. Full goldens, images and both quiet gates
PASS. Six jobs115.714s, exit0/inactive, workers joined/fixtures removed, lease free.

| Complete workload | Elapsed | Process CPU | Unchanged elapsed / CPU |
| --- | ---: | ---: | ---: |
| CSV warm, first |+2.76%|+1.28%|+2.65% /+3.30%|
| CSV warm, second |−1.54%|−2.60%|−0.07% /+2.65%|
| Variable-width CSV warm |+1.77%|+2.25%|+0.02% /−1.38%|
| Text control |+5.32%|+8.22%|+3.63% /+4.01%|
| JSON control |−1.25%|−1.67%|−0.87% /−4.61%|
| CSV fresh compile-to-result |−2.47%|−11.80%|+0.51% /−7.30%|

Canonical warm is flat to modestly better; fresh elapsed about3% lower against its
control. Text/JSON drift remains visible. Variable-width rows are about1.75% slower
elapsed and3.7% CPU relative to stable controls. They retain the same summary source
and output goldens, varying records among2/3/13/63 fields. Independent isolated
CPython3.13.16 verifies the new fixture completely before timing.

Separate complete-job allocation diagnostic:192 warmups/image,9x32 public runs,
shared CoreLib/GC and isolated images, complete goldens/zero final balances.
Canonical CSV5,905,455.25 bytes/run on BOTH images; control+0.0004%.
Variable-width CSV20,517,681→21,614,065 bytes/run (**+5.3436%**), control0%.
Text/JSON follow immaterial control changes. Diagnostic clocks are retained but
do not replace the independent timing run. Service58.973s/exit0/inactive; all
identities/gates/ownership witnesses PASS, fixtures removed and lease free.

Warm excludes compilation/startup; fresh includes each compilation/disposal. Both
include reset/staging, actual efficient mediated I/O, complete output/file checks
and prior-result cleanup. Final retained owners/results/process/fixtures are
outside batches and joined. Process CPU includes runtime/JIT/response, not guest-only.

## Decision

Do not retain this implementation. Existing small-row construction already avoids
material allocation; moving unknown-width rows directly into final backing
reintroduces repeated growth, capacity slack and reservation work. A simpler-looking
producer is insufficient compensation for the stable wide-row regressions without
a substantial common-case benefit. No rigid threshold or blanket permanent ban:
a future design must eliminate copying AND repeated growth, rather than append a
capacity-prediction heuristic or retain a second parser route. Preserve current
reusable staging. The earlier B110 dictionary-fusion rejection remains independent.

[All timing](raw-timing.tar.gz), [allocation](raw-allocation.tar.gz),
[catalog](catalog.json.gz), [plan](pre-code-plan.md) and validation/restoration logs
retain adverse controls, initial failures and exact implementation. Production and
its original tests are restored, with fresh nonincremental per-target builds and
101 internal/425 public checks PASS per actual target after restoration. Full
delivery41540517622 is GREEN at exactfd7bc7e4, independent of
this absent prototype. The broader whole-script objective remains open.
