# B1487 Short nonlocal text across the direct factory source pair

The short nonlocal report was slower in the B1483 CPython checkpoint than
in B1478 while CPython times overlapped. B1482's controlled comparison of
its direct factory change omitted this report. This comparison uses the
exact two frozen B1482 images and includes the original short text fixture.
The reference production revision is `64a0f0217` and the candidate has the
same production sources as `9cea1fc0a`. Only the core DLL differs between
images. Earlier checkpoints include other source and measurement changes.

Two workers per source supplied seven rotated batches after eight seconds
of warmup on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. Ranges cover pairings of the worker medians. Warm
elapsed includes mediated RAM-backed I/O, golden verification and previous
result disposal. Compile to result includes compilation and code-owner
disposal. Process CPU also includes fixture reset and worker transport.
Startup and final teardown are measured separately in the benchmark suite.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Short nonlocal word report | Warm | −1.08 to +2.46% | −1.80 to +2.18% | 2.78 / 0.78% |
| Mixed-width word histogram | Warm | −0.10 to +0.35% | +0.43 to +0.87% | 0.19 / 0.26% |
| Short nonlocal word report | Compile to result | −11.68 to −0.11% | −12.01 to +2.50% | 7.67 / 5.04% |
| Quote-heavy CSV invoice | Warm | −1.81 to +0.04% | −2.44 to +0.70% | 0.92 / 0.96% |

The paired warm comparison leaves the short report's elapsed and CPU
changes unresolved. It does not isolate a repeatable warm regression
from direct factory return. The first-use elapsed direction favors the
candidate with variable workers and CPU spanning zero. Keep both the
adverse CPython checkpoint and these source-pair observations. Profile
exclusive work on current source before selecting another call or word
representation change.

Both 32-file images and three independent CPython goldens verified.
All 112 batches passed in 189 seconds. Workers joined, guest fixtures
were removed and the archive verified under both quiet gates and the
exclusive lease.

[Timings](summary.json) · [Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Original source pair and exact cores](../2026-11-23-closed-definition-return/README.md) ·
[External checkpoint](../2026-11-23-closed-definition-cpython/README.md).
