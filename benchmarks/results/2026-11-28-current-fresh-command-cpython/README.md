# B1992 Refresh fresh-process comparisons with CPython

Lython `04159957a` and stock CPython 3.13.16 ran on November 28, 2026 UTC
on an Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. This is the exact
32-file B1990 worker image with on-demand builtin constructor descriptors.
Both workload goldens verify locally and on the VM. Seven passes of the
existing fresh comparison launch both processes once for each case per pass.

| Workload | Input | Lython median | CPython median | Ratio of medians | Paired ratio range |
| --- | --- | ---: | ---: | ---: | ---: |
| First-line file search | 42,010 B | 237.20 ms | 32.75 ms | 7.24× | 7.10–7.47× |
| Configuration edit | 181 B | 251.91 ms | 32.15 ms | 7.83× | 7.70–7.93× |

The clock spans process launch through exit and pipe drain. It includes
runtime and worker initialization, compilation, execution, applicable I/O
and cleanup. OS caches stay warm, with Lython first in each pair. The
supervisor verifies the decoded reply after the timer. The ratio divides
Lython elapsed by CPython elapsed. The earlier B1970 measurements at
`83042273c` were 7.18× and 7.83×. This endpoint comparison supports the
same overall startup gap. B1990's matched fresh comparison spans zero.
[B1970](../2026-11-28-fresh-command-cpython/README.md) ·
[B1990](../2026-11-28-owner-local-type-new-slots/README.md).

Both quiet checks and all 28 launches verify. The children joined and the
RAM-backed fixtures were removed before the archive was verified. Staging
reused all 32 donor files. Earlier starts stopped before sampling because
of an unused reference role, a missing comparison alias, and a service
name still held by the failed run. The final service used the verified
staged image and catalogs.

[Launch timings](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
