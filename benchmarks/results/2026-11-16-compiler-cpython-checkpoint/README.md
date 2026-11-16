# Current manifest and report CPython checkpoint

R425/B821,2026-11-16UTC. Runtime **a2d20928 / measured9d26d408** versus stock
**CPython3.13.16**. Committed source has590verified production/build inputs;
151internal/1,011public focused checks per actual.NET11/10 pass. Full43789371043
platform/stress/installed-package qualification is pending at capture. No source
edit, new adapter qualification or causal cross-collection speedup is claimed.

Reuse the existing complete six-helper configuration/manifest (833Binput,
1,877Bsource) and JSON-to-Markdown reports (1,024records/189,441B). No generated
normalizer in this external comparison. Sources, fixtures and independent output/
file goldens are unchanged; the actual additional-catalog method report matches
all four planned hashes. No framework, fixture or golden redesign.

| Workload / phase | Lython ms | CPython ms | Lython/CPython | L/P control spread | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| Configuration / six-helper manifest / compilation only | 0.192–0.201 | 0.157–0.157 | 1.22–1.28× | 4.89/0.08% | 1.25% |
| Configuration / six-helper manifest / compile and execute once | 0.496–0.498 | 0.456–0.457 | 1.08–1.09× | 0.53/0.16% | 1.67% |
| Report / format method / warm Run | 4.155–4.213 | 2.560–2.616 | 1.59–1.65× | 1.38/2.16% | 3.99% |
| Report / f-string / warm Run | 2.786–2.822 | 2.350–2.356 | 1.18–1.20× | 1.28/0.26% | 4.50% |

All four process-median pairings, not confidence intervals. **136.664s**
for four jobs, two processes/engine,5s full-request warmups, five rotated approximately
0.5s batches. Manifest compilation's unchanged Lython control moves4.89%; report
IQRs reach4.50%. Keep all adverse observations and moving controls; exact ratios
are diagnostic. Source-pair improvements cannot be inferred from earlier collections.

Quiet Azure F4as_v7/Ubuntu24.04.4, four AMD EPYC9V45vCPUs/16GiB/no swap,
.NET11RC3 ordinary tiering/natural workstationGC and CPython GIL/GC.
Warm includes Run, mediated RAM I/O, complete stdout/file goldens and prior-result
cleanup; excludes compilation/startup, reset/protocol and final cache teardown.
Compilation includes public compile/compiled-owner disposal with no guest I/O.
Compile-to-result includes compilation, one execution and compiled-owner disposal.
Separate phase medians are not additive. Wider process CPU includes reset/protocol.
CPython native buffered files do not provide equivalent containment; this is whole
embedding cost, not isolated interpreter CPU. No startup, pending-I/O, contention,
heap/RSS or embedding-tail comparison.

This refreshes older manifest/report warm rows after format consolidation/compiler
storage. Method formatting retains a material warm gap; manifest fresh is closer.
Report compilation/fresh, other workloads and all historical uncertainty keep their
actual earlier revision labels. Do not infer regression-debt recovery or general
Python parity. Broader call/operand/collection/text/frontend work remains independent.

All source/image/catalog/golden/budget/quiet/Closed/exit/join/archive gates pass.
Persistent archive verifies under lease before teardown; no workers/RAM fixtures
remain. No retirement or floor change; current images/toolchains and254/252MiB
floors remain. Terminal cached forever; publication held.
[Summary/all controls](summary.json),[identity](identity.json),
[raw observations](observations.tgz),[procedure/inputs/source proof](procedure-and-inputs.tar.xz),
[previous guide](prior-BENCHMARKS.md),
[measured runtime source pair](../2026-11-16-compiler-instruction-store/README.md).
