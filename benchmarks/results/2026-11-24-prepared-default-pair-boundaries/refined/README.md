# B1646 Keep the pair boundary in the existing code-object field

The refined implementation stores one verified instruction index for a
defaulted pair. The same field records captured pairs. The existing single
computed definition still uses its fixed default sequence. The defaulted
pair uses its two existing callable descriptors, retains the canonical
default containers and evaluates defaults on the instruction tape.

Compilation allocation, execution allocation and peak admission match for
all six checked scripts. The eight-byte code object growth in the first
trial is gone. Windows and Ubuntu each pass 1,621 focused .NET 11 Release
checks and 23 .NET 10 checks, including promotion, refusal between phases,
cooperative cancellation and refund of unpublished storage. Six CPython
3.13.2 goldens pass in each of the two frozen 32-file images.

| Workload | Phase | Elapsed change | Wider CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | Warm | −0.84 to +5.72% | −0.63 to +5.65% | 1.99 / 4.54% |
| Defaulted rules | Compile to result | −4.07 to −0.35% | −7.22 to −2.50% | 2.38 / 1.45% |
| Eager annotations | Warm | −6.54 to +13.13% | −4.75 to +15.35% | 7.45 / 12.66% |
| Captured rules | Warm | −2.34 to +3.35% | −2.52 to +5.88% | 3.85 / 1.92% |
| Short Unicode report | Warm | −4.25 to +5.12% | −4.25 to +7.96% | 5.73 / 3.83% |

Both trials use the same `9d50a1b1f` reference on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Ranges span
all pairings of medians from two workers per image. Workers warm for five
seconds and supply seven rotated batches. Warm elapsed includes applicable
RAM-backed I/O, verification and previous-result disposal. Time to result
adds compilation and compiled-owner disposal. Wider CPU also includes
framing and fixture reset. Both quiet gates, all 140 batches, worker joins,
fixture cleanup and archive verification pass.

Retain the refined representation. Both trials reduce defaulted rules'
time to result, with lower CPU in the refinement. Warm elapsed remains
uncertain between the two candidates. The eager control in the first run
was adverse and the refinement is noisy, with up to 12.66% worker spread.
Keep both outcomes for the next call-construction comparison.

[First trial](../README.md) · [Identity](identity.json) · [Timings](quiet-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Raw observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Exact patch and procedures](procedures.tar.xz).
