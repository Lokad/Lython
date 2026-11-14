# Native integer workspace lifetime — bounded cleanup refinement

R365/B751, 2026-11-14 UTC. Initial frozenff2b5d76 versus fully qualified
fa40d674/frozen73a1b233, two production inputs of582. Development prototype;
no final retention/delivery claim. Reuse the existing one-idle-workspace policy
for long[] loans instead of allocating an array/temporary reservation per wide
native prefix. Small prefixes keep their fixed managed words. Surviving objects
materialize before the loan returns; no primitive span survives genuine I/O.

Idle capacity stays charged until eviction or execution cleanup. Active/nested
loans survive ending idle reuse and release only after their users/cleanup finish.
The existing64+16*capacity charge is conservative versus the prior32+8*capacity
temporary charge; retention/earlier denial are explicit compatibility tradeoffs.
No independent allowance, policy/cap, new pool framework, frame scalar system,
tracing or forced GC. Cache misses check cancellation after admission/before
allocation; first refusal is terminal. Returned private words contain no references.

Read-only existing compiled-shape inspector finds invoice12/6-slot helpers and
only two private computed inputs before its first rounding call. That does not
justify a broad primitive calling convention or the claimed removal of returned
identities. Fulfillment's price helper needs23slots. Its old conservative array
reservation totals884,736B per4,096calls: **accounting charge, not physical CLR
allocation**. The preliminary plan/commentary conflated those sizes; corrected
here and in active PLAN. No physical array-size or causal CPU attribution inferred.

698focused actual .NET11/10 checks pass, including four loan-lifetime/refusal/
cancellation cases and four public alternating-wide-helper executions (both APIs,
earlier values/three sequential executions/shared-budget cleanup).102isolated
CPython3.13.2 controls and18frozen-worker goldens match. Separate fresh-process
allocation: fulfillment6,745,185→5,762,330B/run (−14.571%); invoice/observed flat.

## Initial complete-script comparison

Quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45/16GiB/no swap/.NET11RC3; normal
GC/tiering, fixed workers/adapter0.4. Four fresh processes/job, full-request
5swarmups (12text), five rotating0.5sbatches. B751 completes sevenjobs270.865s/
all image/catalog/stdout/files/budget/quiet/awaited-cleanup/persistent-archive gates.
No users/fixtures remain. RawSHA256
`2a7008dce50060f942cadec729e8364bfc35292eef49b10f3a990c607399c8b5`.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Fulfillment, warm |−2.76..+0.48%|−1.67..0.00%|
| Fulfillment, compile-to-result |−2.65..−0.24%|−3.85..0.00%|
| Invoice, warm |−2.50..+0.57%|−1.49..+0.45%|
| Captured text |−2.44..+2.16%|−0.44..+0.70%|
| JSON helpers |−1.72..+1.77%|−2.07..+2.71%|
| Short/zero helpers |+0.62..+2.91%|+1.22..+2.71%|
| Helpers exposing intermediates |+0.09..+1.06%|−2.20..+1.73%|

All process-median pairings, not confidence intervals. Text reference spread3.07%,
JSON2.92%; keep both uncertainties and stable short-helper adverse direction.
Warm includes mediated RAM I/O/full output and file validation/result cleanup,
excludes compilation/startup/reset/protocol/final cache teardown. Fresh adds
compilation and compiled-owner disposal. Wider CPU includes reset/protocol.
No universal/neutrality/currentCPython/cumulative improvement claim.

## Selected cleanup refinement

Remove the added nested finally from every helper. Wide native word capacity
implies code capacity>16 and therefore existing bound storage; only small fixed-
values calls can rent continuation storage. These cleanup owners cannot overlap.
Use one mutually exclusive retirement branch in the existing finally. Preserve
all awaiting/materialization/first-refusal/managed-stack behavior; no new helper,
controller or configuration. Source proof justifies this simplification; it does
not prove the added EH tier caused the short-helper timing. Keep initial image/
conditions/adverse points; focused actual11/10 checks and bounded changed-source
affected/short controls decide final retention/debt, not a rigid percentage veto.

Persistent evidence is verified under the lease, current images/catalog/toolchain
and254/252MiBfloors preserved, no retirement. A local PowerShell helper-generation
quoting failure occurred before any build/test/timing and was corrected with a
file-based generator; no runtime inference. Publication remains held; broader
call/record/text/frontend roadmap and independent regex development continue.

[Identity](initial/candidate-identity.json), [timing](initial/timing-summary.json),
[allocation](initial/allocation-summary.json), [checks](initial/focused-checks.json),
[prepared facts](initial/prepared-shapes.json),
[all source/binary/raw/conditions](initial/sources-checks-and-observations.tar.xz).
