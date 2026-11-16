# Retained shared line construction: bounded confirmation

R430/B828,2026-11-16UTC; unchanged **ecd9e956 versus6cedc733 /efd82707**.
Same600 frozen source/build/test inputs, runtime images, catalogs, complete goldens
and focused checks as the [original B827](../README.md). No source refinement or
repeated correctness qualification. Baseline R429 and this change await one combined
full platform/stress/installed-package delivery; publication held.

All four jobs pass image/golden/budget/quiet/Closed/exit/join/archive gates in
**140.323s**, two processes/source,5s full-request warmup/five rotated approximately
0.5s rounds. The original failed final idle gate stays failed; this is an explicitly
separate bounded confirmation, not substitution of a later gate into the first run.

| Complete workload / phase | Elapsed candidate/reference | Wider CPU | Ref/candidate process-median spreads |
| --- | ---: | ---: | ---: |
| Normalization warm | −8.53..−3.81% | −8.29..−5.03% | 2.09/3.00% |
| Normalization compile-to-result | −6.30..−3.36% | −3.46..−2.23% | 0.92/2.20% |
| Method report warm | −0.48..+0.52% | −0.83..+1.13% | 0.82/0.19% |
| JSON warm control | −4.79..+0.37% | −4.63..+2.00% | 4.99/0.41% |

All four pairings, not confidence intervals. Normalization shows supporting CPU
evidence in both phases. JSON's unchanged reference moves4.99%; its favorable
pairings are not a source gain. Keep all controls/adverse CPU and original failure.
Separate source-pair medians cannot be added to prior gains.

**Retain** the shared final split constructor and bounded native line scanner:
remove incremental list growth, per-fragment admission and per-scalar decoding;
explicit current context fixes the old no-break cancellation gap. Existing range
loans stay charged. No new cache/ownership/dispatch framework. Python's full line
set, CRLF/keepends/trailing semantics, chunk boundaries, truth callbacks, owners,
terminal refusal and rollback are preserved. Whole unchanged same-owner input can
be reused; conservative range capacity/overlap may deny earlier and exact final
capacity may admit earlier. No tiny-budget threshold compatibility promise.

Original local allocation: normalization−1.73%, accounting peak effectively flat;
report/invoice flat, JSON immaterial. No repeated allocation measurement for unchanged
images.487internal/1,656public and4related original-scale stress cases pass each
actual.NET11/10;44CPython goldens agree. Cancellation entry/context coverage and
joined construction stress do not establish a measured scan-interruption latency.
Scan/copy chunks check the active token; CLR allocations/copies are not preemptible.

Same Azure F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
ordinary tiering/natural workstation GC. Warm includes mediated RAM I/O, full
stdout/file goldens and prior-result cleanup; excludes compilation/startup,
reset/protocol and final cache teardown. Fresh adds compilation/compiled-owner
disposal; wider CPU includes reset/protocol. No heap/RSS, pending-I/O, contention
or embedding-tail claim; accounting peaks describe estimates.

Persistent archive verified under lease before unmount; all users joined, no RAM
fixture remains. Only exact committed copies of B827 working evidence retired;
both current images/catalogs/toolchains and254/252MiB floors preserved. Terminal
cached. Broader performance work remains open; next source gate must displace a
complete representation, not repeat already-delivered builtin/JSON construction.

[All controls](summary.json),[raw observations](observations.tgz),
[procedure/baseline source proof](procedures-and-source-proof.tar.xz),
[original source/images/checks/allocation](../README.md).
