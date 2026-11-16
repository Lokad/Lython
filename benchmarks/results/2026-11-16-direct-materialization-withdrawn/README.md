# Direct materialization transport: withdrawn

R438/B837,2026-11-16UTC. Isolated **e0d2bdea** versus fully qualified
**312f5911 /0443b5cf**. Withdraw the complete prototype; normal runtime unchanged.
This does not close the larger call/frame/operand performance objective or forbid
a changed materialization design with new evidence.

Hypothesis: use existing Cursor directly in object materialization, removing
CheckedSequence/AsyncSequence and CLR-yield transport around Python iterators.
One DrainLease capacity-growth rule replaces two copies. Initial list admission
precedes live iterator acquisition, with earlier refusal and awaited cursor disposal.
No new owner/controller/policy/cache; genuine enumerable consumers remain.
However, four entry/cleanup wrappers and a shared per-item append call add work
and35production lines. The wrapping classes still have genuine other consumers;
this does not displace a whole execution stage. No source cause is isolated by timing.

| Complete job | Candidate elapsed change | Wider CPU change | Ref/candidate process-median spreads |
| --- | ---: | ---: | ---: |
| Normalization warm | +2.90..+4.56% | +1.89..+3.86% | 0.06/1.55% |
| Normalization compile-to-result | −6.58..+0.05% | −6.12..+1.63% | 6.24/0.80% |
| Generator draining warm | −3.89..+6.36% | −3.41..+6.00% | 1.63/8.88% |
| CSV invoice control warm | −2.38..+2.84% | −0.56..+2.78% | 4.51/0.81% |
| JSON aggregation control warm | −0.15..+1.25% | −1.96..+1.42% | 0.03/1.37% |

Five jobs/173.539s, all image/golden/quiet/Closed/exit/join/archive gates pass.
Two independent processes per source;5s full-request warmup and five rotated~0.5s
rounds. Four pairings, not confidence intervals; maximum within-process IQR2.98%.
Warm normalization is adverse with supporting CPU and stable reference. Fresh,
drain and CSV controls are inconclusive; moving controls establish uncertainty,
not failed semantic correctness. All originals remain. No unchanged retiming or
permanent one-attempt rule follows from withdrawal.

Separate local seven-job natural-GC allocation is effectively flat (largest
absolute change<0.02%); all accounted peaks unchanged. Existing focused checks:
1,092internal/1,897public plus202internal/820public affected consumers per actual
.NET11/10, overlapping filters not summed. Six additional cases per target check
pending disposal/cancellation funding and live admission/replacement/refusal.
609source inputs frozen after nonincremental Release build. New test namespace
compile error corrected before its tests; original log preserved.

The four acquisition-order cases specify the proposed contract and are withdrawn
with it. Two pending-cleanup cases assert independent ownership behavior and will
be checked against the retained runtime. No admission contract/API change is shipped.
No full platform/package qualification is needed for the withdrawn product prototype;
qualified0443remains normal. Future larger redesign must preserve these lifetimes.

Quiet F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural
workstation GC/normal tiering. Warm includes mediated RAM I/O,full stdout/file
goldens and prior-result cleanup; excludes compilation/startup,reset/protocol and
final cache teardown. Fresh includes compilation and one execution; wider CPU includes
reset/protocol. No CPython/source-additive/heap/pending-I/O/embedding-tail claim.

Evidence persisted and independently verified before RAM teardown; no users remain,
terminal cached. Only one exact committed historical archive replica retired after
fresh lease/UID/path/hash/all-alias proof: B445observations.tgz equals nested
b445/observations.tgz in committed prepared-consumers observations.tar.xz. The
initial direct-file custody lookup failed safely before VM mutation; corrected
proof did not change measurement conditions. Current0443/e0images and toolchains,
254/252MiB floors preserved. Publication/tagging held.

[All controls](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[source/tests](sources-and-checks.tar.xz),[images/procedures/original conditions](procedure-and-image.tar.xz).


Both independent pending-cleanup cases pass on the retained runtime per actual11/10.
[Source/check proof](retained-runtime-checks.json),[logs](retained-runtime-checks.tar.xz).
The production tree is identical to qualified312f5911; no unchanged full qualification
was repeated. Only this independent coverage is integrated.
