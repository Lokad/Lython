# B1215 Factory costs after inline captures

Profiles `0f584586e`, whose production and worker sources match
`645a0fcd1` and frozen `356a93e25`. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Each warmed worker executes
under an unlimited shared allowance with accounting active. All-thread
CPU sampling covers approximately five seconds of complete warm
invocations, including checks, prior-result cleanup and worker framing.

| Samples | Eager rules | Eager nested factories | Postponed nested factories |
| --- | ---: | ---: | ---: |
| Process samples | 1,001 | 1,027 | 1,008 |
| Resolved runtime ancestry | 984 | 1,010 | 990 |
| Nearest native instruction loop | 262 | 157 | 86 |
| Nearest general instruction prefix | 129 | 120 | 142 |
| Nearest storage reservation | 84 | 111 | 121 |
| Reservation, positional / keyword default creation | 19 / 31 | 26 / 15 | 20 / 24 |
| Reservation, callable construction | 30 | 46 | 47 |
| Reservation, capture construction | 0 | 24 | 25 |
| Default owner in ancestry | 99 | 77 | 80 |
| Scope promotion in ancestry | 0 | 0 | 0 |

The reservation caller rows are subsets of the nearest reservation row.
Ancestry groups can overlap and the profiler cannot see all inlined
frames. The general prefix is visible, but the measured factories show
no scope promotion. Default construction and publication remain active
on all three. Use this evidence to examine the separately allocated
pending-default owner and its transport through the instruction stream.
The final public tuple and live keyword bindings still need ownership,
and the existing admission order precedes default evaluation.
[B1171](../../2026-11-20-default-container-admission/README.md) already
merged admissions while retaining the interim owner, and eager warm
CPU rose 2.60–5.60%. A further experiment needs to eliminate the wrapper
or its full transport stage. These sample counts do not predict a speedup.

The 32 source-matched image files and three CPython goldens verified
before staging. Each worker passed the goldens before and after
sampling. Both machine checks passed and all workers joined. The
persistent archive verified before guest fixture removal. The original
capture-limit failure and partial traces remain in [B1214](../README.md).

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Source and procedures](procedures.tar.xz).
