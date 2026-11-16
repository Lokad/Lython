# Shared line construction: original unqualified timing

R430/B827,2026-11-16UTC. **Uncommittedecd9e956** versus committed6cedc733 /
measuredefd82707.600 source/build/test inputs frozen. Nonincremental Release and
487 internal/1,656 public eachactual.NET11/10 pass;4 related original-scale stress
rollback cases pass per framework, preserving all original cases.44 Unicode/chunk/
keepends goldens independently match CPython3.13.2. Full new-runtime qualification
pending; no source retention/promotion decision yet.

Splitlines now uses existing range loans and the same final aggregate list/fragment
constructor as ordinary split. Delete incremental PyList growth, per-fragment
admission and per-scalar Rune decoding. Bounded native UTF8 searches handle Python's
complete line-break set, CRLF/lookahead/chunk boundaries, keepends/trailing rules.
Explicit context reaches sync/async/guest-truth paths. Fix the source-demonstrated
no-break scan without internal cancellation checks. The new entry/context regression
and joined large-construction stress are distinct from measured scan-interruption
latency. Every scan/copy chunk checks the active token; no fuel/policy counter.

Final storage admits before allocation; unpublished work refunds on refusal/cancel.
Independent fragments remain funded, whole unchanged same-owner input can be reused.
Existing cached range capacity stays charged and can deny earlier; exact final
capacity may admit earlier. No new cache, scope, per-object ledger, window ownership
protocol or dispatch framework. Scalar/cased/separated splits share one final
constructor, preserving their current semantics and callback order.

Local natural-GC complete normalization allocation **1,922,704→1,889,456B(−1.73%)**;
accounted peak2,515,276→2,515,196B(effectively flat). Report/invoice exactly flat;
JSON−566B, not material. Full stdout/file goldens and budget baseline/zero pass.
Accounting peaks are not heap/RSS. No local timing claim.

All four VM jobs finish, all20 samples/job, image/golden/Closed/join gates pass.
**Final quiet-machine gate fails:** one0.503s idle window is4.975%busy, violating
the unchanged3%median criterion (steal0/no swap). The original service exits1;
complete=false and absent total elapsedSeconds are preserved. Results favor the
candidate but are **unqualified**:

| Complete workload / phase | Elapsed candidate/reference | Wider CPU | Ref/candidate process-median spreads |
| --- | ---: | ---: | ---: |
| Normalization warm | −9.29..−3.98% | −8.57..−3.15% | 2.98/2.78% |
| Normalization compile-to-result | −6.61..−5.01% | −6.27..−3.43% | 1.34/0.37% |
| Method report warm | −0.64..+1.65% | −1.09..+2.54% | 1.11/1.18% |
| JSON warm control | −2.00..+0.78% | −1.42..+0.71% | 1.95/0.86% |

All four pairings, not confidence intervals. A failed gate establishes uncertainty,
not a failed design. One bounded unchanged-source/image confirmation is justified;
retain this original failed condition and all controls. Do not claim qualified CPU
improvement or silently replace the final gate with a later idle observation.

Quiet before, Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,
.NET11RC3 ordinary tiering/natural workstation GC. Two processes/source,5sfull-request
warmups/five rotated approximately0.5sbatches. Warm includes mediated RAM I/O,
full stdout/file goldens and prior-result cleanup; excludes compilation/startup,
reset/protocol/final cache teardown. Fresh adds compilation/compiled-owner disposal;
wider CPU includes reset/protocol. No heap/RSS, contention or embedding-tail claim.

After failure, a fresh exclusive lease verifies every original byte into persistent
archive before fixture unmount. Receipt construction initially referenced missing
elapsedSeconds after teardown; that failure stays recorded. Another fresh lease
verifies every archive member against persistent working files; receipt preserves
missing elapsed as null. No workload reexecution or abandoned user/trace; archive
hash verified locally. Original failed terminal cached. Only exact Git-archived810
VM aliases and redundant working evidence copies retired; currentefd/ecd images,
toolchains and254/252MiB floors retained. Publication held; broader objective open.

[Unqualified original controls](summary.json),[allocation](allocation-summary.json),
[failed gate/raw jobs](observations.tgz),[source/images/checks/original conditions](source-checks-and-procedure.tar.xz).
