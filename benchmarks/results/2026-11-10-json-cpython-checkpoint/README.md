# R197 — committed JSON/report/text CPython checkpoint

Measured 2026-11-10 on quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs,
16GiB/no swap, stock .NET11RC3 and isolated stock/GIL-enabled CPython3.13.16.
Source **8d44bd4ecdb0db96fee7a23113ba28b1e9cff558**, fresh postcommit
nonincremental core **e294fad194255fa086a3356a6adc57061ac3f764cc632f83bc459fa784f41ed9**,
actual assembly0.11.0. All568 production source hashes are in [source identity](core-source.json).
Optional adapter0.4, auxiliary worker/dependencies/protocol unchanged. The exact
source has full Windows/Ubuntu, actual11/10 ordinary, originalStress and installed
Release consumer [qualification](delivery-ci-status.json). No prototype is included.

Nine jobs complete245.513s; all identity/output/ownership/quiet/join/archive/SHA
checks pass. [Raw evidence](observations.tgz), [all medians/CPU/IQR/pairings](summary.json),
[receipt](archive-receipt.json), [preparation](preparation.tar.xz).

| Complete workload / phase | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| JSON-to-Markdown report / compile | 41.7–41.8 µs | 37.7–37.8 µs | 1.10–1.11× | Diagnostic; L0.13%, P0.33% / 2.79% |
| JSON-to-Markdown report / warm | 3.157–3.178 ms | 2.568–2.586 ms | 1.22–1.24× | Variable; L0.66%, P0.68% / 4.94% |
| JSON-to-Markdown report / compile-run | 3.244–3.370 ms | 2.461–2.725 ms | 1.19–1.37× | Variable; L3.86%, P9.70% / 2.20% |
| Text summary/helpers / compile | 44.4–44.9 µs | 41.3–41.6 µs | 1.07–1.09× | Diagnostic; L1.11%, P0.65% / 1.60% |
| Text summary/helpers / warm | 1.670–1.694 ms | 1.164–1.170 ms | 1.43–1.46× | Diagnostic; L1.47%, P0.50% / 1.78% |
| Text summary/helpers / compile-run | 1.807–1.825 ms | 1.291–1.300 ms | 1.39–1.41× | Diagnostic; L1.02%, P0.71% / 0.90% |
| JSON-lines/helpers / compile | 51.3–52.1 µs | 51.3–51.6 µs | 0.99–1.02× | Variable; L1.70%, P0.45% / 3.36% |
| JSON-lines/helpers / warm | 3.686–3.781 ms | 4.096–4.124 ms | 0.89–0.92× | Variable; L2.50%, P0.69% / 5.47% |
| JSON-lines/helpers / compile-run | 3.780–3.794 ms | 4.185–4.286 ms | 0.88–0.91× | Variable; L0.37%, P2.35% / 1.41% |

Report input3072records/189441B; text3072lines/35840B; JSON-lines4096records/110592B.
Ranges span two independent process medians per engine; ratios include all four
pairings, not confidence intervals. Each role has5s warmup, seven rotated150ms
rounds, ordinary GC/tiering and1s settling. Existing workers and protocol provide
full stdout/file goldens and budget cleanup. No new benchmark framework.

Warm execution includes mediated RAM I/O, full output validation and prior-result
cleanup; compilation excluded. Compile-to-result adds compilation in an already
running host. Compile measures construction and prior-script disposal. Startup,
fixture staging/reset, protocol and final cache teardown are excluded. Wider
external processCPU includes reset/protocol, GC/JIT and coarse100Hz ticks. Phase
medians need not add. Genuine pending-I/O latency, RSS and embedding tail latency
are unmeasured. These synchronous embedding clocks are not isolated CPU kernels.

Text remains roughly1.43–1.46×CPython warm. Report warm1.22–1.24× retains up to4.94%
IQR; report complete1.19–1.37× retains Lython3.86% / CPython9.70% control spread.
JSON-lines warm0.89–0.92× retains Lython2.50%spread/maxIQR5.47%; compile and complete
also remain variable. No single-change attribution or general CPU equivalence is
established by comparison with older checkpoints. All adverse observations survive;
neither a repeat nor a percentage threshold is needed to report this useful state.

All users joined, RAM unmounted/removed and archive verified under the owned lease
before retrieval. Terminal status is cached. Fresh all-alias/canonical proofs retired
supersededac6 and four exact duplicate terminal copies only after current source's
full qualification; current678 and measuredE294 survive. Fixed240MiB disk floor and
12MiB setup headroom remain; recovery receipt is in raw evidence. No new producer
release, core package publication or tag. The performance/independent regex roadmap
remains active; this checkpoint does not finish it. The [previous guide](prior-BENCHMARKS.md)
preserves superseded rows and evidence links at their recorded source revisions.
