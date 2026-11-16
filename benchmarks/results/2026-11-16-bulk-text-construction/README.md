# Direct bulk-text output prototype: original adverse result

R429/B825, 2026-11-16 UTC. **Uncommitted5ebd657a** versus committed
**0a04e253 / measured810dafdd**. 598 source/build/test inputs and full runtime
images frozen. Nonincremental Release succeeds;145 internal/412 public focused
checks per actual.NET11/10 pass.16 Unicode/chunk goldens independently match
CPython3.13.2. Baseline full43800910729 remains pending. Prototype is not retained
or fully qualified; one source-backed refinement follows, preserving this result.

Replace expandtabs' scalar decoding/check/append with measured native UTF8 literal
runs and one final admitted value. Replace already-drained join's iterator/growing
builder with validation/sizing and one final admitted value. Remove JoinStrings
and EnumerateJoinParts; preserve drain effects/error order, native string coercion,
Unicode columns/CRLF, bounded cancellation, refusal and live input/result owners.
Exact final capacity changes tiny admission thresholds; no new policy/framework.

Local natural-GC complete runs: normalization allocation **2,171,238→1,922,892B
(−11.44%)**; accounted execution peak **2,606,412→2,515,276B(−3.50%)**.
Report and invoice allocation/peaks exactly flat;JSON−560B, no material reduction.
No local elapsed claim. Shared budgets return to compiled baseline and then zero;
complete stdout/file goldens pass. Peaks are accounting estimates, not heap/RSS.

Quiet VM, two processes/source, five-second full-request warmup and five rotated
approximately half-second rounds. Four jobs **140.165s**:

| Complete workload / phase | Elapsed candidate/reference | Wider CPU | Process-median spreads reference/candidate |
| --- | ---: | ---: | ---: |
| Normalization warm | +5.34..+7.50% | +4.20..+5.97% | 0.81/1.23% |
| Normalization compile-to-result | −2.21..+4.00% | −5.07..+1.66% | 2.19/4.07% |
| Method report warm | −1.91..+0.14% | −4.16..+2.27% | 1.99/0.10% |
| JSON warm control | −1.08..+1.04% | −1.26..0.00% | 0.83/1.30% |

All four median pairings, not confidence intervals. **Warm normalization regresses
with CPU support and stable controls**; allocation benefit does not establish a
performance improvement. Fresh/report uncertain. A plausible new cost is join's
generic repetition helper and repeated checks for every tiny private fragment.
Refine its direct bounded copies/item-loop checkpoints once, retaining all storage,
semantic and cancellation guarantees; this is a hypothesis, not established cause.
No source promotion or broader performance completion claim.

Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,.NET11RC3
ordinary tiering/natural workstation GC. Warm includes mediated RAM I/O, full
stdout/file goldens and prior-result cleanup, excludes compilation/startup,
reset/protocol and final cache teardown. Fresh adds compilation/compiled-owner
disposal; wider CPU includes reset/protocol. Separate medians are not additive.
No pending-I/O, contention, heap/RSS or embedding-tail claim.

All image/golden/quiet/budget/Closed/exit/join/archive gates pass; no VM users/RAM
fixtures remain. Existing immutable catalog linked, not restaged. Only the obsolete
9d26 core's three exact Git-archived VM aliases retired after fresh complete proof;
810 baseline/new candidate/toolchains and254/252MiB floors retained. Terminal cached.

[Original timing](summary.json), [allocation/peaks](allocation-summary.json),
[raw observations](observations.tgz), [frozen source/images/checks/procedure](source-checks-and-procedure.tar.xz).
