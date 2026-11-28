# B1942 Give exact sequence and generic iterators their own storage

The refined candidate at `405afff35` keeps length hints, rendering and
construction on the existing iterator surface. Exact list and tuple
iterators carry the live index and use the native sequence reader. Other
iterators keep their cursor without the initial candidate's extra fields.
The visible generator outer iterator, exhaustion and cancellation checks
retain their existing meaning. [Initial result](../README.md).

Both APIs save about 164 KB per ordinary reduction in the Windows .NET 11
allocation witness, about 4.36%. The range-based and file-fed controls match.
Iterable shipment varies by at most 174 B. Compiled and peak execution
charges match for all four scripts, and every command returns the shared
allowance to baseline.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary tuple reduction | `RunAsync` | −11.48% to −3.43% | −5.45% to −3.80% |
| Ordinary tuple reduction | `Run` | −2.79% to −1.43% | −3.81% to −2.09% |
| Iterable shipment | `RunAsync` | −0.47% to +1.23% | −0.62% to +1.54% |
| File-fed quantity map | `RunAsync` | +0.65% to +4.21% | −0.55% to +3.20% |
| Range-based reduction | `RunAsync` | −2.25% to +3.71% | −2.16% to +0.67% |

The November 28, 2026 local WSL run reuses B1941's exact reference, catalog,
input and five warm lanes. .NET 11 RC3 runs on one logical CPU. Two workers
per image warm for five seconds, then rotate through seven batches. Ranges
pair all worker medians. Warm elapsed includes mediated RAM-backed I/O,
verification and previous-result disposal. Process CPU adds fixture reset
and framing. Compilation and startup are outside both clocks.

Both verified 32-file images match all four CPython 3.13.16 goldens and
differ only in the core DLL. All 140 batches verify and workers join
before archive verification. Windows .NET 10/11 and Ubuntu WSL .NET 11
each pass 163 public and 80 white-box focused Release checks on the timed
source. The delivery also preserves existing iterator type labels in
the ZIP and JSON diagnostic paths. Each of those three configurations
passes 374 focused public checks after the diagnostic edits, including the
ZIP comment regression. The timing predates the diagnostic edits.
[Delivery validation](diagnostic-validation.tar.xz).

Retain the refinement for lower ordinary allocation and CPU in both APIs.
The original iterable CPU increase is inconclusive on the refined source.
The file-fed map retains adverse elapsed and inconclusive CPU. Carry this
control into the next quiet checkpoint. External CPython timings remain
tied to their measured revisions.

[Timing](summary.json) · [Allocation](allocation.json) · [Image identity](identity.json) ·
[Archive receipt](archive-receipt.json) · [Observations](observations.tar.xz) ·
[Source patches and procedures](procedures.tar.xz).
