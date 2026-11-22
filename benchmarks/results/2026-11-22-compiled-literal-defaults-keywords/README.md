# B1326 Load literal default tuples and prepare keywords separately

A declaration with scalar-literal positional defaults loads one immutable
compiled tuple. Mixed declarations prepare only the keyword record after
that load. They share the existing inline keyword admission helper with
ordinary definitions. Evaluated positional defaults still enter the
ordinary allocator. Compiled instruction inspection verifies identical
control tapes and two fewer allocation-filling instructions in each
literal factory. Keyword evaluation still follows the original order and
can suspend before the function is published.

Compared `8c81fb8d2` with `505a2a20a` on November 22, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two independent
processes per image warmed for 16 seconds each and supplied seven rotated
batches. Warm lanes reverse the first collection's order. Ranges span
all process-median pairings. Negative changes favor the candidate.
Warm elapsed includes execution, golden checks and result cleanup.
Time to result adds compilation and code-owner cleanup. Process CPU
includes the wider worker request. The workloads use no file I/O.

| Complete script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Fully supplied ordinary helper | Warm | −1.08 to +0.20% | −2.51 to +0.32% | 0.87 / 0.42% |
| Dynamic positional defaults | Warm | −4.31 to +2.13% | −3.57 to +2.45% | 1.17 / 5.50% |
| Literal positional defaults only | Warm | −13.65 to −7.23% | −13.93 to −5.34% | 1.49 / 5.86% |
| Literal positional and evaluated keyword defaults | Warm | −10.67 to −8.19% | −10.97 to −7.74% | 0.10 / 2.68% |
| Literal positional and evaluated keyword defaults | Compile to result | −14.42 to −10.71% | −14.36 to −11.21% | 3.55 / 0.76% |

Retain the separate keyword preparation. Both literal factories again
use less CPU and allocate 15.51% and 18.09% less. The mixed time-to-result
gain holds with a tighter candidate spread. Dynamic timings span zero
with 5.50% elapsed spread. Preserve the adverse initial result and the
remaining uncertainty. Each affected accounted peak drops 491,520 bytes.
Both controls have identical allocation and peaks, and all allowances
return to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 631 white-box and 2,363
public focused Release checks plus all four original default-accounting
stress cases. The tests cover distinct owned defaults under budget
pressure, immutable tuple identity, independent reassignment and awaited
keyword evaluation. Both full worker images verify CPython goldens. Both
quiet gates and all 140 timed batches pass. Workers join and the archive
is verified after fixture cleanup under the quiet lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Original comparison](../2026-11-22-compiled-literal-defaults/README.md).
