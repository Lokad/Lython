# Qualified native-return CPython checkpoint

Measured2026-11-13UTC, runtime85c73469/frozen8f69261d, stock isolated CPython3.13.16.
[Source-matched full delivery](../2026-11-13-native-tuple-return/final/delivery/README.md)
covers Windows/Ubuntu actual.NET11/10, original stress and installed Release
consumers. R311's later integer prototype is excluded.

| Workload / phase | Lython ms | CPython ms | Lython / CPython | L/P process spread; max IQR |
| --- | ---: | ---: | ---: | --- |
| Invoice warm |7.923–8.004|6.132–6.247|1.27–1.31×|1.02/1.85%;1.20%|
| Invoice compilation |0.10084–0.10130|0.09295–0.09310|1.08–1.09×|0.46/0.16%;1.55%|
| Invoice compile + run |8.140–8.204|6.273–6.313|1.29–1.31×|0.78/0.63%;1.16%|
| Captured text warm |1.475–1.544|0.884–0.894|1.65–1.75×|4.47/1.08%;2.35%|
| JSON-lines warm |3.408–3.481|4.046–4.138|0.82–0.86×|2.08/2.23%;1.64%|
| File search / keyword-only helper default warm |0.280–0.290|0.1595–0.1599|1.75–1.82×|3.21/0.25%;2.52%|

Ratio above one means Lython is slower. Ranges span two process medians per
engine/four ratio pairings, not confidence intervals. Text, JSON and search have
moving controls; retain the spread and uncertainty. These are fresh external
observations, not multiplied source-pair speedups. Differences from older
collections cannot attribute a regression or improvement to one runtime change.
Invoice4096records/127031B, captured text3072lines/35840B, JSON4096records/110592B,
file search2049lines/42010B with early exit after1025.

Warm reuses compiled code and includes efficient mediated RAM-file I/O, full
stdout/file digest validation and prior-result cleanup. Compilation includes code
construction/disposal under the shared allowance. Fresh adds that compilation and
compiled-owner disposal to execution. Startup, fixture staging/reset, protocol and
final cache teardown are outside. Phase medians need not add up. Wider process CPU
in the raw summary includes reset/protocol and does not isolate an internal cost.
No startup, pending-I/O latency, host latency or heap/RSS claim.

B671 six jobs233.888seconds on the quiet AzureF4as_v7/Ubuntu24.04.4, four AMD
EPYC9V45vCPUs/16GiB/no swap, actual.NET11RC3 normal GC/tiering. Two processes per
engine/job; five rotated500ms rounds,5second warmups(12captured). Reuse fixed
fec79worker, original catalog/protocol, adapter0.4 and existing8f image: no new
benchmark framework or runtime upload. Every worker checks exact core/output
identity; full goldens/shared balances/quiet/join/archive gates pass. Archive SHA
is independently verified; RAM fixtures are removed, no users remain. Earlier
reference identity fields retained in the raw payload are unused: all measured
Lython roles are8f. This does not qualify a newer regex producer.

The earlier[633/2e checkpoint](../2026-11-13-invoice-cpython-checkpoint/README.md)
retains its own observations. Current complete-script gaps justify continued
representation/frontend and independent regex work; publication remains held.

[All six rows and CPU/control detail](summary.json),[raw evidence](observations.tgz),
[archive receipt](archive-receipt.json),[joined terminal](terminal-status.json).
