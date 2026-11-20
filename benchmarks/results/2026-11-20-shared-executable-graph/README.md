# B1212 Shared executable graph discovery

Measured on 2026-11-20 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. The measured candidate is `356a93e25`, applied to the
source at `8d6a92140`. The reference image was built at source-equivalent
`7f583edfa`. Each revision uses two independent processes, an eight-second
minimum warm-up and five rotated rounds targeting 0.4 seconds per batch.
Ranges cross all pairings of process medians. Negative changes indicate
less time on the candidate.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated configuration validator | Compile | −14.62% to −5.41% | −12.93% to −2.69% | 10.60% / 0.16% |
| Configuration manifest | Compile | −1.49% to +0.65% | −2.48% to +0.55% | 0.24% / 1.93% |
| Generated configuration validator | Compile to result | −10.22% to −6.57% | −10.74% to −7.87% | 2.49% / 1.53% |
| Configuration manifest | Compile to result | +0.11% to +1.06% | −3.07% to +1.67% | 0.41% / 0.54% |
| Generated configuration validator | Warm | +0.42% to +2.11% | +0.85% to +1.44% | 0.21% / 1.48% |
| Eager annotated rules | Warm | −2.85% to +1.23% | −2.59% to +0.00% | 0.64% / 3.53% |

Warm execution includes mediated input, report/result validation and
previous-result cleanup. Compile to result adds compilation and compiled
code-owner disposal. Compile-only disposes code within each invocation.
Host setup, process startup, fixture reset and final process teardown
are outside the guest clocks.

The compiler assigns operand depths and discovers reachable instructions
in one walk. This removes the independent reachability scan, the temporary
compacted block list and the protected-region normalization pass. The
original block order and protected widths remain available through final
emission. A reachable jump-only protected entry remains a possible unwind
boundary. In the three timed scripts the inspected opcode, operand,
span and exception-region metadata agrees between images. The validator
contains 2,927 instructions and 97 protected regions.

Retained for the compiler simplification and the validator's compile-to-result
improvement. Validator compilation is faster in all pairings, with a 10.60%
spread across the reference processes that limits precision. Validator warm
elapsed and CPU both increase. Manifest compile-to-result elapsed also
increases slightly with mixed CPU. Eager warm elapsed is inconclusive.
Local compile allocation falls 1.05% for the validator and rises 0.45%
for the manifest and 1.64% for the eager factory. Warm allocation and
accounted execution peaks match. Each borrowed allowance returns to baseline.

The first region-free prototype, `40b5e57fe`, still used the old walk for
the protected validator. Its compile allocation was effectively flat on
both validator and manifest. The source and local checks for that prototype
are included under `unprotected-initial/`.

The exact retained source passed 1,735 focused public and 705 focused
white-box checks on both Windows .NET 11 and Ubuntu .NET 10. Fourteen
CPython 3.13 probes agree with both Lython execution modes on both
images, including nested cleanup and exception selection. A focused
asynchronous regression exercises a jump-only protected entry with a
live file cursor, then verifies cancellation, awaited cleanup and budget
reuse. Each 32-file image was verified locally and on the VM. Both quiet
gates and all 120 timed batches passed. All workers joined and the
persistent archive verified before the lease ended.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Inspected instruction counts](instruction-inspection-summary.json) ·
[Raw observations](observations.tgz) · [Source and verification](source-and-checks.tar.xz).
