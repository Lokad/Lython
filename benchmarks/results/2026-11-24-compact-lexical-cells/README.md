# B1563 compact grouped lexical capture storage

This refinement returns each function's capture storage to one
reference. A function backed by a group uses the immutable capture
map its code object already owns. Scalar publication and ordinary
operand transfers use their existing path. Forwarded-source scans
check cancellation before publishing any group.

| Warm script | Elapsed change | Process CPU change | CLR allocation change |
| --- | ---: | ---: | ---: |
| Captured rules | +2.21 to +4.50% | +2.55 to +5.29% | −13.18% |
| Default rules | +3.12 to +3.88% | +2.30 to +4.41% | +120 bytes |
| Annotated rules | +6.48 to +12.70% | +6.25 to +13.28% | +120 bytes |
| Quoted invoice | −0.83 to +3.10% | 0.00 to +4.65% | −616 bytes |

Captured rules allocate 196,608 fewer CLR bytes and use 16,384 more
peak admitted bytes. The two unaffected rule scripts recover their
old allocation sizes to within 120 bytes. Their CPU and elapsed
penalties remain. Replica spreads are all below 3%. The invoice
elapsed range crosses zero. All four allowances return to baseline.

Keep grouped storage private. The compact version removes the extra
pointer and improves allocation further, but the remaining overhead
still reaches ordinary rule execution. The next investigation should
attribute CPU to complete call and frame costs and identify work it
can displace. Group layout merits reconsideration if that redesign
also removes the generic dispatch or ownership work implicated here.

The frozen reference `5d54f7afb` matches runtime and tests in
`566259ebc`. Only the core DLL differs in the candidate `429e300fd`'s
verified 32-file image. On November 24, 2026 UTC, the quiet Azure
F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source
warm for eight seconds and contribute seven rotated samples. Ranges
span all pairings of their medians. Warm elapsed includes guest work,
mediated RAM-file I/O where applicable, prior-result disposal and
verification. Wider process CPU includes reset and request overhead.
Compilation, startup and final teardown are outside warm elapsed.
All 112 timed batches match CPython 3.13.16 goldens. Both quiet gates,
worker joins, fixture cleanup and persistent verification passed.

Windows and Ubuntu each pass 151 white-box and 784 public focused
.NET 11 Release checks. They exercise sibling captures, forwarded
nonlocal rebinding, held I/O and cancellation, exception and class
cells, generators, and shared-budget lifetime.

[Timing](summary.json) · [Allocations](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and exact patch](procedures.tar.xz) · [Terminal receipt](terminal-status.json) ·
[First grouped-storage trial](../2026-11-24-grouped-lexical-cells/README.md).
