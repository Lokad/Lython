# B1433 Share the prepared parameter layout through the body

Private `2f65080f6` stores the prepared signature with executable local
layout. It removes separate references from definition and lambda
descriptors, from each callable instance and from the construction
parameters. Generated callables still own their prepared layout.
Each argument binder reads the layout once and carries it through
pending keyword lookup. Mutable defaults remain live on the callable.

Compared with `47cdc8c80` using the exact B1431 core. Only the core differs
across the two verified 32-file images. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −3.24 to +0.60% | −2.31 to +0.77% | 2.72 / 1.22% |
| Postponed nested factories | Compile to result | +1.66 to +6.38% | +0.34 to +6.56% | 2.99 / 1.61% |
| Eager nested factories | Warm | +0.03 to +3.95% | +1.18 to +4.20% | 3.22 / 0.67% |
| Factories reading defaults metadata | Warm | −4.42 to −3.01% | −4.06 to −1.80% | 0.58 / 0.89% |
| Fully supplied helper | Warm | −1.79 to −0.38% | −2.34 to +0.53% | 1.08 / 0.36% |
| Quote-heavy CSV invoice | Warm | −1.90 to +1.40% | −2.61 to +2.25% | 1.95 / 1.39% |
| Large positional call control | Warm | −1.04 to +1.29% | −1.50 to +1.76% | 1.86 / 0.49% |

Ranges span the four pairings of medians from independent workers.
Each worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, verification
and previous-result disposal. Time to result adds compilation and
compiled-owner disposal. Wider CPU includes resets and protocol.
Startup and final teardown are outside elapsed.

Local Windows allocation falls by 32,776 bytes in each factory lane
(3.72% for the two ordinary factories and 2.67% for the observer).
Compile allocation decreases by 24 bytes for those scripts. Changes
in other execution allocations are at most 112 bytes. Every admitted
peak matches and every allowance returns to baseline.

Keep this pair private while refining native entry. Eager warm CPU and
postponed time to result both increase, while the observer reproducibly
improves. Native call selection already reads the layout. Call entry
reads it again on incomplete supply. Reuse that layout across selection,
closed arithmetic and ordinary binding, and compare the entire set of
workloads again. Local tier-disabled JIT code shrinks by 55 bytes in the
binder and grows by 46 bytes in native call entry. This provides shape
evidence. It leaves causality on the quiet VM unresolved.

Windows .NET 11 and Ubuntu .NET 10 each pass 2,664 focused Release
checks. Both images match six CPython goldens, 21 executable tape
fixtures and a separate CPython probe for positional-only keyword
overflow, live defaults, generators, descriptors and dataclasses.
The first build exposed two property patterns still using the old
binding-plan convenience property. Both initial logs are preserved.

All 196 batches and both quiet gates pass in 383 seconds. Workers
join, guest fixtures are removed and the persistent archive verifies
under the lease. Staging retires only the exact working replica of
the prior private core, preserved with its Git archive. This recording
adds no CPython elapsed ratio.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Reference core](../2026-11-23-direct-bound-integer-refined/README.md).
