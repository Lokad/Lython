# B1644 Rule construction and manifest CPU attribution

Three short profiles use the verified `b9b20aa98` image from the
[CPython checkpoint](../2026-11-24-frontend-cpython/README.md). The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Each process verifies its complete golden, warms for at least eight
seconds of guest elapsed time, and samples at 199 Hz for about six
seconds. Samples include the whole worker request and related fixture
activity. Compilation runs only in the through-result lane. Startup
is outside these profiles.

| Workload | Invocations | All samples | Resolved runtime ancestry | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: |
| Defaulted rules, warm | 6,400 | 1,225 | 1,221 | 73 |
| Defaulted rules, time to result | 6,400 | 1,213 | 1,164 | 112 |
| Configuration manifest, warm | 23,552 | 1,166 | 748 | 753 |

For defaulted rules, 530 warm and 527 through-result runtime samples
have definition-construction ancestry. The nearest runtime method is
`CreateClosedPairMember` in 222 and 235 of those samples, followed by
`MemoryGovernor.Reserve` in 161 and 149. Reservation callers include
default backing allocation and the separate callable allocation. Operand
entry and movement are nearest in 151 and 129 samples overall. The
earlier rule attribution showed the same construction concentration.
Keep the two admission points and the tests that distinguish refusal from
default evaluation and annotation effects. Look for repeated tape or
descriptor work in ordinary definition construction and its closed entry.
[Earlier profile](../2026-11-24-forwarded-rule-cpu/README.md) ·
[Admission ordering](../2026-11-24-closed-definition-construction/README.md).

For the manifest, 317 of 748 resolved runtime stacks also contain the
benchmark file host. The nearest runtime method is `OpenedByteFile.OpenAsync`
in 281 samples. Another 415 samples contain fixture or benchmark host
frames outside resolved runtime ancestry. File work is a significant part
of the measured request. Unmapped leaves limit finer attribution, with
379 even inside resolved runtime stacks. Preserve contained resolution and
handle ownership. The profile gives no removable CPU estimate or basis to
attribute the elapsed gap entirely to interpreter work.

Both quiet gates, worker joins, fixture removal and persistent archive
verification pass. The local wrapper's output log collided with the
launcher's log after dispatch. The collected service journal and terminal
receipt confirm the original run completed and closed its workers.

[Attribution](attribution.json) · [Image](identity.json) ·
[Raw profiles](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
