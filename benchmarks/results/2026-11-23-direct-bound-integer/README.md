# B1430 Read omitted integer arguments from their owners

Private `89579aae7` reads supplied arguments and prepared defaults
directly in the existing closed integer-return evaluator. Successful
calls skip ordinary binding, reference-slot workspace initialization and
the earlier experiment's scalar staging. Live keyword mappings, invalid
arity, general Python objects and wide arithmetic use the existing
fallback. The path retains managed recursion and stack admission,
caller and body cancellation, and ordered failure before guest effects.

Compared with the exact B1429 `1b841b597` core on November 23,
2026 UTC on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Only the core differs across the two verified 32-file images.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Postponed nested factories | Warm | −10.24 to −8.10% | −10.43 to −7.91% |
| Postponed nested factories | Compile to result | −5.77 to −3.62% | −7.26 to −3.76% |
| Eager nested factories | Warm | −6.00 to −0.66% | −7.33 to −0.85% |
| Factories reading defaults metadata | Warm | +3.87 to +5.19% | +3.18 to +6.12% |
| Fully supplied helper | Warm | +0.10 to +4.92% | −1.21 to +4.49% |
| Quote-heavy CSV invoice | Warm | −4.47 to −1.69% | −5.14 to −1.81% |

Ranges span the medians of two independent workers per source.
Each worker warms for eight seconds and supplies seven rotated
batches. Warm elapsed includes applicable mediated RAM-backed I/O,
verification and previous-result disposal. Compile to result adds
compilation and compiled-owner disposal. Wider CPU includes resets
and protocol. Startup and final teardown sit outside elapsed.

The factory improvements warrant refinement. The defaults observer
loses in every elapsed and CPU pairing. Fully supplied candidate
workers differ by 4.68%, with only 0.13% reference spread. The
refinement can reject a live keyword mapping before checking the
positional tuple and trace fully supplied arguments through their
own specialized integer reader. Preserve the observed regressions.
Local allocation is effectively flat, admitted peaks match and all
allowances return after cleanup.

Both images match five CPython goldens. A separate program checks
missing unused parameters, default replacement and wide promotion on
CPython 3.13.2 and both Lython APIs. Windows .NET 11 and Ubuntu .NET 10
each pass 405 focused Release checks. Initial builds required explicit
readonly span fields for the reader structs. Local Windows JIT logs
show both specialized evaluator bodies with tiering disabled for the
diagnostic. VM timings used normal tiering.

All 168 batches and both quiet gates pass in 330 seconds. Workers
join, fixtures are removed and the persistent archive verifies under
the lease. Staging retires only exact member replicas of a previously
archived profile. No new CPython elapsed ratio was measured.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Reference checkpoint](../2026-11-23-retained-cpython-checkpoint/README.md).
