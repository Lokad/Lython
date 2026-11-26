# B1799 Carry a completed short map result directly

The candidate on `526a83ef4` passes a ready short map row as a
`PyIterationResult` at the nested-map and `sum` boundaries. Both reuse the
async producer's short-row preparation. Pending sources and
callbacks keep the existing continuations and own the evaluated prefix.
The cursor still checks execution and managed stack admission before
entering the iterator. Wide calls retain their original packet.

Both 32-file images match three independent CPython goldens. Windows
.NET 11 and Ubuntu .NET 10 each pass 810 public and 180 white-box focused
Release checks. The nested map regression suspends in a source or a
callback and checks alias identity, cancellation, closed files and
budget recovery. Both cases also pass on the production runtime in
Windows .NET 11 and Ubuntu .NET 10. The completed scripts match CPython
3.13.2. The initial private snapshot omitted benchmark worker sources referenced by
the test project. The archive keeps those build errors and the completed
reruns. Windows synchronous allocation, peak admission and retained
compiler allowance match on all three workloads.

## Local diagnostic

Ubuntu WSL ran both APIs for the shipment and file-fed map plus an
unchanged file-search control. The five lanes complete all 140 batches.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment callbacks | Async | +0.02% to +26.47% | +4.77% to +21.94% |
| File-fed map | Sync | −0.72% to +15.80% | +0.38% to +12.28% |
| Protected file-search control | Sync | −8.31% to −2.32% | −10.61% to +3.65% |
| Shipment callbacks | Sync | −6.11% to +4.87% | −7.40% to +1.44% |
| File-fed map | Async | −0.45% to +5.95% | +1.63% to +7.34% |

## Quiet confirmation

The same images ran on Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3
on November 26, 2026 UTC. The two affected async lanes repeat after the
unchanged control. Both warm comparisons use two workers per source
image and seven rotated batches per worker. Ranges span all pairings of
worker medians. Negative values mean less time. Elapsed includes
mediated I/O, output verification and previous-result disposal. Process
CPU also includes fixture reset and request framing. Startup and
compilation sit outside these clocks.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Protected file-search control | Sync | −3.18% to +0.76% | −2.15% to +0.77% |
| File-fed map | Async | −2.97% to −0.62% | −2.34% to −0.90% |
| Shipment callbacks | Async | +0.83% to +12.62% | +1.81% to +11.79% |

Keep the combined candidate out of production. The quiet file-fed map
uses 0.90–2.34% less CPU with tight worker spreads, reversing the local
CPU increase. Quiet shipment uses 1.81–11.79% more CPU, with 6.33% CPU
spread across reference workers. The unchanged quiet file-search CPU
spans zero. Both quiet shipment medians exceed both reference medians,
and its local CPU is also adverse. The result leaves an aggregate-only
route as a separate hypothesis. The synchronous allocation diagnostic
matches across all cases.

All 84 quiet batches and both machine checks pass. All workers join,
guest fixtures are removed, and both archives verify. Staging released
one duplicate B1796 DLL after verifying the full local image, a separate
compressed DLL backup and the remote archive against its pushed copy.

[Local timing](local/summary.json) · [Local observations](local/observations.tar.xz) ·
[Local image](local/identity.json) · [Local receipt](local/archive-receipt.json).

[Quiet timing](quiet/summary.json) · [Quiet observations](quiet/observations.tar.xz) ·
[Quiet image](quiet/identity.json) · [Quiet terminal status](quiet/terminal-status.json).

[Allocation and admission](allocation-summary.json) ·
[Procedures and exact patches](procedures.tar.xz).
