# B1572 Captured and conditional scalar helper returns

The existing scalar evaluator reads forwarded live closure cells and
returns from a selected exact branch. Conditional returns go straight
from each arm to the caller. The captured and defaulted `apply` tapes
each shrink to six instructions, from twelve and eight respectively.
The captured lambda goes from five to three. The captured script
avoids 4,096 activations per invocation and the defaulted script
avoids 2,048.

| Complete script | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rules | Warm | −13.88 to −12.62% | −13.85 to −12.35% |
| Defaulted rules | Warm | −18.85 to −8.66% | −19.05 to −8.16% |
| Annotated rules | Warm | −7.24 to +7.81% | −5.89 to +6.27% |
| Short nonlocal report | Warm | +4.67 to +5.79% | +5.11 to +5.91% |
| Quoted invoice | Warm | +0.17 to +2.05% | +1.30 to +3.31% |
| Captured rules | Compile to result | −12.78 to −8.94% | −12.69 to −8.46% |
| Defaulted rules | Compile to result | −10.21 to −6.54% | −10.95 to −7.28% |
| Short nonlocal report | Compile to result | −2.58 to +1.63% | −0.87 to +3.19% |

The ranges compare worker medians. Defaulted warm reference workers
spread 9.37%, so the direction is consistent and the size uncertain.
Annotated warm elapsed remains inconclusive with 9.54% reference
and 6.09% candidate spread. Warm short-report and quoted-invoice
regressions have supporting CPU. Keep both as controls for subsequent
runtime work. The captured script allocates 152,896 fewer CLR bytes
per invocation, down 10.25%. Other allocations and all admitted
peaks match. Allowances return to baseline.

Retained together in `df89d5b6b`. Both Windows and Ubuntu pass 505
focused .NET 11 Release checks. Coverage includes live rebinding and
defaults, unbound spans, integer identity and promotion, suspending
guest truth and operator methods, cleanup and refused admission.
The preceding [capture-only comparison](../2026-11-24-closure-scalar-operands/README.md)
retains its adverse annotated and first-use observations.

Frozen `2334fafec` and `768cd861d` differ only in the core DLL across
32 verified image files. The reference source matches the runtime
in `582f07328` and the candidate source matches `df89d5b6b`.
The dedicated Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Two workers per revision warm for eight
seconds and alternate seven samples each. Elapsed includes applicable
mediated RAM-file I/O, golden checking and prior-result disposal.
Compile to result adds compilation and compiled-owner disposal.
Wider process CPU includes request and reset overhead. Startup
is outside these intervals.

All 224 batches match CPython 3.13.16 goldens. Image and quiet gates,
worker joins, fixture cleanup and persistent archive verification pass.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Instruction changes](tape-summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures and exact patch](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
