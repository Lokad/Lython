# B1843 Keep compiler temporaries out of the binding map

The builder appends the hidden slots for chained comparisons, `with`, and
`match` directly to the physical local layout. Source bindings reuse the
prepared name map. Hidden slots retain their numeric positions and diagnostic
names. Namespace seeding can enumerate the name map directly.

On the generated guard report, 96 functions reuse their prepared maps and
remove 192 hidden entries. The script is 24,587 UTF-8 bytes and evaluates all
96 rules on representative row dictionaries. The compiler probe compares 16
scripts and 121 budget levels using both frozen cores. Instruction operands,
spans, frame capacities, captures, visible name bindings, admission and
cancellation outcomes match. The missing hidden entries and the reused maps
are the expected changes.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 414 public and 487 white-box
Release checks. Three complete scripts match independent CPython 3.13.2
output and file goldens. Only the core DLL changes from `43aef0247`.

## Allocation and admission

The Windows .NET 11 allocation probe warms eight times and reports the median
of 17 compilations and disposals on the current thread. Governed compilation
admission and warm execution allocation and peak admission match on the three
complete scripts in both hosting APIs. Each release restores its allowance.

| Workload | CLR bytes per compilation and disposal |
| --- | ---: |
| Generated guarded report | 2,288,000 → 2,243,456 |
| Protected file search | 47,448 → 46,920 |
| Ordinary definitions control | 1,443,248 → 1,443,248 |

## Initial local timing

The Ubuntu WSL .NET 11 runs pin two workers per image to one logical CPU.
Each warms for five seconds and contributes seven rotated batches. Intervals
span all pairings of worker medians. Compilation includes compiled-owner
disposal. Compile to result adds execution, mediated I/O where applicable,
verification and cleanup. Process CPU also includes fixture reset and
worker framing. Both exclude process startup.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions control | Compilation only | −4.41% to +0.63% | −2.27% to +4.39% |
| Generated guarded report | Compilation only | −14.24% to +18.16% | −14.20% to +19.82% |
| Protected file search | Compile to result | −5.75% to +83.69% | −32.74% to +80.99% |
| Generated guarded report | Compile to result | −13.53% to +71.44% | −15.15% to +66.67% |

All 112 observations verify. The protected search reference workers differ
by 102.88% in CPU, and the guarded through-result candidate workers differ
by 83.33%. These observations leave timing uncertain.

## Same-image confirmation

The confirmation uses the same binaries and timing boundaries on a
different logical CPU. The launcher samples per-CPU busy time for one second
before choosing it. All 84 observations verify.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated guarded report | Compile to result | −5.16% to +12.99% | −21.21% to +6.67% |
| Ordinary definitions control | Compilation only | −4.69% to +1.24% | −8.00% to −0.21% |
| Generated guarded report | Compilation only | −0.45% to +4.31% | −1.81% to +6.89% |

The confirmation reduces the guarded compile-only spread. Both guarded CPU
ranges still span zero. Keep the simpler binding map and the measured
44,544 B allocation reduction per guarded compilation. Through-result
timing and the protected-search result remain inconclusive.

[Compiler parity and allocation](compiler-parity.json) ·
[Execution allocation and admission](allocation-summary.json) ·
[Image identities](identity.json) · [Fixture counts](fixture-census.json) ·
[Initial timing](summary.json) · [Initial observations](observations.tar.xz) ·
[Confirmation timing](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tar.xz) ·
[Affinity observations](confirmation/affinity-observations.json) ·
[Archive verification](archive-receipt.json) · [Exact patch and checks](procedures.tar.xz).
