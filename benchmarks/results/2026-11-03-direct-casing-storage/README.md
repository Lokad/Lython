# Direct immutable UTF-8 casing storage (B178)

Retained after one sizing refinement and one controlled confirmation, 2026-11-03.
Baseline production source7c183083 (test-only HEAD e27a1176), core
`bf2ddeb1d6adb8b4dccda00e0980225502b3a5a92e8aec019ab753ac3a6e69ed`.
Exact-size prototype core`1e2e4efe11af6f5f8aaaa2b290e67588de23d574c23c662bfa9fbd03d8c380fb`;
retained conservative-capacity core`1daa28e48982052e006ce3a6b00ceebdc153b2cc874be72d4dbd0ea70d52d2cb`.
These frozen PRECOMMIT identities plus source patches describe the actual builds;
later SourceLink revision metadata can change binary hashes without runtime changes.
No CPython refresh, current-HEAD multiplier or general CPU-gain claim.

## Design and decision

All five methods use one immutable output backing, prefunded capacity+metadata,
static generated UTF-8 expansions and one mapping pass. Casing's mutable builder,
growth/replacement and late string-header admission disappear. Existing Unicode
selection, Python3.13 preservation, full expansions, sigma context and title rules
remain. A cheap bounded byte scan admits conservative capacity; ASCII keeps exact
capacity, non-ASCII reserves room for a scalar or the largest generated expansion.
Unused capacity remains funded. Earlier denial is an explicit contract tradeoff.
The existing prefunded PyString factory accepts logical length, committing ALL
backing capacity; no new owner, pool, cache, tracing or hidden allowance.

The exact-size first prototype mapped twice. It passed correctness and cut Unicode
allocation51.6%/accounted peak198,520B, but the original long-Unicode diagnostic ran
48–55% slower. That adverse result motivated the byte-only sizing refinement;
all original samples and source remain in exact/. No worklist or duplicated Unicode
mapping database was introduced to save computed results.

## Complete representative jobs

Three independent processes/job, same frozen auxiliary worker bits and unchanged
old control, normal GC/tiering. Dedicated Standard_F4as_v7, Ubuntu24.04.4,
.NET11.0.0-rc.3.26522.148. Three-second adaptive warmup, nine rotated200ms target
batches and one-second settle. Warm compiled execution only: invocation setup,
actual mediated tmpfs file I/O, full stdout/file checks and previous-result cleanup
included. Fixture reset, compilation/startup/transport and final result/program/
fixture/process teardown excluded; every worker is joined after cleanup.

| Refined complete job | Run elapsed vs old | Old control vs old | RunAsync elapsed vs old | Old control vs old | Allocation change, both APIs |
| --- | ---: | ---: | ---: | ---: | ---: |
| CSV4,096 records |+0.54%|+5.59%|−3.73%|−3.60%|Flat|
| Text3,072 lines |−3.67%|+2.23%|−9.81%|−3.67%|−5.92%|
| JSON4,096 records |−0.38%|−1.70%|−1.60%|−2.56%|Flat|
| Report3,072 records |−1.29%|−1.34%|+0.67%|−1.34%|Flat|
| Config7,177B |+1.85%|+0.76%|−2.17%|−2.32%|Flat|

Text candidate is3.7–5.8% below the two synchronous old roles and6.4–9.8% below
the asynchronous old roles. Within-worker text IQR0.26–2.22%. Treat this as a
bounded elapsed result, not a certified speedup or isolated dispatch CPU saving.
Other complete cases are near moving controls; do not attribute their drift to
casing. Prior B174 CSV/B171 report debts remain accepted, not erased by these
independent comparisons. Whole-script performance remains open.

Allocation is a SEPARATE existing shared-ALC diagnostic:32 warmups/image, five
rotated16-job batches,200ms settle; all allocations across the process are sampled.
It includes fixture reset/complete verification/result disposal. Isolated Lython
images share CoreLib/hash state/GC; short elapsed/CPU here is diagnostic only.
Text allocation falls about172KB/job; accounted execution/root peak rises4,096B
from conservative capacity. Long ASCII allocation changes only fixed bookkeeping;
expanding Unicode allocation falls19.95–20.05%, accounted peak falls14,200B.
All balance/identity/full golden/fixture-cleanup checks passed.

## Controlled long-input confirmation and uncertainty

The refined allocation diagnostic still suggested Unicode elapsed+6–16%, against
moving old roles. A final separate-process confirmation uses complete scripts
asserting all five outputs against independent CPython3.13.2 literal goldens,
with stdout`ok`, empty file fixtures and the SAME protocol2 timing boundary.
Input64KiB ASCII /32,768 Unicode scalars, repeated mappings; large golden literals
are compiled outside warm clocks and are included in retained-code funding.

| Confirmation | Candidate vs first old | Candidate vs second old | Old control difference | Interpretation |
| --- | ---: | ---: | ---: | --- |
| Unicode Run |−0.79%|−1.18%|+0.39%|Near unchanged; IQR0.33–1.47%|
| Unicode RunAsync |−0.09%|−1.85%|+1.79%|Near unchanged; IQR0.44–0.67%|
| ASCII Run |+36.12%|−16.72%|+63.44%|INCONCLUSIVE identical-binary controls|
| ASCII RunAsync |+35.70%|−1.62%|+37.94%|INCONCLUSIVE identical-binary controls|

The Unicode diagnostic regression is not confirmed under this method. The ASCII
process controls disagree substantially despite low within-worker IQR; cause is
UNRESOLVED. Do not average methods or select the favorable old role. No unchanged
retime or third source variant is selected. Retain the ownership simplification,
text allocation/elapsed result, conservative capacity tradeoff and these explicit
uncertainties. Future evidence can justify bounded refinement or correction.

## Validation and evidence

Focused Release219 internal/75 public tests PASS on actual .NET11 and.NET10:
all102 expansion goldens, five-method contextual/digraph/astral/unassigned/sigma
cases, logical prefixes with invalid unused tails, refusal/first attribution,
input/result funding, cleanup/reuse and cancellation BEFORE a256KiB allocation
when admission cancels. Copy/growth-stage-only casing fixtures were consolidated
onto real output admission; independent assertions remain. Full delivery qualification is GREEN on exact737a67e9: Windows/Ubuntu, actual
.NET11/.NET10 Release, installed package consumers and original Stress.
[Terminal run41709980798](https://github.com/Lokad/Lython/actions/runs/41709980798)
and delivery-ci-status.json retain the eight passed gates. No terminal re-query.
Publication remains postponed. This receipt does not qualify later prototypes.

Exact/refined owned collections203.164/207.448s; final confirmation67.038s.
All final quiet gates/goldens/joins passed. Evidence is persistent, RAM is only
for guest fixtures. Raw archives include every sample, identity, warmup, filesystem
and quiet snapshot. Archives' SHA256 verified locally after retrieval. Initial
control-worker launch failed BEFORE timing because an allocation-only catalog
lacked protocol fixture size/text metadata. Its original catalog, empty receipt
and journal remain in controls/observations.tgz; protocol metadata was corrected
without changing scripts/goldens and the fixed owned collection completed.
The collector's original cleanup assertion hid worker stderr; direct reproduction
reported`KeyNotFoundException: size`. No failed timing observation was discarded.
