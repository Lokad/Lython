# B1916 Enter a ready generator row directly

An exhausted generator with its last argument slot filled reaches the
bound callback directly from the admitted workspace. The drain fills
cleared slots in order, so the last slot proves exact arity. The ready
path drops the row's async driver and count scan. An incomplete pull
keeps the same workspace until the ordinary callback or binder takes
over. A suspended callback owns its loan through cleanup. The fallback
binds the prefetched values and any excess yield in their original order.

Against `113bf9523`, only the core DLL differs between complete
32-file images. Both match four independent CPython 3.13.16 output
and file goldens. Both APIs return the allowance to baseline after
every allocation witness. All compiled charges and run peaks match.
Iterable shipment peaks at 4,304,384 B. Median CLR allocation changes
by at most 56 B across the four cases and both hosting modes.

## Local timing

November 28, 2026, on Ubuntu 24.04 WSL with .NET 11 RC3, pinned to one
logical CPU. Two workers per image each warm for five seconds or more
and contribute seven rotated batches. Ranges span pairings of worker
medians. Warm elapsed includes mediated I/O, verification and previous
result disposal. Process CPU also includes fixture reset and request
framing. Compilation and startup are excluded.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | Async | +1.11% to +3.44% | −5.40% to −0.43% |
| Bound shipment control | Async | −7.24% to −0.03% | −5.57% to −0.67% |
| Iterable shipment | Sync | −2.87% to +0.41% | −1.60% to −0.40% |
| Wide shipment control | Async | −1.86% to +2.54% | −2.22% to +0.93% |
| File-fed map control | Async | +1.26% to +1.70% | −0.68% to +2.99% |

The first 140 batches verify. The iterable async candidate workers
differ by 4.55% in CPU, and the unchanged bound workers move in the
same direction as iterable CPU. The first elapsed slowdown and moving
controls prompted a bounded same-image repeat with reversed placement.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | Async | −4.77% to −1.61% | −3.78% to −2.54% |
| File-fed map control | Async | −0.61% to +5.02% | −3.17% to +1.29% |
| Bound shipment control | Async | −1.59% to −0.26% | −1.39% to +0.95% |

All 84 repeat batches pass. Iterable CPU worker spreads fall below 1%.
Both control CPU ranges span zero. Retain the ready path for the
removed driver and the CPU reduction measured in both local runs.
The elapsed observations conflict. Keep the first adverse result
and the file-fed control variation.

## Ownership and semantics

The focused checks cover ordered generator pulls, short and excess rows,
changed defaults, retained scalar aliases and refusal. The additional
workspace cases use a ready generator row followed by an actual
suspended callback. They verify retained funding during file close,
cancellation, preserved aliases on success and return to baseline.
Windows .NET 10 and Ubuntu WSL .NET 11 each pass 1,099 public and 342
white-box focused checks. Windows .NET 11 passes the same focused set
with the initial fixture, plus the seven workspace checks with the final
fixture. The final callback body uses an intermediate local so the
generator case exercises the admitted workspace entry.

[Image identities](identity.json) · [Allocation and admission](allocation-summary.json) ·
[First timing](summary.json) · [First observations](observations.tar.xz) ·
[Repeat timing](repeat/summary.json) · [Repeat observations](repeat/observations.tar.xz) ·
[Source and checks](procedures.tar.xz).
