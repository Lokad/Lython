# B1808 Transfer generator rows across the yield boundary

The prototype on `855bade2b` lets a same-command generator
transfer evaluated list-display operands into an admitted starmap workspace
when an exact native callback can accept the complete row. The generator
leaves its active frame before the callback starts. Other yields and callbacks
keep the ordinary path. The exact source pair changes eight runtime files.

Both frozen 32-file images match four independent CPython output and file
goldens. The initial compile failed on both targets because the reservation
edit landed in a different iterator constructor. The corrected prototype
passes 689 public checks on each target, with 67 white-box checks on Windows
.NET 11 and 74 on Ubuntu .NET 10. Ubuntu ran seven additional stack-admission
cases. The final starmap suite passes all 18 tests on each target, including
the pending source and pending callback cases. CPython 3.13.2 matches the
source reentry and send output.

## Allocation and admission

The four complete scripts ran on Windows .NET 11. Each shipment script
processes 2,048 records. CLR allocation falls 19.68% for three-item rows
and 21.51–21.52% for five-item rows. Peak admitted bytes fall 14.04% and
21.46% respectively. Control peaks match and their CLR allocation differs
by at most 134 B. All allowances return to baseline after disposal.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Three-item shipment rows | sync | 1,391,736 → 1,117,816 | 3,467,109 → 2,980,453 |
| Five-item shipment rows | sync | 1,425,016 → 1,118,558 | 3,795,557 → 2,981,221 |
| Zip-to-starmap control | sync | 1,100,280 → 1,100,414 | 2,822,785 → 2,822,785 |
| CSV meter control | sync | 991,808 → 991,808 | 5,171,785 → 5,171,785 |
| Three-item shipment rows | async | 1,391,920 → 1,118,000 | 3,467,109 → 2,980,453 |
| Five-item shipment rows | async | 1,425,200 → 1,118,512 | 3,795,557 → 2,981,221 |
| Zip-to-starmap control | async | 1,100,470 → 1,100,592 | 2,822,785 → 2,822,785 |
| CSV meter control | async | 992,024 → 992,024 | 5,171,785 → 5,171,785 |

## Local timing

Ubuntu WSL runs two workers per source, with three seconds of warmup and
seven rotated timed batches per worker. Ranges span the four pairings of
worker medians. Positive changes mean more time. Warm elapsed includes
mediated RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and request work. Startup
and compilation stay outside the timed lanes. All 168 batches completed
with workers joined, fixtures removed and a verified persistent archive.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-item shipment rows | Warm async | +9.98% to +14.61% | +11.71% to +16.82% |
| Zip-to-starmap control | Warm async | +0.48% to +7.90% | −4.64% to +5.82% |
| Five-item shipment rows | Warm sync | +7.56% to +11.36% | +6.10% to +11.21% |
| Three-item shipment rows | Warm sync | +4.51% to +10.83% | +4.18% to +15.43% |
| CSV meter control | Warm sync | −0.95% to +9.94% | −2.08% to +7.33% |
| Three-item shipment rows | Warm async | +6.49% to +13.21% | +4.20% to +12.54% |

Keep the prototype private. The wide async process CPU increase of
11.71–16.82% exceeds either worker's same-source spread, which ranges
from 0.81% to 3.74%. Wide sync also uses more elapsed and CPU time.
The unchanged controls move and limit attribution. The implementation
removes the list allocation, while the new buffered pull, reusable-row
copy and generic callback entry add work. Their individual contributions
were unmeasured. Revisit this boundary only with a design that eliminates
an extra operand transfer or call entry while preserving suspension and
ordinary observable yields.

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Checks, catalog verification and exact patches](procedures.tar.xz).
