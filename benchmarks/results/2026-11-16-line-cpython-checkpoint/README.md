# Current complete normalization against CPython

R432/B829,2026-11-16UTC. **86d9344e /measured ecd9e956** versus stock
**CPython3.13.16**.600 frozen production/build/test inputs match committed source;
487internal/1,656public plus4related original-scale stress pass eachactual.NET11/10.
Full43805966777 is active at capture; this comparison does not qualify all runtime
behavior. Publication held. Existing complete52,224B/4,096-line Unicode normalization,
54,269B/4,095-line output, unchanged independent stdout/file goldens.

| Phase | Lython | CPython | Lython / CPython | L/P process spread | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| warm Run | 1.146–1.166 ms | 0.499–0.502 ms | 2.28–2.34× | 1.68/0.50% | 0.67% |
| compile and execute once | 1.213–1.235 ms | 0.587–0.593 ms | 2.05–2.10× | 1.85/0.95% | 0.98% |

All four process-median pairings, not confidence intervals. Two jobs**70.938s**,
two processes/engine,5s full-request warmups and five rotated approximately0.5s
batches. All image/catalog/golden/budget/quiet/Closed/exit/join/archive gates pass.

**Important unresolved comparison:** CPython warm is about0.50ms here versus
0.73ms in [B823](../2026-11-16-normalization-cpython-checkpoint/README.md); fresh
about0.59ms versus0.83ms. Wider CPU also changes. Worker SHA, CPython executable
SHA/build/flags, timing-boundary string and every case field are identical. Current
catalog loads4cases versus1previously; this process-initialization difference is
not isolated as the cause. Within-run controls/IQRs are stable, but exact ratios
are collection-specific. Preserve both results; do not infer a Lython regression,
causal gain or recovered debt from cross-collection ratios. The current material
gap is real within this measurement boundary; its magnitude remains uncertain.
Any follow-up must control catalog/process initialization or another concrete
changed condition; no automatic ban on bounded confirmation or repeated campaign.

Separate retained [bulk-text](../2026-11-16-bulk-text-construction/refined/README.md)
and [line-construction](../2026-11-16-line-construction/confirmation/README.md)
source pairs establish their own allocation/CPU/removal evidence. They cannot be
added or inferred from this external table. Broader performance work remains open.

Quiet Azure F4as_v7/Ubuntu24.04.4,four EPYC9V45 vCPUs,16GiB/no swap,.NET11RC3
ordinary tiering/natural workstation GC and CPython GIL/GC. Warm includes mediated
RAM I/O, full stdout/file goldens and prior-result cleanup; excludes compilation/
startup, reset/protocol and final cache teardown. Fresh adds compilation/compiled-
owner disposal. Wider CPU includes reset/protocol; separate medians are not additive.
Native CPython buffered files do not provide equivalent containment. No startup,
pending-I/O, heap/RSS, contention or embedding-tail comparison.

Original staging failed the unchanged disk floor before mutation/workers. Retired
only two exact archived old-catalog aliases and redundant B828working evidence
after fresh idle-lease path/ownership/hash/all-alias checks. An own-parent pgrep
guard correction is preserved. Current images/toolchains and254/252MiB floors
remain. Full traces archived/verified persistently under lease before RAM unmount;
no workers/fixtures remain. Terminal cached. No new framework or runtime edit.

[All controls](summary.json),[identity review](identity-review.json),
[raw observations](observations.tgz),[procedure/inputs/source proof/original conditions](procedure-and-inputs.tar.xz),
[previous guide](prior-BENCHMARKS.md).


[Combined Windows/Ubuntu actual11/10, original stress plus line case and installed-package qualification passes](../2026-11-16-line-construction/delivery/README.md) at86d9344e/ecd9e956. Earlier capture-time pending state and all measurement limits remain evidence; later prototypes are separate.
