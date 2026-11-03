# B162 shared native instruction driver

2026-11-03. Runtime source **b087c7b86a02f3ba24e9926bf8e41526f39ba8b2** is
committed, with **open synchronous regression debt**. Publication remains postponed.
This is an architectural simplification with some supported async improvements,
not a general CPU speedup or completion of the whole-script performance objective.

Both APIs now use one non-async opcode loop. Completed work remains in that loop;
a thin async driver awaits incomplete operations and relinquished cursor disposal,
then resumes it. The duplicated sync/async opcode switches are deleted. Shared
existing member-cache eligibility also applies to async resolution; custom mutable
members/protocols still resolve normally. No public API change, new frame fields,
owner layer, operation registry, pool, policy switch or recursive suspension chain.
Genuine async operation helpers and real lowered consumers remain.

The originating operation span travels independently of a helper that may finish
and update its jump before the caller resumes. This strengthens a source invariant;
no externally demonstrated wrong-span failure is claimed. A final bounded refinement
moves the yield test from every instruction to YieldFrom, preserving the original
native boundary invariant. Yield and actual async suspension retain their cleanup.
Source variants and displaced code are preserved below.

## Retained source comparison

Same synchronous-completion host, normal GC/tiering, shared CoreLib/hash witness,
four complete RAM-backed file scripts. All three images use the SAME selected API
in each run: before/control are B160, candidate is B162. Nine rotated32-job batches
follow512 warmups per image; compilation/startup excluded. The outer timer includes
fixture reset, public invocation, actual mediated file I/O, complete stdout/file
checks and result disposal. Nested Run-only timings remain in raw observations.
Every program owner, fixture and worker joins; all balances return to zero.

| API/workload | Before ms | Candidate ms | Elapsed change /unchanged control | CPU change /unchanged control |
| --- | ---: | ---: | ---: | ---: |
|sync/csv-summary-helpers|5.4418|5.2960|-2.68% /-1.80%|-2.82% /-2.02%|
|sync/text-summary-helpers|3.6457|3.7088|+1.73% /-0.54%|+1.75% /-0.54%|
|sync/json-summary-helpers|5.6837|5.7839|+1.76% /-0.54%|+1.77% /-0.20%|
|sync/report-render|6.1493|6.0130|-2.22% /-4.06%|-2.52% /-3.39%|
|async/csv-summary-helpers|6.3540|6.4395|+1.35% /+2.66%|+1.43% /+3.05%|
|async/text-summary-helpers|4.4167|4.1591|-5.83% /-4.96%|-5.63% /-4.82%|
|async/json-summary-helpers|6.7076|6.4752|-3.46% /+2.67%|-3.47% /+2.57%|
|async/report-render|6.3906|6.3900|-0.01% /+7.44%|+1.13% /+7.36%|

Allocation is effectively unchanged (within0.03%, with similar incidental control
changes); accounted peaks are identical. Physical RSS/embedding latency is not
measured. The common loop alone does not remove the CSV async allocation difference
observed in B161. It does not explain the synchronous CPython gap.

**Debt decision:** retain the single-loop architecture on the unreleased branch.
Synchronous text/JSON are about2.3%adverse against the later identical control;
report is also adverse relative to its moving control. Async JSON improves across
the variants, but controls limit the precision of other script gains. Do not claim
an all-workload improvement. The next call/value-transport milestone must address
this synchronous cost, or refine/withdraw the shared-mode implementation before
release; it may not silently turn into permanent regression debt. No further
unchanged replay is selected here. B160's earlier CSV uncertainty remains open.

## Earlier variants and adverse observations

- Initial46381f2e: async−1 to−5%; sync JSON+3.58%/control−0.84%,
  report+3.69%/control+6.09%. Allocation/peaks effectively unchanged.
- One role-swapped confirmation of that exact image: new source occupies
  before/before-control, OLD source occupies candidate; API order and warmup order
  reversed. Async CSV/text/JSON−4 to−12%, report−2 to−4%; sync text+3.9 to+6.8%,
  CSV+0.7 to+2.9%, report+1.7 to+4.0%, JSON−1.4 to−1.7%. Both new-source clones
  are reported; raw generic purpose text does not override recorded source hashes.
- Span-corrected924086a4: async CSV/text/JSON−5.1/−7.6/−7.9%, controls
  −3.2/−0.2/+1.3%; report−2.9/control−1.6%. Sync text+4.94/control+2.11%,
  JSON+0.33/control−2.62%; all original adverse controls retained.
- Retaineda3aa2347 (yield check confined to its opcode): table above. It is a
  different source; timing differences between these runs do not isolate the
  yield test's CPU cost. Four owned services complete138.272/138.404/138.388/138.939s.

The first unpack failed before measurement because the VM disk was full. Verified
local copies of obsolete worker images permitted deleting only disposable remote
staging; historical raw observations were preserved. Original failure log remains.
All measurement services exit0, quiet gates pass, units inactive and shared lease
independently free. No abandoned matching or benchmark workers.

## Validation and identities

Focused Release **5,232 public and624 white-box tests per actual .NET11/.NET10**
pass on retained source, including real async protocols/host operations, generators,
closures/defaults/type descriptors/imports, shared-cache mutation, managed stack
admission, terminal first refusal, cancellation, retained aliases and awaited
cursor cleanup/budget reuse. Existing semantic/lifetime coverage is reused; no fuel
or internal-dispatch architecture tests were added. **Full delivery41642981787 PASSED** at exactb087c7b8: Windows/Ubuntu.NET11/
.NET10, installed Release consumers and original Stress on both platforms.
[Cached exact-source receipt](delivery-ci-status.json); do not re-query this terminal run.

Quiet Azure Standard_F4as_v7, Ubuntu24.04.4, SDK/runtime.NET11RC3 as in B161.
Baseline source873ae625/corea890ccf44ea9ee7ccbae436cfc64b39bb6367191f1a9aaf42c8ef5388ac2da79.
Retained corea3aa23470c2437acf40e4d26db47bdc1b9e3bd35b62b5e0aeb3dd783fcfe54a6,
built before the matching source commit. Source hashes identify exact content;
auxiliary binaries are identical. Frozen identities describe pre-delivery status.

[Retained assessment](retained-assessment.json), [initial](assessment.json),
[crossover](confirmation-assessment.json), [span-corrected](final-assessment.json),
[retained identities](identity-retained.json), [source patch](source.patch),
[source variants](source-variants.tgz). Raw observations: [initial](observations.tgz),
[crossover](confirmation.tgz), [span](final.tgz), [retained](retained.tgz).
[B161 same-source API comparison](../2026-11-03-async-execution-strategy/README.md)
is separate from these source pairs.
