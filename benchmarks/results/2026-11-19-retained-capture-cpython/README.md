# B1057 retained runtime against CPython

Measured Lython `f80035c13` with core `8ce376c4` against stock CPython
3.13.16 on 2026-11-19 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. The complete 32-file candidate image from B1055 and
all seven catalog cases verified before staging. Five cases repeat B1047.
The two added cases exercise read-only and mutable closure cells.

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Shipment report with format selectors | 3,072 records / 189,441 B | 0.95–0.96× | 1.18–1.33× | 1.17–1.31× |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.83× | 1.75–1.79× | 1.64–1.66× |
| Ordinary rule closures | 2,048 inputs / 4,096 definitions | 0.79–0.81× | 4.14–4.24× | 3.98–4.09× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.81–0.82× | 4.77–4.90× | 4.55–4.61× |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 0.82–0.83× | 3.99–4.20× | 3.86–4.04× |
| Mutable rule counters | 2,048 counters / 4,096 calls | 0.81–0.82× | 3.76–3.88× | 3.56–3.76× |
| CSV invoice | 4,096 data records / 127,031 B | 0.97–0.98× | 1.10–1.12× | 1.09–1.13× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Above one means Lython took longer. Lython's warm controls
differ by at most 2.41%. The shipment report's CPython processes differ by
10.73% warm and 8.85% compile-to-result, leaving the size of that gap
uncertain. Mutable counters' compile-to-result Lython controls differ by
4.67%. Compilation alone favors Lython on all seven cases. Use B1055 for
attribution of the prepared-capture change.

All seven jobs and 420 timed batches passed the protocol and golden checks.
The 433.37-second collection cleared both quiet gates. Workers joined,
guest fixtures were removed and the persistent archive verified under the
lease. No retired files were needed for this run.

Warm elapsed includes mediated RAM-backed I/O, golden checks and previous
result cleanup. Compilation alone includes compiled-owner disposal.
Compile-to-result includes compilation, execution and cleanup. Process
startup, fixture reset, request framing and final cache teardown fall
outside guest elapsed. The separately reported process CPU has a wider
boundary. Installed-consumer and full-platform qualification for these
source changes is tracked independently.

[Elapsed and CPU observations](summary.json) · [Raw observations](observations.tgz) ·
[Procedures and worker identity](procedures.tar.xz) ·
[Earlier B1047](../2026-11-19-retained-selectors-cpython/README.md) ·
[Paired capture comparison](../2026-11-19-prepared-capture-slots/README.md) ·
[Delivery CI](https://github.com/Lokad/Lython/actions/runs/44471822057).
