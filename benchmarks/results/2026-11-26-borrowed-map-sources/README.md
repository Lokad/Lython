# B1792 Borrow existing map sources

The prototype uses the executable loop's managed-iterator eligibility rule
when building map inputs. Existing managed iterators keep their shared
position. Protocol and CLR sources still acquire owned cursors. Ordered
pulls, managed stack checks and suspended work keep their existing owners.
The source patch and alias regression are in the procedures archive.

The shipment fixture creates 16 batches with four eligible sources per
batch. The Windows .NET 11 diagnostic measures 5,632 B less CLR allocation
per synchronous shipment run, from 1,093,480 B to 1,087,848 B. Admitted
peak stays at 2,817,644 B. The file-fed map saves 88 B and both controls
match allocation and admitted peak. The allowances return to baseline.
[Allocation and admission](allocation-summary.json).

Both 32-file images match four independent CPython goldens. Windows
.NET 11 and Ubuntu .NET 10 each pass 195 focused Release checks, covering
failed source acquisition, iterator aliases, suspended pulls, cancellation
and cleanup. CPython 3.13.2 reproduces the alias-test values and order.

## Timing

The 112-batch local diagnostic shows lower synchronous shipment elapsed
and CPU, with 7.57% elapsed spread across reference workers. Async shipment
spans zero in both metrics. File-fed map has 99.74% reference elapsed
spread. Elapsed rises 1.57–4.36% in the unchanged CSV meter, with mixed CPU.
The local summary and raw evidence are in the procedures archive.

The quiet repeat uses the same source pair based on `d370fef84` and the
same fixtures. It ran November 26, 2026 UTC on Azure F4as_v7, Ubuntu
24.04.4 and .NET 11 RC3. Two workers per source warm for five seconds
each, then run seven rotated batches targeting 440 ms. Ranges compare
all four pairings of worker medians. Negative values mean less time.
Elapsed covers applicable mediated RAM-backed I/O, output verification
and previous-result disposal. Process CPU includes fixture reset and
request framing. Compilation and startup sit outside these phases.

| Workload and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Shipment callback report, async | −1.52% to +5.53% | −0.43% to +5.21% |
| CSV meter control, sync | −0.65% to +1.47% | −2.22% to +0.65% |
| Shipment callback report, sync | −3.46% to −1.26% | −1.41% to +1.04% |
| File-fed map, async | −1.99% to +1.53% | −4.35% to +3.87% |

The quiet CSV control spans zero. Synchronous shipment takes less elapsed
time across both runs. All quiet CPU ranges include zero. Keep the
prototype out of production. For this change the allocation reduction
is small, and the async result remains uncertain while pulls gain another
dispatch choice. Revisit with a design that removes a shared pull or
operand handoff.

All 112 quiet batches and both machine checks pass. Workers join and
fixtures are removed before the archive finishes. Staging retired one
expanded B1791 observation directory after verifying each file against
the persistent archive and its pushed copy. Both original archives remain.

[Image identity](identity.json) · [Timing and worker spreads](summary.json) ·
[Quiet observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures, patches and local observations](procedures.tar.xz).
