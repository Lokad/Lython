# B1952 Combine generator-expression frame admission

Generator-expression construction now admits the context shell, initial
executable slots and their local cells in one request. Both allocations were
already admitted before acquiring the outer iterator. Refusal of the combined
request names the generator expression and precedes outer iterator effects.
Captures from the parent keep their existing preflights and ownership.

The slot storage retires at frame cleanup. The context shell remains funded
for the execution. Native values such as `ChainMap` and `ZipFile`, as well as
nested functions, can retain it after the reduction returns. A new regression
exports a `ChainMap` and a closure through a numeric callback and uses them
after `sum` completes.

## Admission and allocation

The Windows .NET 11 witness counts positive resize requests on the borrowed
host budget. Both APIs show the same counts for each complete invocation.

| Workload | Reference requests | Candidate requests | Peak admitted bytes |
| --- | ---: | ---: | ---: |
| Ordinary tuple reduction | 14,365 | 12,317 | 5,940,647 |
| Range reduction | 16,413 | 14,365 | 6,268,415 |
| Iterable shipment | 6,180 | 6,179 | 4,304,384 |
| File-fed quantity map | 2,075 | 2,075 | 2,270,071 |

The reductions remove one growth request per record for 2,048 records.
Both images report identical peaks and compiled reservations. Every
invocation refunds to the compile baseline and disposing the compiled
owner returns the rest. In a separate allocation witness, the reductions
match CLR allocation and the controls differ by at most 56 B.

## Warm execution

The November 28, 2026 local Ubuntu WSL comparison uses .NET 11 RC3 and
one logical CPU. Two workers per image warm for five seconds each and
contribute seven rotated batches. Ranges pair all worker medians. Warm
elapsed includes applicable RAM-backed I/O, verification and previous-result
disposal. Process CPU includes reset and framing as well. Both clocks exclude
compilation and startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary tuple reduction | `Run` | −1.65% to +1.16% | −1.91% to +0.38% |
| Ordinary tuple reduction | `RunAsync` | −0.32% to +3.07% | −1.93% to −1.00% |
| Range reduction | `RunAsync` | −3.10% to −1.28% | −2.05% to −1.79% |
| Iterable shipment | `RunAsync` | +0.01% to +1.04% | −0.74% to +1.94% |
| File-fed quantity map | `RunAsync` | −0.09% to +3.87% | −1.06% to +3.61% |

Both full images verify four CPython 3.13.16 output and file goldens.
Only the core DLL differs from `52ba7a052`. All 140 timed batches verify,
workers join and archival verifies after fixture removal. Focused Release
passes 743 public and 211 white-box checks on Windows .NET 10 and 11 and
Ubuntu WSL .NET 11. Keep the combined admission and the local async CPU
result. Ordinary sync and control CPU span zero. Carry the file-fed upper
result and its 4.40% candidate spread into the next quiet checkpoint.

[Admission](admission.json) · [Allocation](allocation.json) ·
[Timing](summary.json) · [Verified images](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
