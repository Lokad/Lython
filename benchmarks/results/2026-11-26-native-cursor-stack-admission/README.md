# B1743 Check stack availability when advancing native iterators

A native iterator can advance another cursor before reaching a guest call.
The cursor now checks Lython's existing interpreter depth and the managed
stack on the advancing thread. A deferred iterator acquisition checks again
after it resumes. The bounded regression verifies that a pull refused at the
existing depth limit leaves its source untouched and its allowance funded.
The same cursor advances after the depth is restored. Delayed-host checks
also cover cancellation during fixed unpack and strict exhaustion.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment callback report | Async | −1.22% to +1.11% | −2.04% to −0.53% |
| File-fed CSV quantity map | Async | −3.18% to +0.11% | −3.54% to +0.85% |
| Unicode text report | Sync | +3.37% to +6.11% | +3.18% to +4.83% |

The November 26, 2026 UTC comparison pairs `1aefc2976` with the archived
patch on the Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Ranges span
the pairings of two process medians per build. Each worker warms for seven
seconds and provides seven rotated batches. Elapsed includes mediated
RAM-backed I/O, output verification and result cleanup. Wider CPU includes
request framing and fixture reset. The text workload advances a native file
iterator and exercises the new check.

Shipment and file-fed elapsed ranges span zero. The text result shows a
measurable cost in this block, with same-source CPU spread below 1.4% on
both images. Keep the check for correctness and carry the text cost into the
next ordinary file-processing investigation. Local synchronous allocations
match on shipment and file-fed map. Text moves by 6 B per invocation. Peak
shared admission matches on all three and allowance returns to baseline.

Windows .NET 11 and Ubuntu .NET 10 each passed 1,512 public and 553
internal focused Release checks. Both exact 32-file images passed three
independent CPython output and file goldens. All 84 batches, both machine
checks, worker joins, fixture cleanup and persistent archive verification passed.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and patch](procedures.tar.xz).
