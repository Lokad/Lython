# B1804 Fill exact starmap rows into native callback slots

The prototype on `089cb9d60` fills the native
callback's existing operands directly from an exact list or tuple, including
its bound receiver. A complete positional supply can omit the temporary
call packet and the subsequent copy. The existing continuation owns live
slots if the callback suspends. Dynamic binders and larger workspaces
keep the funded argument packet.

Both frozen 32-file images match four independent CPython output and
file goldens. Windows .NET 11 and Ubuntu .NET 10 each pass 397 public
and 33 white-box focused Release checks. The regression preserves aliases
for wide lists and tuples, real asynchronous cancellation and cleanup, and
shared budget recovery. CPython 3.13.2 independently reproduces completed
regression outputs.

## Allocation and admission

The four complete scripts ran on Windows .NET 11. The five-item script
has 2,048 records. Both modes allocate about 20.2% less in this script
and use 188 B less peak admission. Control peaks match. All allowances
return to baseline after the result owner is disposed.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Three-item shipment rows | sync | 1,391,736 → 1,391,736 | 3,467,109 → 3,467,109 |
| Five-item shipment rows | sync | 1,785,694 → 1,425,016 | 3,795,745 → 3,795,557 |
| Zip-to-starmap control | sync | 1,100,336 → 1,100,280 | 2,822,785 → 2,822,785 |
| CSV meter control | sync | 991,878 → 991,864 | 5,171,785 → 5,171,785 |
| Three-item shipment rows | async | 1,391,920 → 1,391,920 | 3,467,109 → 3,467,109 |
| Five-item shipment rows | async | 1,785,704 → 1,425,200 | 3,795,745 → 3,795,557 |
| Zip-to-starmap control | async | 1,100,464 → 1,100,464 | 2,822,785 → 2,822,785 |
| CSV meter control | async | 992,030 → 992,024 | 5,171,785 → 5,171,785 |

## Local timing

Ubuntu WSL runs two workers per source, with three seconds of warmup
and seven rotated timed batches per worker. Ranges span the four
pairings of worker medians. Negative changes mean less time.
Warm elapsed includes mediated RAM-backed I/O, output and file
verification, and previous-result disposal. Process CPU also includes
fixture reset and request work. Startup and compilation stay outside
the timed lanes. All 112 batches completed with workers joined,
fixtures removed and a verified persistent archive.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-item shipment rows | Warm async | −13.67% to +4.74% | −15.69% to +5.60% |
| Zip-to-starmap control | Warm async | −5.95% to +1.10% | −6.89% to −3.57% |
| Five-item shipment rows | Warm sync | +2.29% to +6.70% | +1.05% to +5.08% |
| Three-item shipment rows | Warm sync | −3.75% to +13.25% | −3.07% to +11.81% |

The initial decision kept the prototype private. The five-item synchronous script takes
2.29–6.70% more elapsed time and 1.05–5.08% more CPU across the worker
pairs. Async timing crosses zero, with same-source CPU spreads above
11% in both images. The zip control moves favorably in CPU and the
three-item control has wide candidate spread. The comparison verifies
substantial allocation savings and leaves their whole-script CPU tradeoff
open. The same source pair and controls are available for a bounded
confirmation on the quiet VM. [Quiet confirmation and retention decision](quiet/README.md).

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Checks, catalog verification and exact patches](procedures.tar.xz).
