# Complete file-normalization CPython checkpoint

R427/B823, 2026-11-16 UTC. Runtime **116fe6e0 / measured810dafdd** versus
**CPython3.13.16**. The 593 production/build/test inputs match committed source.
This comparison uses the unchanged R426 core. Its initial full delivery later failed
on six stale accounting expectations and one Windows process-allocation assertion;
test-only corrections are being qualified. Measurement gates do not establish full
runtime qualification. Original identity text saying CI was active describes capture
time; retain it unchanged.

One complete Unicode file-normalization script reads 52,224 bytes / 4,096 lines,
normalizes mixed newlines and tabs, trims lines, joins and writes 54,269 bytes /
4,095 lines. Café/emoji, CJK, Straße, Greek and blank lines are represented. Complete
stdout and file bytes agree with independently constructed goldens and CPython.
This fills a useful new workload gap; it is not a causal source-pair speedup.

| Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | ---: | ---: | ---: | --- |
| warm Run | 1.192–1.219 ms | 0.727–0.731 ms | 1.63–1.68× | Diagnostic; spreads 2.24/0.56%; IQR ≤6.70% |
| compile and execute once | 1.248–1.316 ms | 0.830–0.839 ms | 1.49–1.58× | Diagnostic; spreads 5.11/1.10%; IQR ≤7.35% |

All four process-median pairings are shown, not confidence intervals. Two jobs in
**71.314s**; two processes/engine, five-second full-request warmups
and five rotated approximately half-second batches. Lython's fresh control moves
5.11% and IQR reaches 7.35%. Keep the uncertainty in exact ratios; a material gap
remains and justifies one bounded current-source CPU capture before a redesign.

Quiet Azure F4as_v7 / Ubuntu24.04.4, four AMD EPYC9V45 vCPUs, 16 GiB/no swap,
.NET11RC3 ordinary tiering/natural workstation GC and CPython GIL/GC.
Warm includes Run, mediated RAM I/O, full stdout/file goldens and prior-result
cleanup; excludes compilation/startup, reset/protocol and final cache teardown.
Compile-to-result adds compilation, one execution and compiled-owner disposal.
Separate medians are not additive. Wider CPU includes reset/protocol. Native
CPython buffered files provide different containment; this measures embedding
cost, not isolated interpreter CPU. No startup, pending-I/O, contention, heap/RSS
or embedding-tail comparison.

All image/catalog/golden/budget/quiet/Closed/exit/join/archive gates pass. Every RAM
trace was archived and verified under the lease before teardown. No workers or
guest fixtures remain. An exact redundant historical catalog was retired only
after Git custody and fresh path/ownership/hash checks; toolchains/current images
and the original 254/252 MiB floors remain. Terminal cached; publication held.

[All controls](summary.json), [identity](identity.json), [raw observations](observations.tgz),
[procedure/inputs/source proof](procedure-and-inputs.tar.xz), [previous guide](prior-BENCHMARKS.md),
[runtime source pair](../2026-11-16-consuming-text-completion/README.md).
