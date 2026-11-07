# Pending memory admission publication — R53

2026-11-07, baseline production55e510ff, parent documentationd7df3147. This is a
correctness correction discovered after R52, not a performance claim.

Two native regressions fail on the baseline: a host after-admission callback
allocating a retained string, or completing temporary scratch, resizes the aggregate
lease from stale counters. The outer string then remains live without its funding;
another shared reservation can exceed the allowance's actual retained payload.

Publish pending bytes before external code, centralize host resize publication,
and roll back only this operation's unused charge. Same-governor allocation is
permitted after the changed host estimate is visible; earlier allocation fails
terminally. Independent governors remain able to share the root. First refusal
survives callback suppression, and cleanup still refunds unused storage.
The initial correction also invoked a redundant refund after failure before
funding; a new red test demonstrated an unexpected retained value from that
callback. Final code skips unchanged rollback and no-op refunds.

Removed blind post-callback counter publication and unnecessary external calls.
No tracing, collector, identity ledger, new owner/pool, retry, quota, hidden allowance
or capacity-rounding policy. Public signatures are unchanged. Callback observation
and before-publication reentry behavior are intentionally clarified in the contract.

Final focused Release checks: **1,401 internal / 1,652 public per actual .NET 11
and .NET 10**, including ten small retained/scratch/refusal/exception/refund/shared-
root controls. First refusals retain their span and identity, pre-allocation failure
does not publish a string, later commands can reuse the allowance and all retained
callback values stay funded. Original baseline and refinement failures remain.

One short existing five-script diagnostic uses frozen baseline core83af401e and
the exact final cores/source inventory in [focused.json](focused.json). Normal
GC/tiering, isolated ALCs sharing CoreLib/hash seed/process GC, 32 warmups/image,
five rotated rounds of four jobs, sync/async separately. Complete public Run,
fixture reset, mediated I/O, full output/file checks and result cleanup; compile/
startup/final cached teardown excluded. All funding/isolation/cleanup gates pass.

Allocation is effectively flat (within0.08%). **Desktop elapsed is diagnostic:**
sync CSV is +3.31–3.60%; sync search +6.69–12.64% with5.58% identical-before drift.
Other cases are mixed, including async JSON+1.01–4.87%/3.81%baseline drift. Retain
these adverse observations as possible cost/uncertainty; do not claim CPU benefit
or retime unchanged until favorable. This fix preserves containment independently
of its timing. Primary CPython tables still identify source55e510ff, not R53.

Committed/pushed **d04024ee**, fully qualified by
[CI42393422539](https://github.com/Lokad/Lython/actions/runs/42393422539): Windows
and Ubuntu Release on actual .NET11/.NET10, original Stress on both targets and
installed core-package consumers. [Cached receipt](delivery-ci.json); no unchanged
qualification is repeated. Publication postponed.
[Original failing baseline](baseline.json), [initial focused checks](initial-focused.json),
[final checks and source hashes](focused.json), [diagnostic observations](local-summary.json),
[image/driver identity](comparison-identity.json), [decision/archive hash](decision.json)
and [complete source/core/log/raw evidence](development.tar.xz) preserve the record.
