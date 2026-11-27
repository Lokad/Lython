# B1817 One argument accumulator for iterable starmap rows

The trial reuses the existing expanded-call accumulator when `starmap`
receives an arbitrary iterable as a row. Both APIs drain directly into the
callback argument buffer. Span-aware binders consume its live prefix and
other callables receive an exact array that remains funded through the
callback. The async list drain and sync-specific growth code go away.
Ordinary exact-list and zip inputs retain their established routes.

Two 1,024-row CSV scripts differ in how each set of fields reaches the
callback. One supplies `map(int, ...)` over the parsed fields and the
other uses a generator. Both frozen images match the two independent
CPython output and file goldens and the exact-list shipment control.
Windows .NET 11 and Ubuntu .NET 10 each pass 621 focused Release checks.
The pending-row regression verifies a retained alias, nested file close,
cancellation during the callback and a return to baseline admission.
The initial build used the concrete cursor return type, which made the
existing `ConfigureAwait` extension ambiguous. The corrected source keeps
the original interface. The initial fixture schema error is archived too.

## Allocation and timing

Windows .NET 11 measures CLR allocation and peak admitted bytes. All
shared allowances return to baseline after disposal.

| Workload | API | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Mapped CSV fields | sync | 2,300,126 → 2,267,358 | 1,863,254 → 1,863,254 |
| Yielded CSV fields | sync | 2,021,416 → 1,988,648 | 2,354,774 → 2,354,774 |
| Exact-list shipment control | sync | 1,358,936 → 1,359,110 | 3,795,557 → 3,795,557 |
| Mapped CSV fields | async | 2,169,104 → 2,267,344 | 1,863,254 → 1,863,254 |
| Yielded CSV fields | async | 1,890,734 → 1,988,808 | 2,354,774 → 2,354,774 |
| Exact-list shipment control | async | 1,359,120 → 1,359,120 | 3,795,557 → 3,795,557 |

Ubuntu WSL uses two workers per source, three seconds of warmup and seven
rotated batches per worker. The intervals span all worker median pairings.
Negative values mean less time. Elapsed includes RAM-backed file I/O,
output verification and prior-result disposal. The wider process clock
includes fixture reset and framing. Compilation and startup stay outside.

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Mapped CSV fields | async | −3.14% to +9.87% | −6.03% to +6.05% |
| Exact-list shipment control | async | +2.04% to +51.34% | −1.54% to +45.27% |
| Yielded CSV fields | async | −20.61% to +31.45% | −18.27% to +30.43% |
| Mapped CSV fields | sync | −7.50% to +3.44% | −4.70% to +1.93% |
| Yielded CSV fields | sync | −5.11% to +4.93% | −6.06% to +5.91% |

Keep this implementation private. Both affected scripts allocate 32,768 B
less in sync execution. The async scripts allocate 4.53% and 5.19% more,
respectively, and all peak admission numbers match. All four affected
elapsed and process CPU intervals span zero. The exact-list control has
wide worker spread, which limits timing attribution. Removing the async
list and the sync exact copy was insufficient with the existing expanded
accumulator's shell and storage. A later design needs a cheaper single
owner, independent of which binder consumes the row.

The new pending-row regression remains useful on the current runtime.
Both its completion and cancellation cases also pass after restoring
the delivered source on Windows .NET 11 and Ubuntu .NET 10.

[Allocations](allocation-summary.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).
