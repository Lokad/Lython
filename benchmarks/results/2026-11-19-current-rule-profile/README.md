# B1125 rule execution CPU attribution

This profile uses the exact retained Lython source `40ab5132a` on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. The complete 32-file image and all three CPython workload
goldens passed local verification. Each worker verified the program and
files before and after sampling.

| Samples or nearest runtime method | Ordinary rules | Annotated rules | Captured rules |
| --- | ---: | ---: | ---: |
| Samples with runtime ancestry | 990 | 1,150 | 1,046 |
| Shared instruction loop | 131 | 209 | 133 |
| Stack-transfer switch | 1 | 88 | 0 |
| Storage admission | 96 | 70 | 108 |
| Builtin arithmetic | 107 | 71 | 104 |
| Type metadata attachment | 0 | 51 | 0 |
| Executable work-state construction | 10 | 25 | 24 |

Stack transfer is visible on annotated rules. The shared loop selects
ordinary load, store and stack operations and calls a second opcode
switch to perform them. This source evidence supports testing direct
selection in the shared loop. Preserve member protocol dispatch,
retirement, result forwarding and cooperative checks. The sample
counts include useful work and can omit inlined methods. They leave
elapsed and CPU improvement to the paired comparison.

Both quiet gates passed. Workers joined and the VM verified the
persistent archive before removing guest fixtures. An early local
observation attempt stopped at the observation guard before contacting
the VM. The same completed collection was subsequently retrieved.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
