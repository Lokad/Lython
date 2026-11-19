# B1116 borrowed return transport isolation

Private `ea04b7212` removes the borrowed fixed-return destination and its
call plumbing from B1115 `cabe0e295`. Lexical leaf execution and pending
frame transfer stay enabled. Borrowed returns now construct ordinary
tuples, while executable frames and the native path keep the existing
fixed-unpack forwarding. This source pair isolates the borrowed result
path from the changes to call entry and child transfer.

Both complete 32-file images passed all five CPython workload goldens.
Three warm lanes ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on November 19, 2026 UTC. Four workers per lane warmed at least
14 seconds each. Changes below span the four pairings of duplicate-process
medians, with negative values indicating lower time or allocation.

| Workload | Warm elapsed change | Process CPU change | Local warm allocation change |
| --- | ---: | ---: | ---: |
| Annotated rules | −1.33…+6.61% | −3.97…+5.34% | 0.00% |
| Captured rules | +4.32…+8.69% | +4.66…+9.57% | +10.22% |
| Retained nonlocal siblings | −0.30…+3.78% | +0.82…+3.07% | 0.00% |

Annotated elapsed and CPU span zero. Duplicate-process elapsed spreads
of 3.75% and 4.15% limit the conclusion. Captured-rule CPU moves in the
same direction as elapsed and allocations. The extra tuple increases
captured allocation from 2,404,064 to 2,649,824 bytes. Ordinary rules were
checked for allocation only and return to 2,388,104 bytes. Accounted peaks
and allowance refunds stayed the same in all five allocation checks. The
refinement passed 44 Windows .NET 11 Release checks covering aliases,
unpack, resumption at host I/O and cancellation.

Keep both prototypes private. The borrowed destination lowers captured-rule
time and allocation in this comparison. The original annotated regression
remains the acceptance control. Isolate the work moved out of common
bound-call entry before another whole-script comparison with the retained
runtime.

The first local preflight caught mismatched generated job counts and
stopped before staging. The corrected preflight verified both images.
Staging retired a B1097 replica with committed core and observation hashes;
the disk headroom floor was preserved. All 60 timed batches completed in
212.94 seconds. Both quiet gates passed, workers joined, fixtures were
removed and the archive was verified under the lease.

[Original prototype](../2026-11-19-borrowed-lexical-leaf/README.md) ·
[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source and checks](checks-and-procedures.tar.xz).
