# B1477 Use a fixed reservation for final executable tuples

Retained `a94f78fc7` is source-equivalent to frozen `f5852aa99`.
Construction admits its final backing, builds a private tuple and
commits the same charge. Cancellation or failure returns exactly
that reservation. The least complicated owner for this synchronous
construction is its own call. The temporary reservation object is
removed. Tuple layout and existing admission estimates are preserved.

Compared with `123b2c894`, the local allocation comparison shows
8.16% less allocation in the typed CSV meter and 9.31% less in the
explicit-float meter. Admitted peaks stay equal across all five
workloads. The budget returns to baseline after every run.

| Workload | Local managed allocation before → after | Admitted peak |
| --- | ---: | ---: |
| Typed ASCII CSV | 1,606,512 → 1,475,344 B | 3,552,905 B |
| Explicit-float CSV | 2,818,998 → 2,556,584 B | 6,285,897 B |
| Explicit-float CSV with every tuple element observed | 2,818,598 → 2,556,974 B | 6,286,305 B |
| Configuration manifest | 99,488 → 99,712 B | 56,623 B |
| Defaults observer | 1,111,664 → 1,111,664 B | 2,903,281 B |

## Quiet comparison

The dedicated Ubuntu 24.04.4 Azure F4as_v7 used .NET 11 RC3 on
November 23, 2026 UTC. Two independent processes per image warmed
for eight seconds and supplied seven rotated batches each. Warm
elapsed includes applicable mediated RAM-file I/O, result verification
and prior-result disposal. Wider process CPU includes reset and
transport. Compilation, startup and final teardown are separate.

| Complete warm script | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Typed ASCII CSV | −3.20% to +5.83% | −3.78% to +5.15% |
| Explicit-float CSV | −2.56% to +2.03% | −3.00% to +1.05% |
| Explicit-float CSV with every tuple element observed | −0.92% to +1.55% | 0.00% to +2.63% |
| Configuration manifest | −0.93% to +2.23% | +0.06% to +2.38% |
| Defaults observer | +0.44% to +1.55% | −0.89% to +0.82% |

Both CSV elapsed ranges cross zero. Candidate elapsed spread is
7.99% for typed CSV and 4.53% for explicit-float CSV. The tuple observer
and manifest CPU ranges and the defaults observer's elapsed result
remain visible. Retain the simpler owner and allocation reduction.

Both 32-file images match five CPython fixture goldens. Windows .NET 11
and Ubuntu .NET 10 each pass 449 white-box and 1,842 public Release
tests. The refusal and cancellation regression now covers float
tuples as well as integer tuples and keeps an independent reservation
funded. All 140 batches, quiet checks, worker joins, temporary fixture
cleanup and archive verification pass. The initial staging workspace
gate refused before creating the run directory. The corrected 120 MiB
workspace margin leaves the images, workloads and sampling gates intact.

[Source and images](identity.json) · [Allocations](allocation-summary-six.json) ·
[Timing](summary.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and setup](procedures.tar.xz).
