# B1662 Share prepared function-local slots with emitted code

Lexical preparation assigns slots in the existing binder's parameter order,
then appends each body binding in its first source position. Scope membership
uses the prepared index. Emission borrows that index and its ordered names
directly. A body with synthetic locals extends a private copy. Module and
class namespaces and the generator expression's hidden input keep their
existing layout.

Both Windows and Ubuntu pass 432 focused Release checks on each target,
including variadic parameter snapshots, closures, generator declarations,
class scoping, cancellation and first refusal. Seven sources produce the
same 28 code objects and 4,199 instructions, with the same spans, local
slot order and retained admission. The variadic fixture checks the known
difference between source parameter order and executable argument order.

The reference is the exact B1661 candidate, based on `694c1645b` with the
two recorded direct-return patches. The candidate adds the local-slot
patch only. Production correctness checks include the captured-pair fix.
Both 32-file timing images verify five complete CPython 3.13.2 goldens.

The local Windows allocation diagnostic puts defaulted rules at 64,288 B
per compile, down 1,184 B or 1.81%. The manifest takes 213,664 B, down
1,712 B or 0.79%. The other three sources save 128–912 B. All retained
admission values match and each owner returns its allowance.

## Initial quiet block

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
The four workers each warm for at least five seconds and supply seven
rotated batches with a 0.56-second target. Ranges use the four pairings of
process medians. Compilation includes compiled-owner disposal. Warm time
uses prepared code and includes applicable mediated RAM I/O, output and file
verification and previous-result disposal. Compile to result includes both.
Wider CPU includes reset and framing. Process startup is outside both clocks.

| Complete script | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | Compile | −1.95 to −0.46% | −1.78 to +0.10% | 1.11 / 0.41% |
| Sparse generated validator | Compile | −0.70 to +1.34% | −0.63 to +1.71% | 1.93 / 0.12% |
| Quote-heavy invoice | Warm | +0.45 to +1.54% | +0.00 to +2.94% | 0.23 / 0.86% |
| Configuration manifest | Compile | −3.52 to −2.60% | −3.54 to −1.19% | 0.82 / 0.12% |
| Structural classifier | Compile | −1.39 to +0.22% | −1.23 to +1.73% | 0.32 / 1.30% |
| Defaulted rules | Compile to result | −0.91 to +0.09% | −1.92 to −0.16% | 0.97 / 0.04% |

The manifest improves on both clocks. Defaulted-rule compilation takes
less elapsed time, and wider CPU through result falls. The invoice warm
increase motivates a longer repeat. Sparse validator and classifier ranges
span zero on both clocks.

Both quiet gates, all 168 batches, worker joins, fixture removal and
archive verification pass.

## Longer confirmation

The same images use a 0.87-second batch target. Three complete goldens pass
again for the reduced catalog. Each other measurement boundary is unchanged.

| Complete script | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy invoice | Warm | −0.28 to +0.99% | 0.00 to +0.32% | 0.53 / 0.75% |
| Configuration manifest | Compile | −0.87 to +3.31% | −1.29 to +2.58% | 1.24 / 2.94% |
| Defaulted rules | Compile to result | −5.29 to −0.77% | −5.59 to −0.92% | 4.76 / 0.01% |

The invoice's elapsed increase does not repeat consistently. The manifest
gain does not repeat either, with higher candidate worker spread. Wider
CPU through result falls for defaulted rules in both blocks. The reference
spread in the second block limits the precision of that estimate. Keep the
shared representation and its allocation savings. Revisit manifest or
invoice timing at their next affected checkpoint. Earlier defaulted-rule
timing debt belongs to different source pairs.

All 84 confirmation batches pass. Both quiet gates, worker joins and
fixture cleanup succeed and the archive verifies.

[Identity](identity.json) · [Tape](tape-summary.json) ·
[Allocation](compile-allocation-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Longer timings](confirmation/quiet-summary.json) ·
[Longer observations](confirmation/quiet-observations.tar.xz) ·
[Longer terminal receipt](confirmation/quiet-terminal-status.json) ·
[Longer procedures](confirmation/procedures.tar.xz).
