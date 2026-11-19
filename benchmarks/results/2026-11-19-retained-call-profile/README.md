# B1070/B1071 retained call and construction attribution

The frozen `12fabffb1` runtime ran on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC. Each whole-script
warm profile verifies stdout and file goldens before and after coverage.
Both complete image checks and the same CPython golden verification
preceded staging. The combined evidence covers three captured lanes.

| Runtime attribution | Ordinary rules | Retained siblings | Expanded forwarding |
| --- | ---: | ---: | ---: |
| Stacks with identifiable runtime ancestry | 988 | 961 | 1,007 |
| Definition execution ancestry | 279 | 313 | 0 |
| Borrowed invocation ancestry | 257 | 4 | 183 |
| Admission with closure capture as nearest caller | 0 | 46 | 0 |

Call-entry ancestry includes guest work. Nearest-frame attribution narrows
the work but may miss inlined code. The siblings show 46 admission stacks
nearest capture construction and 23 capture-construction nearest frames.
Their three retained siblings each build a separate array for the one shared
cell. This supports a measured prototype that removes singleton arrays and
preserves namespace-backed and multi-cell cases. The ordinary-rule profile
has 87 nearest frames in native call entry or inline start. Forwarding also
shows repeat split construction and expanded-keyword copying. This diagnostic
has no paired elapsed result or claimed removable CPU fraction.

B1070 completed rules. Siblings reached 7.751 of the required eight warmup
seconds in 128 batches of 256 before sampling. Forwarding had yet to start.
B1071 keeps the warmup and sample targets and uses batches of 512 on those
two cases. It reuses the same image and case definitions. Both quiet gates,
worker joins and persistent archive verification succeeded in each run.
The first run's incomplete warmup and its sampled rules lane are retained.

[Attribution](attribution.json) · [B1070 raw observations](b1070/observations.tgz) ·
[B1071 raw observations](b1071/observations.tgz) · [Procedures](procedures.tar.xz).
