# B1712a Isolate dictionary layout from prepared readers

The private source retains the parser's final value array, packed short fields
and the shared dictionary value layout from B1712. Ordinary executable reads
publish fields. This removes the new immediate-consumer checks from both
drivers. The production reference is the same source `6934aafb9`.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Each source uses two process medians from seven timed batches after warmup.
Ranges cover all four candidate/reference pairings. Warm elapsed includes
applicable mediated RAM-backed reads and writes, verification and result
disposal. Wider CPU also covers fixture reset and request framing.

| Workload / warm phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Nonlocal text report | −10.27 to +8.92% | −9.31 to +8.27% | 10.84 / 9.51% |
| Quoted invoice | −1.94 to +1.96% | −2.15 to +0.50% | 2.49 / 1.46% |
| File-fed map, async | −4.95 to +2.85% | −3.17 to +4.16% | 2.16 / 5.91% |

The invoice regression from the full prototype does not recur in this
collection. File-fed timing no longer shows a consistent improvement.
The text observations remain inconclusive and cannot assign the earlier
regression to either part of the design. The comparisons use the same
reference source in separate collections.

The layout and unused fields account for a 29.63% local sync allocation
reduction in file-fed map. Shipment allocates 3.74% less. Quoted invoice
allocates 0.75% more. Its packed numeric fields now construct strings before
conversion. Admission peaks match B1712, including result cleanup restoring
the allowance. Retain the source privately and test prepared byte consumers
that avoid new resolution work on unrelated instructions.

Both complete frozen images verify all six independent CPython goldens.
The isolation passes 73 focused Windows .NET 11 Release tests, including
the packed-field alias fixture. All 84 timed batches and both quiet checks
pass. Workers join, fixtures are removed, and the persistent archive verifies.

[Parent comparison](../README.md) · [Source identity](identity.json) ·
[Timing](quiet-summary.json) · [Allocation](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz).
