# B1736 CPython comparison after deferred callable construction

The retained `7f0c5b996` source runs against stock CPython 3.13.16 on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
The fully verified 32-file image is the candidate from the [paired comparison](../2026-11-25-deferred-callable-locals/confirmation/README.md).
The ordinary factory creates 96 definitions 16 times. The shipment script
reads 2,048 records from a 27,350 B CSV and prints a verified report.

| Workload | Phase | Lython elapsed | CPython elapsed | Lython / CPython | Same-engine worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | Compilation only | 0.57294–0.57500 ms | 1.10284–1.10975 ms | 0.516–0.521× | 0.36% / 0.63% |
| Ordinary definitions | Warm sync | 0.22097–0.22361 ms | 0.14817–0.15315 ms | 1.443–1.509× | 1.20% / 3.36% |
| Ordinary definitions | Compile-to-result | 0.78635–0.80450 ms | 1.11361–1.12001 ms | 0.702–0.722× | 2.31% / 0.57% |
| Shipment callbacks | Warm sync | 2.32550–2.40626 ms | 1.62525–1.63851 ms | 1.419–1.481× | 3.47% / 0.82% |
| Shipment callbacks | Warm Lython `RunAsync` | 2.74330–2.87713 ms | 1.73829–1.74817 ms | 1.569–1.655× | 4.88% / 0.57% |

Ratios span the four pairings of engine process medians. Values above 1× mean
Lython takes longer. Each phase uses two processes per engine, five seconds of
warmup per process and seven rotated batches per process. Warm elapsed includes
guest file I/O where applicable, output verification and result disposal.
Compilation includes compiled-owner disposal. Time through result combines
compilation, execution and both cleanups. Fixture reset, framing and process
startup sit outside elapsed. Wider process CPU includes framing and reset.
The async row uses the same synchronous guest script on both engines.

The factory remains slower warm and reaches its result sooner when compiled
once. Shipment remains slower in both hosting modes. Absolute shipment elapsed
varies across collections for both engines. Use the paired source comparison
for the effect of deferring definitions. All 140 timed batches, two local and
VM script goldens, elapsed boundaries and terminal worker cleanup are recorded.

[Summary](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Earlier CPython checkpoint](../2026-11-25-arithmetic-call-cpython/README.md).
