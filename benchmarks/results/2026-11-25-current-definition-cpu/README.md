# B1680 Warm CPU in ordinary and defaulted definitions

Both profiles use the frozen `a39d00228` core image from B1679. Its source
matches the committed runtime. The Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 25, 2026 UTC. Separate workers verified both
CPython goldens and warmed for at least eight seconds before six seconds
of `perf` sampling at 199 Hz. The warm boundary includes output verification,
result disposal and request framing. Compilation and file I/O are outside
these captures.

| Nearest resolved runtime region | Ordinary definitions | Defaulted-rule pairs |
| --- | ---: | ---: |
| Instruction dispatch and call routing | 467 | 191 |
| Function and default construction | 180 | 316 |
| Admission and retirement | 177 | 206 |
| Entry and operand movement | 9 | 117 |
| Arithmetic | 21 | 46 |
| Other resolved methods | 299 | 320 |
| **All samples with a resolved runtime ancestor** | **1,153** | **1,196** |

Ordinary definitions run 17,920 verified invocations. `RunCore` (237),
`NativeExecution.Call` (224), `CreateInlineDefinition` (101) and
`TryStartDirectOperandReturn` (71) account for 633 nearest samples. The
rule worker verifies 7,168 invocations. `CreateClosedPairMember` has 200
nearest samples and total definition construction appears in 571 stacks.

Of the 177 ordinary and 194 rule samples nearest `Reserve`, 86/93 resolve
to callable construction and 82/98 resolve to default storage. Preserve
both admission points. Default consumption appears in zero/three stacks.
These counts include inlined work and overlapping ancestry. They locate
construction and dispatch but do not give a causal decomposition or a speed
comparison between the two scripts.

The ordinary path still publishes evaluated singleton keyword integers
before a definition or call needs their identity. The next comparison
will test removing that publication. Keep the original default owner and
publish a canonical box when an observable binding requests it. Earlier
shared definition-reader experiments remain adverse controls for a broader
construction redesign. Both quiet gates, verified worker joins, temporary
fixture cleanup and persistent archive verification pass.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
