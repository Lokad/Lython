# B1900 Reuse lexical slots in local-name diagnostics

Local-name analysis now uses the compiler's existing name-to-slot map.
Each scope tracks assignments in a Boolean array and journals slot
indices across branches. This removes the independent set of assigned
names, the copied local-name set for nested nonlocals and the extra name
lookups at branch joins. Postponed annotation lambdas can still use a
private slot map where lexical preparation skipped the expression.

The reference is `cf0b36779`, with core source from `5cb61871a`. The two
complete 32-file images differ only in the core DLL. Both verify four
independent CPython 3.13.16 output and file goldens. Seventeen scripts
match emitted code, bound slots, refusal at 131 budget levels, compiled
peaks and allowance cleanup. Nine specimens match diagnostics, including
code, order and spans. They cover branch scope, deletion, exception
targets, nested defaults, postponed annotations, comprehensions,
nonlocals and a function with 144 locals. The compilation cancellation
witness restores the allowance to baseline. All 245 focused Release
checks pass on each Windows and Ubuntu WSL target (.NET 10 and 11).
The probe's first summary
used a mismatched JSON property spelling. Its corrected run verifies the
same pair of binaries.

## Compilation allocation

Windows .NET 11 medians over 17 compilations with disposal. Governed
peaks match. The separately measured allocation is CLR allocation.

| Script | Reference | Candidate | Bytes saved |
| --- | ---: | ---: | ---: |
| Ordinary definitions | 1,381,208 B | 1,351,624 B | 29,584 B |
| Escaping definitions | 1,415,712 B | 1,386,128 B | 29,584 B |
| Guarded report | 2,147,472 B | 2,119,056 B | 28,416 B |

## Whole-script clocks

November 27, 2026 UTC with .NET 11 RC3. The first pass runs on Ubuntu WSL
pinned to CPU 27. The quiet repeat runs on the Azure F4as_v7 with Ubuntu
24.04.4 and reverses initial worker placement. Two workers per image warm
for at least five seconds and supply seven rotated batches each. Ranges
compare all pairings of worker medians. Compile includes disposal of the
prior compiled owner. Warm and compile-to-result include applicable
mediated RAM-backed reads, golden checks and result cleanup. Process CPU
also covers reset and framing. Startup sits outside these clocks.

| Workload | Lane | WSL elapsed | WSL CPU | Quiet elapsed | Quiet CPU |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | Compile | −8.55% to +8.18% | −12.23% to +11.09% | −3.48% to −2.11% | −4.14% to −2.82% |
| Nested JSON control | Warm | −1.57% to +0.83% | −2.09% to +0.27% | −1.20% to +0.18% | −2.38% to +1.83% |
| Guarded report | Compile | −3.50% to +0.14% | −2.05% to +0.75% | — | — |
| Escaping definitions | Compile to result | −3.47% to +1.43% | −1.26% to +1.61% | — | — |
| Guarded report | Compile to result | −0.39% to +5.74% | −0.62% to +4.92% | −2.98% to −1.06% | −2.49% to +0.80% |

The WSL ordinary lane varies by 17.88% in CPU between identical candidate
workers. The quiet repeat confirms lower ordinary compilation CPU and
elapsed. Guarded elapsed through result falls on the quiet VM with CPU
spanning zero. The nested JSON control spans zero in both clocks.
The other WSL timing ranges remain inconclusive.

The two runs verify 224 batches. The quiet run joins all workers and
verifies its persistent archive after fixture disposal and both machine
checks. Retain the shared slot representation and its allocation savings.

[Compiler parity and allocation](compiler-parity.json) ·
[WSL timing](summary.json) · [Quiet timing](quiet-summary.json) ·
[Image identity](identity.json) · [WSL observations](observations.tar.xz) ·
[Quiet observations](quiet-observations.tar.xz) · [Procedures and checks](procedures.tar.xz).
