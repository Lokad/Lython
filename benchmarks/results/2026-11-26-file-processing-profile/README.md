# B1745 Attribute file search and Unicode text CPU

Three CPU-clock profiles use the exact `a490e9d5f` image from the
[CPython checkpoint](../2026-11-26-file-processing-cpython/README.md).
The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. Each worker warms for at least eight seconds and samples at
199 Hz for a short batch block. The process boundary includes mediated
RAM-backed I/O, output and file checks, cleanup, framing and fixture reset.
The through-result lane also compiles and disposes code on each invocation.

| Workload and phase | Invocations | Samples | Resolved runtime ancestry | Frame and operand binding, inclusive | File or host I/O, inclusive | Unresolved leaves overall |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Protected file search, warm | 19,456 | 1,182 | 880 | 271 | 227 | 448 |
| Protected file search, compile-to-result | 17,408 | 1,209 | 775 | 256 | 226 | 476 |
| Unicode text report, warm | 7,168 | 1,276 | 1,168 | 452 | 134 | 214 |

These groups overlap. A stack with a runtime method can include useful work
and an unresolved leaf. The counts locate work and give no estimate of the
time a redesign would save. The compile-to-result profile has 119 nearest
frontend samples. The Unicode report has 645 stacks with its existing native
word preparation or dictionary loop. That path is a separate cost from the
file search predicate.

The file reader already gives long lines admitted views into stable UTF-8
windows. `CreateWindowValue` is the nearest visible caller on 59 of 63 warm
search reserve samples. The earlier counted header grant had inconclusive
CPU, and its byte-bound refinement admitted substantially more memory.
Keep those observations with the decision. [Counted grant](../2026-11-13-text-window-capacity/counting/README.md) ·
[Byte-bound grant](../2026-11-13-text-window-capacity/rough/README.md).

Inspecting the exact compiled search helper shows `LoadCallMember`,
`LoadConst`, `Call` and `Return`, with one argument local and five total
value slots. It takes the native helper workspace path. The next hypothesis
is that an exact closed string predicate can return from the supplied
operand, using the existing affix operation and call admission. This would
displace the workspace and the inner dispatch stages. Dynamic receiver
resolution, actual suspension and diagnostics still require their ordinary
path. Compare the complete search, a dynamic receiver and the existing text
and CSV controls before deciding whether to retain the change.

Both independent output and file goldens passed before staging. Workers
verified results before and after capture, then joined. Both quiet checks
passed and the evidence archive verified before the VM lease ended.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and tape inspection](procedures.tar.xz).
