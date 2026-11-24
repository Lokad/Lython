# B1601 CPU attribution for private closure calls

Five serial Linux profiles use the same two 32-file images as B1599.
The candidate at `642c8f957` matches delivered `c40540135`. The reference
matches `722b49f2d`. The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11
RC3 on November 24, 2026 UTC. Each workload warms for at least eight seconds
and contributes about six seconds of whole-batch CPU samples at 199 Hz.
The profiles locate work. The execution timing comparison remains B1599.

| Workload and source | Completed invocations | Samples with resolved runtime ancestry | Native dispatch | Entry and operand movement | Construction | Admission or retirement |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Captured rules, current | 4,480 | 1,191 | 304 | 161 | 121 | 112 |
| Postponed rules, reference | 9,984 | 1,173 | 193 | 65 | 105 | 182 |
| Postponed rules, current | 9,216 | 1,176 | 173 | 50 | 123 | 207 |
| Quoted invoice, reference | 1,024 | 1,294 | 415 | 60 | 0 | 87 |
| Quoted invoice, current | 1,024 | 1,285 | 346 | 59 | 0 | 79 |

Nearest runtime groups are heuristic and the table shows selected
categories. Captured stacks resolve 512 samples under the outer native
workspace outside the private inner call, 140 under the private inner
call and 107 under the closed integer consumer. The other 432 are shared
runtime or lack a distinguishing caller boundary. Within the 512 outer
samples, 146 resolve nearest the native loop, 92 the definition dispatcher,
63 reservation and 45 capture construction. The outer factory offers
more attributable work than a capture-storage-only proposal. First prove
that its existing exact integer bindings, two definitions and terminal
return can leave the workspace together.

Postponed rules show more samples near reservation and construction in
the current image. Captured routes are unused in that workload and its
admitted peaks match. This single profile per image leaves the cause
unresolved. Invoice work has equal completed counts and shifts between
dispatch, host I/O and other runtime methods. It also leaves the earlier
regression unexplained. The Windows FullOpts listings show the same sizes
for `RunCore`, `CallFunction`, `StartInline` and `StartNativeWorkspace`.
`ApplyDefinition` grows from 666 to 700 bytes with the same frame.

Both images verify all three goldens and every frozen file. All five
profiles finish with both quiet gates, joined workers, removed fixtures
and verified persistent archives. The first launcher failed locally
before submitting a service. Its corrected launcher and original source
are archived. Normal profiling does not qualify an elapsed speedup.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Generated code](jit-summary.json) · [Raw profiles](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Storage custody](storage-custody.json) ·
[Terminal status](terminal-status.json) ·
[B1599 controlled timing](../2026-11-24-private-definition-call/isolation/README.md).
