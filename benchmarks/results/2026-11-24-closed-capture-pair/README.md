# B1602 Complete the captured pair before the outer activation

Private `97313e53b` prepares the captured outer bindings in four
local slots. It shares the existing cell, callable and private-call
constructors. The caller's unpack window receives both retained results
after their construction and cleanup. Both call stacks get managed
admission. Dynamic inputs and observable bindings keep their original
instructions.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −27.29 to −24.01% | −27.12 to −24.53% | 2.10 / 2.37% |
| Defaulted rules | Warm | −3.38 to +5.69% | −1.56 to +5.19% | 2.70 / 6.50% |
| Eager annotated rules | Warm | −6.56 to −3.54% | −7.49 to −3.81% | 1.92 / 1.30% |
| Postponed annotated rules | Warm | −11.25 to +1.63% | −12.11 to +1.02% | 9.04 / 5.01% |
| Short Unicode report | Warm | −4.71 to +2.95% | −4.68 to +2.58% | 3.62 / 4.27% |
| Quoted invoice | Warm | −1.88 to +1.15% | −2.59 to −0.62% | 2.46 / 0.62% |
| Captured rules | Compile to result | −24.07 to −21.92% | −25.00 to −22.17% | 1.44 / 1.36% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. The reference runtime matches `ad88474bb` and the images differ
only in their core DLL. Seven rotated batches follow eight seconds of
warmup per worker. Each elapsed range covers the four pairings of two
independent worker medians. Warm includes applicable mediated RAM-backed
I/O, verification and previous-result disposal. Compile to result adds
compilation and compiled-owner disposal. Wider CPU includes the request.
Startup and final teardown have separate clocks.

The instrumented core completes 8,192 captured pairs across four
invocations with no outer or inner workspace. The ordinary text and
invoice workspace counts are unchanged. Captured allocation and admitted
peak match, and both return to the baseline. Each platform passes 106
focused Release checks, including cancellation and refusal at the
escaping lambda's admission. Six independent CPython programs and the
six script goldens verify on both images. All 196 timed batches, quiet
gates, joins, fixture cleanup and archive verification pass.

The complete outer activation can leave at a substantial CPU gain.
The enlarged shared parameter evaluator remains a source cost. It
grows from 1,111 to 1,810 bytes in the Windows FullOpts listing.
Eager rules improve despite the growth. Other controls leave timing
uncertain. Confirm the smaller seeded accumulator before integrating.

[Timings](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Route counts](route-counts.json) · [Generated code](jit-summary.json) ·
[Images](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and source](procedures.tar.xz) · [Storage custody](storage-custody.json) ·
[Terminal status](terminal-status.json) ·
[B1601 attribution](../2026-11-24-private-call-source-cpu/README.md).
