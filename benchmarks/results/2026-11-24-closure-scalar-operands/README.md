# B1571 Captured arithmetic operands in scalar returns

The compiler forwards adjacent lexical reads into the existing
arithmetic operand positions. The captured fallback compiles from
five instructions to three and enters the existing scalar-return
path. That displaces 2,048 native activations per captured-rule
invocation. The `apply` and `alternate` tapes lose three and one
loads respectively. The four control workloads have identical tapes.

| Complete script | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rules | Warm | −3.29 to −0.69% | −2.95 to −0.34% |
| Defaulted rules | Warm | −2.29 to +0.77% | −3.45 to +0.32% |
| Annotated rules | Warm | +2.46 to +4.51% | +1.50 to +5.50% |
| Short nonlocal report | Warm | −0.55 to +6.47% | −2.22 to +6.42% |
| Quoted invoice | Warm | +0.19 to +1.01% | +1.30% |
| Captured rules | Compile to result | −5.77 to −0.42% | −4.85 to −1.04% |
| Short nonlocal report | Compile to result | +1.35 to +5.60% | +1.42 to +4.07% |

Captured rules allocate 152,896 fewer CLR bytes per invocation, a
10.25% reduction. All admitted peaks match and allowances return to
baseline. Keep this version private while comparing the complete
conditional and linear design against the delivery baseline. The
annotated, short-report and invoice control costs inform that decision.

Windows and Ubuntu each pass 504 focused .NET 11 Release checks.
Those include rebinding, unbound capture diagnostics at the original
read span, wide promotion and a suspending operator with owner reuse.
The first fixture used a CRLF offset against an LF-normalized span.
The corrected assertion slices the same source after normalization.

Frozen `2334fafec` and `91db4e0bb` differ only in the core DLL across
32 verified image files. The reference matches the runtime in
`582f07328`. The dedicated Azure F4as_v7 runs Ubuntu 24.04.4 and .NET
11 RC3 on November 24, 2026 UTC. Two independent workers per source
warm for eight seconds and contribute seven rotated samples each.
Elapsed includes applicable mediated RAM-file I/O, golden checks and
prior-result disposal. Compile to result adds compilation and disposal
of the compiled owner. Wider CPU includes reset and request handling.

All 196 batches match the CPython 3.13.16 goldens. Both quiet gates,
worker joins, fixture cleanup and persistent archive verification pass.
The initial collector used an incorrect reference directory and stopped
before starting fixtures or workers. The corrected collection reused
the verified staged images and catalogs.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Instruction changes](tape-summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures and exact patch](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
