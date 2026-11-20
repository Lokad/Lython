# B1167 generated dataclass defaults

Generated dataclass initialization now prepares one admitted sequence of
parameter names, kinds and values. The shared layout feeds positional and
keyword defaults into the existing private default containers. This removes
the transient name-keyed defaults map, the per-parameter objects, and the
copy through an enumerable. Generated equality and ordering methods share
one immutable two-argument layout. Default factories still run at invocation.

The reference is `5908b8c94`. The measured source is `2b8e388a1`, retained
as `4e2bd4695` alongside an independent compiler cancellation fix.
Windows .NET 11 and Ubuntu .NET 10 each passed 249 focused Release checks,
including factory callbacks, default alias identity, and rollback after
partial admission. Both complete frozen images matched four CPython
goldens. All local workloads restored the shared allowance.

| Workload | Boundary | Windows warm allocation change | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclasses, 48 subclasses | Warm | −12.25% | −11.67% to −7.75% | −11.28% to −7.35% |
| Inherited dataclasses | Compile to result | — | −7.39% to −5.26% | −8.07% to −4.77% |
| Expanded component registry control | Warm | Flat | −0.10% to +2.96% | +0.53% to +1.49% |
| Eager annotated rules control | Warm | Flat | −0.73% to +2.52% | −0.29% to +2.13% |
| Positional CSV invoice control | Warm | Flat | −3.39% to +2.05% | −3.27% to +2.04% |

Ranges come from the four duplicate-process median pairings on the quiet
Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, 2026-11-20 UTC. Warm
elapsed includes mediated guest I/O where applicable, golden checks and
prior-result disposal. Compile-to-result adds compilation and compiled-owner
disposal. Process CPU covers the wider batch. The dataclass gains held
across pairings. The registry control used slightly more CPU with mixed
elapsed. The dataclass accounted peak rose by 384 bytes with admitted input
storage and construction overlap. Control peaks matched.

All 100 timed batches passed. Both quiet gates passed, workers joined,
fixtures were removed and the observation archive was verified under the
lease. The archived receipt printed a literal six inherited from B1166.
The corrected retrieval checked the archive hash and verified the actual
five selected lanes; the original receipt and failed retrieval are preserved.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
