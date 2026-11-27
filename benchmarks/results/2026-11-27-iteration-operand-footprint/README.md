# B1857 Prove iterator callback storage during instruction emission

An eligible `map` or `starmap` previously scanned the callback's complete
instruction tape at construction. The candidate checks the same three-slot
footprint in the existing emission loop and records it on the code object.
The iterator still checks the live callable and signature. Both constructors
check cancellation after admission. Promotion admits the full frame before
retaining operands or resuming guest code.

Windows passes 781 public and 142 white-box focused Release checks on each
target. Ubuntu WSL .NET 10 passes 47 public and five white-box checks,
including suspended promotion, retained operands and terminal refusal. A
probe recalculates the old footprint predicate over the final tape and
agrees with the new metadata across 17 scripts. Both images match the
instruction tapes, compiled admission, cancellation and 117 budget levels.

Four complete scripts match independent CPython output and file goldens
on Windows and on the VM. Only the core DLL differs from `c665a179e`.
Peak execution admission and retained compilation admission match in both
APIs. All allowances return to baseline. The generated report allocates
5,186,800 B per compile in both images. The largest warm allocation
difference is 174 B on the nested-JSON control.

## Initial quiet timing

November 27, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds each and supply seven
rotated batches. Ranges span pairings of worker medians. Warm elapsed
includes applicable mediated RAM-backed I/O, verification and previous-result
disposal. Compile-only includes compiled-owner cleanup. Process CPU also
includes fixture reset and request framing. Startup sits outside both clocks.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| 2,048 `map` and 2,048 `starmap` constructions | Warm | −4.58% to −2.63% | −5.23% to −2.13% |
| File-fed map | Warm async | −0.59% to +1.06% | −1.26% to −0.01% |
| Generated comprehension report | Compile only | −2.54% to −0.27% | −2.09% to +3.62% |
| Nested JSON control | Warm | −0.79% to +21.09% | −1.47% to +17.86% |

Repeated construction improves across all worker pairings. One candidate
worker on the unchanged JSON control produces a 20.23% elapsed and a 19.07%
CPU spread in that image. Keep this observation and repeat the same images
with JSON first and the initial worker order reversed. All 112 initial
batches and both quiet gates pass. Staging reuses 32 reference files and 31
candidate files. Workers join and the persistent archive verifies after
the RAM-backed fixtures are removed.

## Same-image confirmation

The repeat runs nested JSON first and reverses the initial worker order.
It uses the same timing boundary and two unchanged worker binaries per
image. All 56 batches and both quiet gates pass. Staging reuses all 64
image files. The workers join, the RAM-backed fixtures are removed and
the persistent archive verifies.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested JSON control | Warm | +0.84% to +2.77% | −0.71% to +0.61% |
| Repeated iterator construction | Warm | −3.47% to +0.01% | −2.11% to 0.00% |

Keep the prototype out of delivery. The earlier large JSON spread does
not recur. JSON still takes more elapsed across worker pairings and its
CPU spans zero. Repeated construction reaches parity in the repeat. The
original runtime scan stays in production and the measured change and
both sets of observations remain available.

[Compiler parity](compiler-parity.json) · [Fixture counts](fixture-census.json) ·
[Initial timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Initial observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patch and checks](procedures.tar.xz) ·
[Confirmation timing](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tar.xz) ·
[Confirmation procedure](confirmation/procedures.tar.xz).
