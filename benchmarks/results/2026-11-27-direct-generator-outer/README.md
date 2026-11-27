# B1893 Use the acquired generator-expression iterator directly

A generator expression puts the acquired iterator into its existing loop
slot. This removes the surrounding cursor, its construction record and the
cursor-specific cleanup. The same `.0` local exposes the Python iterator's
identity when advancement uses a user protocol adapter. Optimized frames
use their local slot and mirrored frames also seed their namespace. The
existing stack and cancellation checks cover every loop advance.

Reference `63ae080ac` and the candidate with the archived patch use the
same 32-file image apart from the core DLL. The affected report reads
2,048 rows from a 27,350-byte CSV fixture and creates a generator expression
per row to count handling flags. Both images match four independent CPython
3.13.16 output and file goldens. The wide and iterable report controls
create fewer outer generator expressions. The file-fed map uses none.

The Windows .NET 11 allocation witness measures 622,592 fewer bytes in
both APIs, or 13.09% per CSV report. The reduction is 304 B per generated
row. The peak admitted balance is 5,940,679 B in both images and APIs.
All other admission peaks also match. Every case restores the borrowed
allowance baseline. The wide report saves 4,864 B in both APIs.

Focused Release checks pass on Windows and Ubuntu WSL with .NET 10 and
.NET 11. Public checks pass 1,147 on Windows .NET 11 and 213 on each other
configuration. Each white-box run passes 60. These checks cover async
suspension, cancellation, refusal, iterator identities and closure lifetime.

## Paired timing

November 27 UTC, pinned Ubuntu WSL and .NET 11 RC3. Two workers per
image warm for at least five seconds each and supply seven rotated
batches. Ranges span all pairings of worker medians. The warm stopwatch
includes mediated RAM-backed I/O, golden checks and prior-result cleanup.
Process CPU adds fixture reset and request framing. Compilation and
startup are outside both clocks.

| Pass | Workload | API | Elapsed change | Process CPU change |
| --- | --- | --- | ---: | ---: |
| Initial | Per-row generator reduction | `Run` | +2.66% to +7.72% | +0.13% to +6.89% |
| Initial | Per-row generator reduction | `RunAsync` | −3.56% to −0.29% | +0.87% to +1.46% |
| Initial | Wide shipment | `RunAsync` | −17.35% to −9.88% | −13.96% to −3.45% |
| Initial | Iterable shipment | `RunAsync` | −0.41% to +37.43% | −2.09% to +28.11% |
| Initial | File-fed map | `RunAsync` | −2.27% to +2.27% | −4.04% to +3.30% |
| Reverse placement | Per-row generator reduction | `Run` | +0.05% to +8.07% | +1.01% to +5.99% |
| Reverse placement | Per-row generator reduction | `RunAsync` | −2.26% to +19.30% | −4.41% to +4.40% |
| Reverse placement | File-fed map | `RunAsync` | −14.41% to +63.36% | −12.50% to +0.70% |

Sync process CPU increases in both passes. Async CPU spans zero on the
repeat. Its unchanged file-fed control has a 72.28% candidate elapsed
spread, so these results remain local diagnostics. Keep the simplification
and allocation reduction. Carry the sync CPU regression to a quiet timing
checkpoint and preserve both adverse observations. All 224 timed batches
verify. Workers join and the persistent archives verify.

## Compatibility observation

The archived independent probe checks a user iterator whose `__iter__`
returns itself and records each call. Lython calls it at construction in
both measured images and APIs. CPython 3.13.2 and 3.13.16 call it at
construction and again on first resume. Track that pre-existing behavioral
difference separately. A second probe gives CPython a different iterator
on that resume. It takes its values from the new iterator while `.0` still
exposes the original. The new regression checks original-object identity,
live `__next__` rebinding and reuse of the iterator after generator close.

[Initial timing](summary.json) · [Reverse-placement timing](repeat/summary.json) ·
[Allocation and admission](allocation-summary.json) · [Image identity](identity.json) ·
[Initial observations](observations.tar.xz) · [Repeat observations](repeat/observations.tar.xz) ·
[Exact source and verification](procedures.tar.xz).
