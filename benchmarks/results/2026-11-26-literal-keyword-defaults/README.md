# B1788 Initialize literal keyword defaults in admitted storage

The trial uses source `f46a574f35f9c51e48bbdcb8f92a5c0462cae13f`
and the [recorded patch](candidate.patch). Prepared immutable literals
initialize each function's keyword-default slot or backing directly.
This removes separate load and store instructions. The function admits
its own backing before initialization. Computed defaults keep their
evaluation order. The literal values stay private until the callable
can expose them.

## First quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. Two workers per source warm for five seconds before seven
rotated timed batches with a 440 ms target. Ranges span all pairings
of worker medians. A minus sign means the candidate used less time.
Warm execution includes result cleanup and RAM-backed I/O in the file
control. Through-result measurement adds compilation and compiled
program disposal. Wider process CPU also includes request work.

| Complete script and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Escaping rules, warm sync | −7.57% to −2.36% | −8.65% to −0.71% |
| Ordinary definitions, through result | −0.01% to +2.27% | +0.18% to +3.70% |
| Escaping rules, warm async | −9.06% to −3.96% | −9.09% to −4.63% |
| Protected file search, warm sync | −2.50% to −0.17% | −4.39% to +0.05% |
| Escaping rules, through result | −2.39% to +1.86% | −3.55% to +1.21% |
| Ordinary definitions, warm sync | −1.69% to −0.64% | −3.66% to −0.67% |

Both quiet checks and all 168 batches pass. Workers join, RAM-backed
fixtures are removed and the persistent observations archive verifies.
The first warm sync result overlaps the favorable drift on the unused
file-search control. The escaping-rules through-result timings span
zero. These are Lython source-pair results. The four CPython 3.13.16
goldens qualify outputs and files for both images.
[Reordered confirmation and delivery decision](confirmation/README.md).

## Correctness and ownership

Windows .NET 11 and Ubuntu .NET 10 each pass 220 focused white-box and
940 focused public Release tests. Each platform also passes the four
existing default-accounting stress scenarios. The new regression covers
suspending positional defaults, separate keyword-default mappings,
replacement with an old alias retained, file disposal and accounting
returning to the compiled baseline.

An isolated instrumented build observes 2,048 literals initialized
directly in closed pair members per full escaping-rule run in each host
API. The ordinary factory, scope observer and protected search have no
hits in either API. Warm CLR allocation and peak execution admission
match on all four fixtures. The escaping factory retains 80 more admitted
compiler bytes. Both compiled and execution owners return their allowance.

Staging reused a previously frozen reference and retired two inactive
duplicate VM DLLs after checking each full image and preserving a verified
local backup of each DLL. The historical observations remain unchanged.
The first custody preview used an earlier local artifact for one VM copy
and stopped before mutation. The corrected preview matches the archived
quiet image.

[Timing and worker spreads](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Allocation and admission](allocation-summary.json) · [Instrumented routes](routes.json) ·
[Checks and procedures](procedures.tar.xz)
