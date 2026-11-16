# Retained direct bulk-text construction

R429/B826,2026-11-16UTC. **efd82707 versus0a04e253 /810dafdd**;598 source/
build/test inputs frozen. Nonincremental Release and145 internal/412 public each
actual.NET11/10 pass. Baseline [full qualification passes](../../2026-11-16-consuming-text-completion/delivery/README.md).
New runtime full delivery is pending at the next coherent checkpoint; publication held.

Both tab expansion and joined text allocate one measured final UTF8 value using
existing admission/ownership. Remove their growing byte-builder stages, JoinStrings
and EnumerateJoinParts. Private input drains remain funded; native coercion occurs
once. Tab columns count Unicode scalars; CR/LF resets, nonpositive widths and
chunk-boundary scalars preserve Python results. Copies/fills and native item loops
retain bounded cancellation; refusal is terminal and rolls back unpublished storage.
No cache, memory policy, public API or replacement dispatch framework is introduced.
Exact final capacity can admit earlier than geometric capacity/overlap; no identical
tiny-threshold promise. Existing genuine snapshots/builders remain where needed.

The [original prototype](../README.md) regressed warm elapsed5.34–7.50% with CPU
support despite less allocation. This refinement removes generic repetition setup
and redundant per-tiny-fragment checks from join, using direct bounded copies and
item-loop checkpoints. The changed helper is a hypothesis, not an isolated causal
measurement; original source, failed conditions and all adverse evidence remain.

| Complete workload / phase | Elapsed candidate/reference | Wider CPU | Reference/candidate process-median spreads |
| --- | ---: | ---: | ---: |
| Normalization warm | −2.58..+4.28% | −3.89..+3.81% | 4.56/2.37% |
| Normalization compile-to-result | −5.26..−1.29% | −7.09..−3.24% | 3.31/0.85% |
| Method report warm | −0.19..+1.78% | −0.30..+1.14% | 1.11/0.86% |
| JSON warm control | +0.96..+1.72% | −0.56..+2.90% | 0.18/0.58% |

All four pairings, not confidence intervals. Four jobs139.786s, two processes/source,
five-second full-request warmups and five rotated approximately half-second rounds.
Warm remains uncertain; the original warm regression is **not proven recovered**.
Fresh has modest supporting CPU evidence, with3.31% moving reference; report/JSON
adverse elapsed/mixed CPU are accepted debt. No universal speedup or embedding-tail
claim. Retain for coherent final-storage ownership and lower complete allocation;
the substantial performance objective remains open.

Independent local natural-GC allocation: **2,171,832→1,922,934B(−11.46%)**;
accounted execution peak **2,606,412→2,515,276B(−3.50%)**. Complete stdout/files
match independent goldens; shared accounting returns to compiled baseline then0.
No local timing claim. Report/invoice were allocation-flat in the original source
pair; they were not remeasured for this nonallocating copy/check refinement.

Quiet Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,.NET11RC3
ordinary tiering/natural workstation GC. Warm includes mediated RAM I/O, complete
goldens and prior-result cleanup; excludes compilation/startup, reset/protocol and
final cache teardown. Fresh adds compilation/compiled-owner disposal. Wider CPU
includes reset/protocol; separate medians are not additive. No pending-I/O, heap/RSS,
contention or embedding-tail comparison. Accounting peaks are not physical memory.

All image/golden/quiet/budget/Closed/exit/join/archive gates pass. Original5eb VM
image and redundant B825 JSON retired only after exact committed custody and fresh
path/ownership/hash/all-alias proof;810/new images/toolchains/floors remain. All
RAM bytes archived/verified under lease before unmount; no users/fixtures remain.
Terminal cached. Next milestone consolidates duplicate splitlines construction and
fixes its source-demonstrated no-break cancellation gap before further tuning.

[All timing/controls](summary.json),[allocation/ownership](allocation-summary.json),
[raw observations](observations.tgz),[source/images/checks/procedure](source-checks-and-procedure.tar.xz).
