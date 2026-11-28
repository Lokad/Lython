# B1970 Compare short fresh-process commands with CPython

Lython `83042273c` and stock CPython 3.13.16 ran November 28, 2026 UTC
on an Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The same exact
32-file image supplies the warm B1968 comparison. Both goldens verify
locally and on the VM. Seven runs of the existing fresh comparison
command each launch a Lython and a CPython process for both cases.

| Workload | Lython median | CPython median | Ratio of medians | Paired ratio range |
| --- | ---: | ---: | ---: | ---: |
| First-line file search | 235.08 ms | 32.76 ms | 7.18× | 7.09–7.30× |
| Configuration edit | 251.07 ms | 32.06 ms | 7.83× | 7.62–7.94× |

The parent measures process launch through exit and pipe drain. The
clock includes runtime and worker initialization, compilation, one
execution, applicable I/O and cleanup. Decoded-reply validation is
outside the clock. OS caches stay warm. Lython launches first in each
pair. The measurement describes a process-level command using these
workers. B1906 measured configuration at 259.0 ms on `a159f24f2`.
The separate revision endpoints leave the cause of that difference open.
[B1906](../2026-11-27-current-cpython-a159f24/README.md) ·
[Warm and compile comparisons](../2026-11-28-short-frontend-cpython/README.md).

Both quiet checks and all 28 launches verify. All children joined and
their RAM fixtures were removed. The archive verifies under the lease.
Staging reused all 32 verified donor files.

[Launch timings](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
