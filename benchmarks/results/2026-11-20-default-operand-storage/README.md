# B1216 Final default storage in executable operands

Compares `4ab503f59` against private `9741634b1` on 2026-11-20 UTC
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The
reference core comes from source-equivalent `356a93e25`. Each lane uses
two independent processes per revision, eight-second warm-ups and
five rotated 0.4-second batches. Ranges cover all four process-median
pairings. Negative changes favor the candidate.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager annotated rules | Warm | −2.99% to −0.91% | −3.52% to −0.26% | 1.65% / 0.48% |
| Eager nested factories | Warm | −3.31% to +9.10% | −3.87% to +9.24% | 0.11% / 12.71% |
| Postponed nested factories | Warm | −7.21% to −3.93% | −7.72% to −3.55% | 3.27% / 0.25% |
| CSV invoice | Warm | −1.93% to +0.43% | −4.88% to +2.50% | 0.29% / 2.11% |
| Eager annotated rules | Compile to result | −5.18% to −1.88% | −5.04% to −2.82% | 0.25% / 3.22% |
| Eager annotated rules | Compile | −0.31% to +2.81% | +0.02% to +2.09% | 2.50% / 0.62% |

Warm includes mediated RAM-backed I/O, output and file checks and
prior-result cleanup. Compile-to-result adds compilation and compiled
owner disposal. Compile-only includes owner disposal. Fixture reset,
request framing, process startup and final teardown are outside the guest
clocks. Wider process CPU includes reset and request handling.

The private positional tuple and prepared keyword values occupy their
final storage from the start of default evaluation. The executable
operands own them until an actual function can be published. During
unwind they relinquish only those containers, retaining any independently
owned values. Decorators can keep the function before they raise.
The compiler provides the destination default positions. Lowered lambdas
and generated definitions carry the existing two-reference value instead
of allocating a heap owner. Mixed defaults occupy one additional operand.
All measured code keeps its original instruction counts and native
prefix eligibility.

The three factory scripts each eliminate 114,688 allocated bytes per
warm run, a 10.96% reduction for eager rules and 7.62% for either nested
factory. Invoice control and local compile allocation match. All four
accounted peaks match and every allowance returns to its baseline.

Eager rules and postponed nested factories use less elapsed time and CPU
in every warm pairing. The eager rules also reach their result sooner
including compilation. Compile-only elapsed spans zero and CPU is
slightly higher. The eager nested result is inconclusive. One candidate
process runs about 3.3% faster and the other about 9% slower than both
references. Its 12.71% process spread motivates a bounded longer-batch
repeat on the identical images. The first observations remain in this
record. [B1217 repeat](confirmation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each passed 2,712 public and 648
white-box focused Release checks. These include mixed-default admission
refusal before expression effects, asynchronous transfer, exception
cleanup and decorator escapes. Another 55 Windows Debug checks passed
with operand invariants enabled. Each 32-file image verified all four
workload goldens and ten independent CPython probes in both execution
modes. Both quiet gates and all 120 timed batches passed. All workers
joined, the persistent archive verified and guest fixtures were removed.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Inspected code](instruction-inspection-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](source-and-checks.tar.xz).
