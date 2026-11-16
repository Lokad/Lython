# Immutable-fact kernel refinement: unpromoted

R439/B840,2026-11-16UTC. Changed4f7385cc versus qualified0443b5cf.
Only the existing kernel changes from91ca: it carries immutable Code and derives
its fixed storage/loop boundary once. Mutable PC,live extent and cells remain
actual shared metadata. No stored mode,controller,owner,policy or new engine.
This refinement **does not clear regression and is not promoted**.

| Complete job | Candidate elapsed change | Wider CPU change | Ref/candidate control spread |
| --- | ---: | ---: | ---: |
| Normalization warm | +2.22..+7.42% | +2.64..+7.86% | 2.40/2.63% |
| Normalization compile-to-result | +0.09..+3.99% | +3.67..+4.65% | 2.04/1.81% |
| Captured-helper text | +4.94..+15.98% | +4.97..+16.28% | 5.37/4.89% |
| Protected search | +2.55..+12.24% | −0.06..+10.57% | 7.85/1.48% |
| CSV invoice | −0.43..+5.21% | 0.00..+4.23% | 4.60/1.00% |
| JSON helpers | +1.21..+2.93% | +1.43..+3.81% | 1.41/0.30% |

All four independent process pairings remain visible. Normalization/JSON are
consistently adverse. Moving controls widen captured/search/CSV uncertainty.
Do not infer a causal refinement gain by comparing separately collected B838
and B840. Both source conditions and all adverse originals stay preserved;
there is no unchanged retiming or automatic permanent design ban.

Eight local full warm diagnostics pass goldens/shared-budget baseline cleanup.
Allocation remains flat(all absolute changes<0.05%); accounted peaks unchanged.
Both-target nonincremental Release build and143internal/45public per actual11/10
focused semantic/reentry/stack/cancellation/refusal/cleanup checks pass.609source
inputs and core remain byte-exact through comparison. This is not full platform,
stress or installed-package qualification; that remains at retained delivery.

The next coherent correction removes full ExecutableWorkState construction from
private calls. The existing kernel will borrow actual instruction index and
storage directly; immutable Code/closure-cell references remain facts. Remove
work-state-specific wrappers from common publication/operand/closure helpers.
Keep the existing persistent work/frame for genuine suspension and return/finally
routing; no additional controller,type-dispatch framework,mode,owner or copied PC.
Preserve all existing admission and awaited cleanup. This is a source-backed
eliminated-storage hypothesis,not a measured explanation of the adverse timing.
Stop that migration at working primitive borrows,focused correctness and one
bounded changed-source comparison; explicitly decide regression debt at retention.

The exact same five-case catalog and six jobs as B838 complete206.688s with
all image/golden/quiet/Closed/exit/join/archive gates. No users/RAM fixtures remain;
B840terminal is cached,never requery. Same quiet F4as_v7,.NET11RC3,tiering,
workstationGC and mediated RAM I/O boundaries as[original](../README.md).
Warm excludes compilation/startup/reset/protocol/final cache teardown; fresh
includes compilation and compiled-owner cleanup. Wider CPU includes protocol/reset.
No CPython refresh,startup,physical heap/RSS,pending-I/O or embedding-tail claim.

Only exact committed archived replicas were retired after fresh UID/path/hash/
link/all-alias proof:91ca's two image aliases,B839working raw archive and B835
working catalog. Canonical evidence,current0443baseline,toolchains and254/252MiB
floors remain. Original source/images/conditions are[archived](procedure-and-image.tar.xz).

[Raw](observations.tgz),[summary](summary.json),[allocation](allocation-summary.json),
[checks](focused-checks.json),[sources](sources-and-checks.tar.xz).
