# B1426 Factory attribution after optional metadata consolidation

Profiled the exact B1425 `9946d48aa` and `1a8aefaff` images on
the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, on
November 23, 2026 UTC. Two source roles each supplied one worker
per lane. The first role alternated by lane. Workers warmed for
eight seconds and supplied ten verified requests under all-thread
199 Hz CPU sampling.

| Script / phase | Samples, reference / candidate | Callable construction per 1,000 invocations | Memory ownership per 1,000 invocations | Calls per 1,000 invocations |
| --- | ---: | ---: | ---: | ---: |
| Postponed factories / warm | 1,187 / 1,174 | 32.7 / 31.9 | 19.6 / 19.1 | 19.9 / 22.2 |
| Postponed factories / compile to result | 1,209 / 1,196 | 32.4 / 31.8 | 16.1 / 17.8 | 22.2 / 20.6 |
| Eager factories / warm | 1,207 / 1,201 | 35.5 / 33.0 | 20.3 / 18.7 | 20.6 / 23.5 |

Counts use the nearest resolved managed frame and normalize by
verified invocations. `CreateFunctionValue` and
`StartExecutableDefaults` still dominate visible admission
callers. Result-consumer samples rise in all three lanes, including
the eager control. Its source is unchanged and the profiles cannot
separate its contribution from code layout, inlining or other work.
The warm postponed follow-up is adverse. The two compile-to-result
follow-up batches favor the candidate, opposite to B1425. Keep the
original independently replicated B1425 result and the profiling
uncertainty. There is no isolated first-result cause or new timing
qualification.

The profiles include applicable verification, previous-result disposal
and worker-request costs. Compile to result adds compilation and
compiled-owner disposal. All 60 profiled and 12 follow-up requests,
both quiet gates, worker joins and archive verification passed. The
owned RAM mount was removed. The staging lease retired four replicas
of the exact B1419 working core. Its original binary and observations
remain in the committed archives.

[Resolved profiles](summary.json) · [Image identities](identity.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Original B1425 comparison](../2026-11-23-function-optional-attributes/README.md).
