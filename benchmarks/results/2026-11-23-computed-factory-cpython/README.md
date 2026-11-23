# B1503 CPython checkpoint after direct computed definitions

Lython `b32bf6b6b` and stock CPython 3.13.16 ran on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
The measured `8b79aef66` image has the same production source. The
complete 32-file image, worker protocol and CPython executable verify.

Ratios divide Lython elapsed by CPython elapsed. Ranges pair the medians
of two independent workers per engine. Each worker warms for eight
seconds and supplies seven rotated batches. Warm time includes applicable
RAM-backed I/O, golden verification and disposal of the prior result.
Lython uses an admitted, mediated host. Compile to result adds compilation
and code-owner disposal in a running process. Wider process CPU includes
fixture resets and protocol. Startup and final teardown are outside elapsed.

| Workload | Phase | Lython | CPython | Lython / CPython | Wider CPU ratio | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Eager nested factories | Warm | 0.75279–0.79514 ms | 0.56739–0.57242 ms | 1.315–1.401× | 1.331–1.398× | 5.63 / 0.89% |
| Postponed nested factories | Warm | 0.71381–0.79271 ms | 0.53835–0.54989 ms | 1.298–1.472× | 1.327–1.516× | 11.05 / 2.14% |
| Fully supplied helper | Warm | 0.31858–0.31959 ms | 0.19821–0.19901 ms | 1.601–1.612× | 1.595–1.624× | 0.32 / 0.41% |
| Quote-heavy CSV invoice | Warm | 7.29052–7.38776 ms | 6.52282–6.56641 ms | 1.110–1.133× | 1.088–1.167× | 1.33 / 0.67% |
| Eager nested factories | Compile to result | 0.80314–0.84580 ms | 0.60904–0.61961 ms | 1.296–1.389× | 1.302–1.388× | 5.31 / 1.74% |
| Postponed nested factories | Compile to result | 0.77801–0.80157 ms | 0.61664–0.62162 ms | 1.252–1.300× | 1.267–1.318× | 3.03 / 0.81% |

Both factory scripts take longer than CPython through first use. The
postponed warm magnitude remains uncertain because the Lython workers
differ by 11.05%. The fully supplied control reproduces the earlier gap
with low worker spread. Absolute quoted-invoice time rises on both
engines since B1499. Use B1500 and B1502 to assess the Lython changes in
matched source pairs. Compilation-only and fresh-process observations
remain at their earlier measured revisions.

Four separate workload goldens and all 168 timed batches verify in 273
seconds. Both quiet gates pass, workers join, guest fixtures are removed
and the persistent raw archive verifies under the exclusive lease.

[Timing](summary.json) · [Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[B1500](../2026-11-23-direct-computed-definition/README.md) ·
[B1502](../2026-11-23-eager-isolated-definition/README.md) ·
[Earlier CPython checkpoint](../2026-11-23-closure-cpython/README.md).
