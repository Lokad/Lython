# B1323 Share one asynchronous CSV pull loop

The candidate removes the per-line delegated await introduced by B1322.
Ordinary and fixed-unpack iteration share one asynchronous record loop.
It returns an owned row or the field count written into existing operand
storage. The synchronous direct fill and the single field admission are
unchanged from B1322.

Compared `8c81fb8d2` with private `0c5cf6787` on November 22, 2026 UTC
on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two independent
processes per revision warmed for 16 seconds each and contributed seven
rotated batches. The four B1322 lanes run in reverse order. The ranges
cover all pairings of process medians. Negative changes favor the candidate.
Warm elapsed includes mediated RAM-backed I/O, goldens and result cleanup.
Process CPU covers the wider worker request. Both clocks exclude compilation,
startup and final teardown.

| Complete script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Plain integer invoice control | Warm async | +2.18 to +5.34% | −0.07 to +5.68% | 0.16 / 2.92% |
| Plain integer invoice control | Warm | +0.45 to +3.29% | −0.56 to +2.94% | 1.35 / 1.45% |
| CSV meter report with explicit float | Warm | −5.23 to −3.97% | −5.49 to −3.92% | 0.97 / 0.35% |
| Typed ASCII CSV meter report | Warm | −6.09 to −4.89% | −4.35 to −3.45% | 0.82 / 0.45% |

Keep this version private. Both affected meter scripts use less CPU,
with stable controls. The async invoice elapsed regression persists after
removing the extra await. Its CPU range spans zero with a 2.92% candidate
elapsed spread. The synchronous invoice also takes longer on all elapsed
pairings. The combined evidence leaves work shared with ordinary iteration
as a retention issue. A refinement should restrict the extra selection to
compiled fixed unpack and keep ordinary record acquisition on its original path.

Local CLR allocation drops 14.14–14.15% across the two typed reports and
10.49% on the explicit-float report. Each uses 589,824 fewer accounted bytes
at peak. Invoice allocation and peak match, and every allowance refunds to
baseline. Both images match the local CPython goldens.

Windows .NET 11 and Ubuntu .NET 10 each pass 120 white-box and 405
public focused Release checks. All 112 timed batches match. Workers join,
fixtures are removed, and the durable archive verifies under the lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Original B1322](../2026-11-22-csv-direct-slots/README.md).
