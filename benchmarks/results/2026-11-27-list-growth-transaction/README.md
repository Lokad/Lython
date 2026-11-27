# B1833 Validate live list size during storage admission

A host callback can allocate after it publishes the pending estimate. The
list now checks its live count before copying and checks the count and
capacity again after an old backing or unused reservation is refunded. A
conflicting host change fails the pending operation without rolling back
the callback's effects. An already adequate backing is kept. If a nested
admission failed during a refund callback, its first failure stops the
pending outer mutation. Both fixed growth and snapshot copying reserve
their known estimates directly and remove the temporary lease allocation.

Four new regression cases fail at `c7ac5dbaf` and pass with the change.
They cover a stale snapshot estimate, stale growth copy, replacement of
capacity supplied by reentry and terminal refusal suppressed by a refund
callback. Both local Release targets pass 1,260 focused checks. Both
images match CPython 3.13.2 stdout and file goldens for the same two
4,096-record JSON Lines variants used in B1831. The nested input is
131,826 bytes and the flat input is 110,592 bytes.

## Allocation and admission

Windows .NET 11 estimates median CLR allocations from complete executions.
Execution, result handling and disposal return the allowance on every run.

| Workload | API | CLR bytes, reference → candidate | Peak admitted bytes, both |
| --- | --- | ---: | ---: |
| Nested-array Lines | sync | 2,387,862 → 2,256,616 | 6,214,167 |
| Flat Lines control | sync | 1,371,256 → 1,371,312 | 3,297,534 |
| Nested-array Lines | async | 2,388,078 → 2,257,062 | 6,214,167 |
| Flat Lines control | async | 1,371,478 → 1,371,472 | 3,297,534 |

## Local timing

Two frozen workers per source warm for five seconds on one logical Ubuntu WSL
.NET 11 CPU and contribute seven rotated batches. Ranges cover worker-median
pairs. Warm elapsed includes mediated I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and framing. Compilation
and startup sit outside the timed lanes. Only the core DLL changes.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested-array Lines | warm | −6.65% to +9.69% | −7.02% to +24.00% |
| Flat Lines control | warm-async | −4.67% to +0.78% | −2.90% to +0.82% |
| Nested-array Lines | warm-async | −7.00% to +6.60% | −1.34% to +3.99% |

Nested arrays allocate 5.49–5.50% less in both APIs at the same peak.
The results are consistent with removing one growth lease per record.
The flat control differs by at most 56 CLR bytes per execution. All elapsed
and CPU ranges span zero, and the reference sync CPU medians differ by
30%. The evidence establishes the allocation reduction. Whole-script CPU
and elapsed improvement remain unproven.

All 84 timed batches verify. Workers join, guest fixtures are removed and
the persistent archive verifies.

[Images](identity.json) · [Fixture census](field-census.json) ·
[Allocation](allocation-summary.json) · [Medians and spreads](summary.json) ·
[Raw observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Exact patches and checks](procedures.tar.xz).
