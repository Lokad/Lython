# B1156 postponed factories against CPython

Two factory scripts run with `from __future__ import annotations`.
The ordinary script performs 2,048 definitions. The nested script performs
4,096 definitions. Both use 2,048 inputs and produce the same checked
outputs as their eager counterparts.

Lython `551fde44e` uses the exact
[B1155 core](../2026-11-20-compiled-postponed-annotations/measured-core.dll.gz),
built from `a6928c942`. The complete 32-file image and both catalog
cases were verified before staging. The comparison ran on the quiet
Azure F4as_v7 with Ubuntu 24.04.4, .NET 11 RC3 and stock CPython 3.13.16
on November 20, 2026 UTC. There are two workers per engine.

Warm includes execution, mediated I/O where used, output validation and
previous-result cleanup. Compilation includes compiled-owner disposal.
Compile-to-result adds compilation to one complete invocation, including
owner cleanup. Fixture reset, process startup and final teardown are
outside the guest clock. Process CPU over the wider worker boundary
is in the summary. Ratios divide Lython by CPython elapsed and span
all four combinations of process medians.

| Workload | Phase | Lython | CPython | Ratio | Lython / CPython control spread |
| --- | --- | ---: | ---: | ---: | ---: |
| Postponed annotated rules | Warm | 1.486–1.569 ms | 0.406–0.407 ms | 3.65–3.86× | 5.61 / 0.28% |
| Postponed annotated rules | Compile-to-result | 1.531–1.599 ms | 0.429–0.436 ms | 3.51–3.73× | 4.44 / 1.68% |
| Postponed annotated rules | Compile | 0.02514–0.02533 ms | 0.03518–0.03539 ms | 0.71–0.72× | 0.77 / 0.58% |
| Postponed nested factories | Warm | 2.051–2.117 ms | 0.577–0.590 ms | 3.47–3.67× | 3.22 / 2.39% |
| Postponed nested factories | Compile-to-result | 2.103–2.146 ms | 0.624–0.637 ms | 3.30–3.44× | 2.02 / 2.20% |
| Postponed nested factories | Compile | 0.03018–0.03026 ms | 0.04285–0.04312 ms | 0.70–0.71× | 0.27 / 0.62% |

These diagnostic ratios show a substantial execution gap despite faster
compilation. The ordinary warm gap size is uncertain with 5.61% spread
across Lython's duplicate workers. The eager workloads remain documented at
their separate checkpoint.

All 120 batches passed. The VM passed both quiet gates, joined all workers
and verified the persistent archive under the lease after fixture cleanup.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
