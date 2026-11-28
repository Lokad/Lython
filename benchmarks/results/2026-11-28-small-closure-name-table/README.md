# B1911 Reuse ordered names for small closure lookups

The compiler and runtime resolve up to four closure names from their
existing slot order. A wider scope creates the indexed form when the
fifth name is interned. The same lookup resolves forwarded cells when
the compiler prepares a child scope. No extra per-execution storage is
needed.

The reference is `44cff04d8`. Both 32-file images are verified and only
the core DLL differs. The compiler probe matches emitted tape, slot order,
admission and refusal across 18 source cases and 140 budget levels.
Twenty-two diagnostic sequences and cancellation with allowance cleanup
also match. The wide closure witness contains six captured names in two
successive scopes. It forwards cells, writes through `nonlocal` and deletes
one binding. Its output matches CPython 3.13.16 and both Lython APIs.
Focused Release checks pass on Windows .NET 11 (402), Windows .NET 10
(372) and Ubuntu WSL .NET 11 (372).

| Script | Compiled scopes | CLR bytes per compile, reference → candidate | Peak admitted bytes |
| --- | ---: | ---: | ---: |
| Ordinary definitions | 99 | 1,351,016 → 1,343,096 | 889,975 |
| Escaping definitions | 99 | 1,385,528 → 1,377,608 | 973,783 |
| Guarded report | 97 | 2,116,760 → 2,109,000 | 2,410,080 |
| Nested JSON control | 2 | 47,376 → 47,216 | 69,840 |

The table reports medians of 17 warmed Windows .NET 11 compilations with
disposal. Each of these benchmark scopes has no closure names. The
separate parity specimens exercise small and wide captures. All compile
allowances return to baseline.

## Matched local timing

On November 28, 2026 UTC, Ubuntu WSL ran .NET 11 RC3 pinned to one
logical CPU. Two workers per image warm for at least five seconds each
and supply seven rotated batches. The intervals span all pairings of
their medians. Compile includes disposal of the prior compiled owner.
Warm and compile-to-result include applicable mediated reads, golden
checks and result cleanup. Process CPU also covers fixture reset and
request framing. Startup sits outside the clocks.

| Script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile | −4.62% to −0.64% | −1.43% to −0.43% |
| Escaping definitions | Compile to result | −10.39% to +3.36% | −6.81% to +0.43% |
| Guarded report | Compile | −0.58% to +3.49% | +0.34% to +0.71% |
| Escaping definitions | Warm | −3.71% to −0.87% | −2.50% to +1.05% |
| Nested JSON control | Warm | −5.97% to +10.45% | −10.56% to +11.82% |

Both images match four independent CPython 3.13.16 output and file
goldens. All 140 timed batches verify, all workers join and the
persistent archive verifies after fixture cleanup. The candidate
workers differ by 7.38% on escaping compile-to-result CPU and 14.52%
on the nested JSON control. Guarded compilation uses slightly more
CPU despite close same-image spreads. Retain the removal of the
per-scope dictionary and record the mixed timing separately.

[Compiler parity and allocation](compiler-parity.json) · [Image identity](identity.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Source patches and focused checks](procedures.tar.xz).
