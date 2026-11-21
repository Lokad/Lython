# B1309 skip repeat visits to leaf suites

The private `b8359d01a` prototype records nested-definition presence
while collecting lexical bindings. Leaf suites can omit the second
walk when they need neither expression preparation nor postponed
annotation text. Nested declarations, scope directives and class-cell
semantics still use the prepared lexical facts. The binding scan and
compaction check cancellation directly.

## Quiet comparison

Compared `4337b7765` with `b8359d01a` on November 21, 2026 UTC
on the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
Two independently warmed processes per revision contribute seven
rotated batches each. Ranges cover all process-median pairings.
Compilation includes the code owner's disposal. Time to result adds
execution, mediated RAM-backed I/O and prior-result disposal. CPU
covers the wider worker request. Negative changes favor the prototype.

| Workload | Boundary | Elapsed change | Process CPU change | Ref / prototype elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compile | −1.53 to +0.27% | −3.42 to 0.00% | 0.46 / 1.36% |
| Postponed factories | Compile | −1.30 to +0.12% | −1.42 to +0.62% | 0.23 / 1.20% |
| Inherited dataclasses | Compile | +0.27 to +3.27% | −1.24 to +2.32% | 1.55 / 1.42% |
| Generated validator | Time to result | −0.55 to +0.60% | −1.02 to +1.29% | 0.97 / 0.19% |

Keep this variant private. Validator compilation uses up to 3.42% less
process CPU, but time to result spans zero with stable controls.
Dataclass elapsed rises across all pairings with mixed CPU. Local
compilation allocates about 25–31 KB less for the validator, 2,000
bytes less for dataclasses and 240 bytes less for postponed factories.
The dead-heavy control allocates 32,992 bytes less. Local elapsed is
diagnostic. The next design should eliminate the repeated walk in
non-leaf suites too, without keeping a second inventory of statements.

Windows .NET 11 and Ubuntu .NET 10 each pass 111 white-box and 962
public focused Release checks. The exact frozen cores produce identical
metadata for 65 code objects across seven scripts, including protected
definitions, postponed module annotations, a protected assertion tail
and a dead-heavy control. The compile owners refund. Both 32-file
worker images pass CPython goldens for all three timed scripts.
All 112 batches pass. Both machine gates, worker join, fixture cleanup
and archive verification finish under the lease in 219 seconds.

[Timing](summary.json) · [Allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-21-cpython-retained-checkpoint/measured-core.dll.gz).
