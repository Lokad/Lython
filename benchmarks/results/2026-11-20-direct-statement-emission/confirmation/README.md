# B1192 Direct statement emission confirmation

Retained as `a44435d61`. The confirmation used the unchanged `b418fa7d8`
and `2131039a4` worker images from B1191. It ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Each process
warmed for at least 18 seconds. Seven rotated rounds targeted 1.25-second
requests. Ranges cover the four pairings of duplicate process medians.

| Workload | Phase | Quiet elapsed change | Quiet process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −13.73% to −9.75% | −13.28% to −10.05% | 1.42% / 3.15% |
| Configuration manifest | Compile to result | −1.57% to −0.09% | −1.45% to −0.05% | 1.20% / 0.29% |
| Eager annotated rules | Warm | −2.51% to +3.95% | −1.89% to +4.10% | 3.42% / 3.11% |

The longer validator comparison shows a compilation gain with lower CPU.
The first run spanned zero, and validator compile-to-result CPU remains mixed
in that run. The confirmed compile-only gain applies to the longer requests.
The earlier manifest CPU penalty did not recur. Eager warm still spans zero
with adverse pairings. An inspection of all five workloads found matching
opcode names, operands, spans, exception regions, binding layouts and constant
types in the roots and inspected child functions. It cannot attribute the
eager timings to a specific runtime cost. The direct compiler eliminates
an entire statement construction pass and reduces measured compile allocation
by 0.11–2.45%. Warm allocation and accounted peaks match.

Compilation includes code-owner disposal. Compile to result adds execution,
mediated I/O, output checks and prior-result cleanup. Warm includes execution
and the same result boundary. Process CPU includes the wider request loop.

Both machine gates and all 84 timed batches passed. Workers joined and
the observations were verified under the lease before fixture teardown.
The initial source qualification remains applicable. A supplemental 164
checks on each platform exercise parser bounds, declaration isolation and
native returns on the same source.

[Summary](summary.json) · [Instruction inspection](instruction-inspection-summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz) ·
[Original conditions and adverse results](../README.md).
