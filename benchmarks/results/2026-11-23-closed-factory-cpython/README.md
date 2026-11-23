# B1530 closed factory operands against CPython

Retained `b04e371c0` and stock CPython 3.13.16 run the same reports
with 2,048 nested definitions and calls. The quiet Azure F4as_v7
ran Ubuntu 24.04.4 with .NET 11 RC3 on November 23, 2026 UTC.

| Workload and phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Postponed factories, warm | 0.57014–0.61600 ms | 0.54444–0.54632 ms | 1.044–1.131× |
| Postponed factories, compile to result | 0.61115–0.63207 ms | 0.57428–0.57816 ms | 1.057–1.101× |
| Postponed factories, compilation only | 0.02242–0.02257 ms | 0.04173–0.04179 ms | 0.536–0.541× |
| Eager factories, warm | 0.66236–0.68124 ms | 0.57585–0.58043 ms | 1.141–1.183× |

Ratios span all four pairings of per-process medians. In the postponed
warm phase Lython's workers spread 8.04%, leaving the magnitude uncertain.
The eager warm and postponed first-use spreads are under 3.5% per engine.
The scripts issue no file operations. Warm elapsed includes evaluation,
output verification and prior-result disposal. Time to result adds
compilation and disposal of the compiled owner. The compilation-only clock
includes disposal of the previous compiled object in the batch. Replacement
of the worker's retained compiled object, worker launch and request transport
sit outside the phase timers.

Two workers per engine warm for eight seconds and contribute seven timed
batches per phase. Both goldens, all 112 batches, both idle gates, full
image hashes, worker joins, fixture cleanup and the persistent archive
verify. [B1529](../2026-11-23-closed-factory-operand/README.md) attributes
the change to Lython source under a matched comparison. [B1516](../2026-11-23-closed-defaults-cpython/README.md)
retains the earlier external checkpoint.

[Identity](identity.json) · [Summary](summary.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
