# B1578 Shared compiled callable definition preparation

Private `ebfe49f02` shares the input count, lexical eligibility and
borrowed construction branch for `def` and lambda. Both drivers consume
the same descriptor. The compiled capture map allows the runtime to
skip its local-only prefix during eligibility checks. The captured
script's three child definitions together skip five such checks per
group, or 10,240 per invocation. Functions requiring a definition
scope still use the general invocation.

| Complete script | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −3.58 to −1.91% | −3.02 to −0.77% | 1.29 / 0.44% |
| Defaulted rules | Warm | +1.04 to +3.81% | +0.07 to +6.04% | 0.60 / 2.13% |
| Annotated rules | Warm | −6.25 to +9.84% | −5.85 to +10.38% | 12.29 / 4.34% |
| Short nonlocal report | Warm | −0.38 to +3.98% | −0.79 to +4.72% | 0.27 / 4.10% |
| Quoted invoice | Warm | −1.50 to +0.93% | −0.58 to +2.00% | 0.91 / 1.54% |
| Captured rules | Compile to result | −4.85 to −2.90% | −6.38 to −2.42% | 1.93 / 0.12% |
| Defaulted rules | Compile to result | +1.76 to +14.09% | +2.14 to +14.73% | 0.37 / 11.71% |

Keep the patch private. Captured gains have supporting CPU. Defaulted
results move adversely in both phases and their first-use magnitude
is uncertain. The annotated control is unstable. A bounded confirmation
of those lanes with longer warmup can distinguish repeatable debt from
worker movement. All five admitted peaks match and allowances return
to baseline. Rule CLR allocation matches and the two control scripts
differ by at most 230 B per invocation.

The Windows x64 FullOpts JIT listing moves some code into the shared
guard. The selected dispatcher and definition methods together decrease
from 7,116 to 6,202 bytes. Native definition dispatch itself shrinks
from 666 to 254 bytes, while the separate native constructor grows
from 975 to 1,068 bytes. This diagnostic describes generated code.

Windows and Ubuntu each pass 457 focused .NET 11 Release checks.
Both 32-file images match five CPython 3.13.16 goldens. Frozen
`bd0f0ca93` matches the reference runtime in `f5bbd0cc9`. Only the
core differs in the candidate. The quiet Azure F4as_v7 runs Ubuntu
24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers per
revision warm for eight seconds and alternate seven samples each.
Elapsed includes applicable mediated RAM-file I/O, golden checks and
previous-result disposal. Compile to result adds compilation and
compiled-owner disposal. Wider CPU includes request and reset overhead.

All 196 batches pass in 356 seconds, with quiet gates, worker joins,
fixture cleanup and persistent archive verification. The first local
staging check rejected a mismatched proof for an older reference core.
The corrected stage verified a persistent copy and retired three exact
VM duplicates under the lease.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[JIT sizes](jit-summary.json) · [JIT output](jit-observations.tar.xz) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and exact patch](procedures.tar.xz) · [Terminal status](terminal-status.json).
