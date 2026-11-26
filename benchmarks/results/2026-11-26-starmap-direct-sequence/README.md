# B1795 Fill starmap call storage directly from exact sequences

Exact list and tuple rows use the short callback operands or one admitted
wide call packet. The general iterator path stays available for custom
iteration. This removes the cursor and drained-list intermediates for
exact rows. After wide-packet admission, the native copy confirms the live
length before reading any element. A changed list uses its ordinary
iteration semantics. A pending callback keeps the packet funded through
completion and cleanup. Three-item tuple reads preserve the existing loans
for deferred scalar identities.

The same 2,048-record shipment shapes compare against production at
`a0df0ac3f`. The [capacity-hint trial](../2026-11-26-starmap-row-capacity/README.md)
and its slower wide async confirmation remain available.

## Allocation and admission

Medians below use `RunAsync` on Windows .NET 11 with the cooperating
comparison host, applicable I/O, output checks and result disposal.

| Workload | Production | Capacity hint | Direct |
| --- | ---: | ---: | ---: |
| Three-item list rows | 2,129,430 B | 1,572,832 B | 1,391,920 B |
| Five-item list rows | 2,293,782 B | 2,129,886 B | 1,785,704 B |
| Existing zip-to-starmap control | 1,100,520 B | 1,100,464 B | 1,100,464 B |
| CSV meter control | 992,024 B | 992,024 B | 992,080 B |

Three-item rows allocate 34.63% less than production and five-item rows
allocate 22.15% less. Synchronous allocation falls by 35.13% and 34.35%.
Async peak admission falls 148 B and 40 B, respectively. Sync admitted
peaks fall by 164 B and 120 B. Allowances return to baseline.
[Async allocation](async-allocation-summary.json) · [Sync allocation](allocation-summary.json).

## Quiet comparison

The two 32-file images ran November 26, 2026 UTC on the dedicated
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source
warm for five seconds and supply seven rotated batches targeting 440 ms.
Ranges span all four pairs of worker medians. Negative values indicate
less time. Elapsed includes mediated RAM-backed I/O, result and file
checks and previous-result cleanup. Process CPU also covers fixture
reset and request framing.

| Complete script | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-item list rows | Async | −12.31% to −7.45% | −10.78% to −7.83% |
| Existing zip-to-starmap control | Async | −5.33% to −0.76% | −3.72% to −0.46% |
| Three-item list rows | Sync | −14.13% to −11.13% | −12.94% to −9.39% |
| Five-item list rows | Async | −10.67% to −5.16% | −10.18% to −5.77% |

Keep the direct path. Three-item sync and async elapsed drop
11.13–14.13% and 7.45–12.31%. For five-item async rows, elapsed drops
5.16–10.67% and CPU drops 5.77–10.18%. The existing zip control also
runs faster, limiting attribution of the exact size. Both list shapes
show larger CPU reductions than the control across the paired medians.
The source removes the separate cursor and drain for exact rows.


Windows .NET 11 and Ubuntu .NET 10 each pass 290 focused Release checks.
The new assertions exercise list growth and shrinkage during admission,
refusal before callback effects, ordered aliases, and packet ownership
through cancellation and async cleanup. Both frozen images match four
independent CPython goldens. All 112 timed batches and both quiet checks
pass. Workers join, fixtures are removed and the persistent archive
verifies. Staging retired a verified duplicate VM DLL with its full
image available locally and all three dependent archives preserved.

[Image identity](identity.json) · [Timing and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and exact patch](procedures.tar.xz).
