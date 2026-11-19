# B1059 retained rule and Unicode regex attribution

Profiled `f80035c13` with core `8ce376c4` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3, on 2026-11-19 UTC. The four rule cases are
unchanged from B1057. Three new Unicode extraction cases use named group
text, scalar spans and mixed access with lookbehind. The complete 32-file
image and seven cases verified against CPython goldens. Each case warmed
for at least eight guest seconds. The recorded guest windows were
5.01–5.68 seconds per case.

| Runtime ancestry or nearest frame | Ordinary rules | Annotated rules | Captured rules | Mutable counters |
| --- | ---: | ---: | ---: | ---: |
| Resolved runtime stacks | 1,004 | 983 | 1,122 | 1,070 |
| Definition execution ancestry | 287 | 267 | 221 | 161 |
| Borrowed invocation ancestry | 254 | 264 | 148 | 244 |
| Nearest frame is capture construction | 1 | 5 | 22 | 17 |
| Admission with capture as nearest caller | 0 | 0 | 37 | 24 |
| Nearest frame is closure retention | 5 | 3 | 3 | 14 |
| Admission with closure retention as nearest caller | 0 | 0 | 0 | 15 |

Mutable counters still retain a lexical context when their captures are
already present in executable cells. Extending independent cells to exact
nonlocal captures can remove that context charge while preserving shared
rebinding. Ordinary rules still show two positional-default admission
boundaries. Any redesign there must displace an ownership or transport
stage and account for the unfavorable B1034/B1035 and B1043 results.

| All-thread stack ancestry | Group text | Scalar spans | Mixed access |
| --- | ---: | ---: | ---: |
| Samples | 1,024 | 1,084 | 1,004 |
| Pinned provider or adapter | 752 | 883 | 811 |
| Core match projection | 42 | 36 | 23 |

The current Unicode cases put most sampled CPU in the pinned 0.4.0
provider or adapter. Core match projection accounts for about 2–4%.
Retain the current scalar translation for those workloads. A new provider
or wider capture workload may change that balance. The evidence compares
ancestry in one worker image; it supplies neither elapsed speedups nor
removed CPU estimates.

Both quiet gates and output checks passed. Workers joined and the RAM
traces were archived and verified under the lease before fixture removal.
Incomplete and ambiguous symbols remain in the raw evidence.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verified procedures](procedures.tar.xz).
