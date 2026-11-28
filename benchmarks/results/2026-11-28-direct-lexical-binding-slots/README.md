# B1925 Collect lexical bindings into their final slots

Function and lambda binding walks now add each first body binding to its
final slot map. Parameter-only scopes borrow the existing signature tables.
A new local creates an independent extended map. Global and nonlocal
declarations can remove a name from that map in source order. Absent
directives share the existing empty set. This removes the transient list of
all binding occurrences and its separate map construction. Lookup,
scope diagnostics and instruction emission use the existing interfaces.

The reference is `d9689b659`. Both frozen 32-file images differ only in
the core DLL and match four independent CPython 3.13.16 output and file
goldens. The compiler witness matches tapes, slot metadata, nested captures,
admission and refusal across 20 source cases and 154 allowance levels.
It also matches 24 diagnostic sequences and cancellation with cleanup.
The cases include rebinding, variadic and keyword-only parameters, global
and nonlocal declarations, and assignment expressions in lambda bodies.
Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 432 public and
142 white-box focused Release checks.

| Script | CLR bytes per compile, reference → candidate | Peak admitted bytes |
| --- | ---: | ---: |
| Ordinary definitions | 1,343,096 → 1,306,424 | 889,975 |
| Escaping definitions | 1,377,608 → 1,340,936 | 973,783 |
| Guarded report | 2,109,000 → 2,090,440 | 2,410,080 |
| Function-free configuration control | 1,573,376 → 1,573,248 | 1,461,312 |

The table reports medians of 17 warmed Windows .NET 11 measurements with
compiled-owner disposal. All compile allowances return to baseline.

## Local timing

November 28, 2026 UTC on Ubuntu WSL and .NET 11 RC3, pinned to one logical
CPU. Two workers per image warm for at least five seconds and supply seven
rotated batches each. Ranges span pairings of worker medians. Compile-only
includes disposal of the prior compiled owner. Compile-to-result also
includes execution, golden verification and result cleanup. Process CPU
includes fixture reset and request framing. Startup is outside the clocks.

| Script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile only | −5.28% to −3.92% | −4.54% to −2.65% |
| Function-free configuration control | Compile only | +1.01% to +1.86% | +0.63% to +2.11% |
| Escaping definitions | Compile to result | −7.14% to −2.20% | −7.34% to −2.02% |
| Guarded report | Compile only | −3.17% to +1.75% | −4.39% to +2.54% |
| Guarded report | Compile to result | −3.08% to +6.27% | −1.41% to +2.96% |

All 140 batches verify, all workers join and the persistent archive
verifies after fixture cleanup. Retain the shared signature layout
and the removal of the duplicate binding list. Ordinary compilation
and escaping compile-to-result use less elapsed and CPU. The
function-free control uses 0.63–2.11% more CPU with narrow same-image
spreads. Both guarded-report lanes span zero. Keep the adverse control
in the next quiet qualification. Admission peaks match all cases.

[Compiler parity and allocation](compiler-parity.json) · [Timing](summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Source patches, checks and procedures](procedures.tar.xz).
