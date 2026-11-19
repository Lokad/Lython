# B1137 rule execution CPU attribution

The quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 profiled the
retained `e20604ef4` image on November 19, 2026 UTC. The complete 32-file
image passed three CPython goldens. Each worker verified its workload
before and after tracing. Samples cover warm whole-script execution.

| Samples or nearest runtime method | Ordinary rules | Annotated rules | Captured rules |
| --- | ---: | ---: | ---: |
| Samples with runtime ancestry | 1,150 | 1,034 | 1,000 |
| Shared instruction loop | 167 | 224 | 155 |
| Storage admission | 119 | 66 | 94 |
| Result consumer | 67 | 67 | 54 |
| Native call entry | 65 | 48 | 12 |
| Native instruction loop | 52 | 53 | 87 |
| Executable work-state construction | 22 | 23 | 32 |

Result forwarding is visible across all three workloads. The fast route
already avoids a temporary result write and dispatch of an adjacent store.
It still reads the following instruction to find the destination, then the
shared runner checks name and cell policy. A prepared local destination can
remove both steps when the compiler has proved the slot is uncaptured and
the live frame has no namespace or mirroring. Keep the ordinary consumer
for pending cleanup, teardown, observable locals and dynamic namespace
effects. The sample counts include useful work and inlining. A bounded
source pair is needed to measure the effect of removing these steps.

Both quiet gates passed. All workers joined and the VM verified the
persistent traces before unmounting guest fixtures. The staging step
retired only the redundant reference DLL whose exact compressed core and
observations were already committed for B1135.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
