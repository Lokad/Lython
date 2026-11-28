# B1957 Run private integer generator reductions in borrowed slots

The candidate patch on `bcc822ecc` keeps the exact builtin outer iterator in
the admitted generator operand through the ordinary builtin call. The shared
instruction dispatcher accumulates integer yields. A dynamic operation or
integer overflow can transfer the same live slots, iterator and instruction
position into the existing generator frame. The regular reducer continues
with the accumulated prefix. The generator shell stays prepaid until the
operand relinquishes it or transfers it to a real frame.

The Windows .NET 11 allocation witness saves 491,520 CLR bytes per
2,048-row script in both APIs for the tuple and range reductions. That is
14.9% of tuple allocation and 12.6% of range allocation. Peak execution
admission falls from 5,940,647 to 3,779,207 B for the tuple and from
6,268,415 to 3,910,463 B for the range. The number of growth requests,
compiled admission and control peaks match. All commands return the
allowance to baseline.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 611 focused public
Release checks. Windows .NET 11 also passes 248 focused white-box checks.
The new numeric and protocol regression agrees with CPython 3.13.16.
The public cases exercise promotion after integer yields, exact overflow,
float correction, dynamic and reflected addition, genuine I/O suspension,
cancellation and budget cleanup.

## Local warm comparison

November 28, 2026 in Ubuntu WSL on .NET 11 RC3, pinned to one logical CPU.
The verified 32-file images differ in the Lython core DLL. Both pass four
CPython 3.13.16 output and file goldens. Two workers per image warm for
five seconds each and supply seven rotated timed batches. Ranges pair
all worker medians. Elapsed includes RAM-backed mediated I/O, verification
and previous-result disposal. Process CPU also includes fixture reset and
request framing. Compilation and startup are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Tuple generator reduction | `Run` | −13.98% to −9.60% | −14.88% to −8.17% |
| Tuple generator reduction | `RunAsync` | −21.14% to −17.78% | −20.12% to −16.18% |
| Range generator reduction | `RunAsync` | −21.80% to −10.85% | −21.22% to −13.53% |
| Iterable shipment control | `RunAsync` | −5.42% to +2.30% | −3.27% to +0.65% |
| File-fed map control | `RunAsync` | −3.40% to −1.58% | −1.81% to −0.33% |

The range reference workers differ by 6.40% in CPU. The iterable control
spans zero. Treat the modest favorable file-fed result as control variation.
All 140 timed batches verify, the workers join and the guest fixtures are
removed. These are revision-to-revision diagnostics. The external CPython
ratios still describe their earlier measured revision.

The first eligibility check used function-only compiler metadata. It
left the generator expressions on the existing frame path and peak
admission matched the reference. The procedures preserve that witness.
The timing harness rejected two starts before the first timed batch.
The corrected launch clears the inherited .NET bundle extraction setting
and preserves the empty output directory and both failure logs.

[Timing summary](summary.json) · [Allocation](allocation.json) ·
[Admission](admission.json) · [Worker images](identity.json) ·
[Observations](observations.tar.xz) · [Source and procedures](procedures.tar.xz).
