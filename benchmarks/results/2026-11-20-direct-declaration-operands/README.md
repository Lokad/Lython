# B1220 Store executable declaration operands directly

Compares production source `cbec0f2eb` with private `842c2c8f9` on
2026-11-20 UTC. The reference image comes from source-equivalent
`9741634b1`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
Each lane used two independent processes per revision, eight-second
warm-ups and five rotated rounds with roughly 0.4-second batches.
Ranges are the four pairings of process medians. Negative changes
favor the candidate.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclasses | Compile | −6.95% to −5.77% | −8.37% to −5.62% | 0.41% / 0.85% |
| Inherited dataclasses | Compile to result | −6.69% to −1.95% | −7.18% to −3.46% | 1.83% / 3.19% |
| Configuration manifest | Compile | −0.93% to +0.25% | −2.13% to +1.04% | 1.08% / 0.11% |
| Configuration manifest | Compile to result | +1.99% to +3.97% | +2.43% to +3.49% | 1.33% / 0.60% |
| Inherited dataclasses | Warm | −1.25% to +0.48% | −1.82% to −0.45% | 1.01% / 0.73% |
| Eager annotated rules | Warm | −10.73% to +1.25% | −12.46% to +0.52% | 11.81% / 1.45% |

Compilation includes compiled-owner disposal. Warm includes mediated
RAM-backed I/O, golden checks and previous-result cleanup. Compile to
result includes compilation and one execution. Process startup, fixture
reset, protocol and final teardown are outside guest clocks. Process
CPU includes the wider request.

An executable declaration owns a separate body or deferred binding.
Append it directly to the existing operand table instead of hashing
its syntax and looking up a value-equivalent copy. Functions, lambdas,
classes, generator expressions and type aliases use that route. Literal
and reusable operation interning keep their existing table. All
inputs arrive as constructed values, so the unused null and raw
byte-array interning cases and custom comparer are removed. This
subtracts 37 production lines. Checks of opcodes, operands, native
prefix eligibility and constant types agree in all three workloads,
including all 51 inspected code objects in the dataclass case.

Local compilation allocation falls 0.97% for dataclasses and 0.18%
for the manifest. Eager rules are effectively flat. Warm allocation
and accounted execution peaks match in all three and the shared
allowance returns to baseline. Dataclass compilation and compile to
result are faster on both clocks. Its warm elapsed remains mixed.
Eager warm elapsed spans zero with an 11.81% reference process spread.
Manifest compile-only elapsed is mixed. Manifest compile to result
slows on both clocks. Its original samples stay with the longer
[B1221 confirmation](confirmation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each passed 3,129 focused public
and 736 white-box Release checks. Each source image matched ten
CPython probes in both public execution modes. Both 32-file worker
images verified all three workload goldens. Both quiet checks
and all 120 batches passed. Workers joined, the persistent archive
verified under lease, and the guest fixtures were removed.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Inspected code](instruction-inspection-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](source-and-checks.tar.xz).
