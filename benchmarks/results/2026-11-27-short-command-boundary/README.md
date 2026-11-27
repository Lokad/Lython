# B1827 Separate verification from short command timing

This diagnostic reuses the B1815 Lython core at `c10f06884`, stock CPython
3.13.16 and the same Azure F4as_v7 on Ubuntu 24.04.4 with .NET 11 RC3.
It ran on November 27, 2026 UTC. The early and late search share the
same source and 42,010-byte fixture size. The early case matches on
the first line and the late case on line 1,025. The configuration edit
uses the existing small fixture.

The original workers retain the complete command clock. The instrumented
workers keep that clock and the same per-invocation golden checks. They
also measure stdout, stderr and file verification separately. Removing
that interval leaves runtime and guest I/O, output capture, setup and
cleanup inside the original command boundary. Fixture reset and response
framing remain outside. A narrower invocation interval and the residual
are included in the raw summary. Startup and compilation sit outside all
warm intervals.

| Warm sync workload | Complete command gap, µs | Gap excluding verification, µs | Verification gap, µs |
| --- | ---: | ---: | ---: |
| File search, line 1 | 22.30–22.99 | 28.95–29.59 | −6.64–−6.16 |
| Configuration edit | 13.62–14.46 | 20.66–20.86 | −6.34–−6.14 |
| File search, line 1,025 | −56.11–−54.13 | −49.40–−47.53 | −6.70–−6.37 |

Gaps are Lython minus CPython. Each range spans all four pairs of worker
medians. The complete command column uses original workers and the split
columns use instrumented workers.

| Warm sync workload | Lython instrumented command change | CPython instrumented command change |
| --- | ---: | ---: |
| File search, line 1 | +0.23% to +1.10% | −0.82% to +1.23% |
| Configuration edit | −0.19% to +0.97% | −0.51% to +0.04% |
| File search, line 1,025 | −0.63% to −0.08% | −1.44% to +0.93% |

Each engine and mode uses two workers, three seconds of warmup per worker
and seven rotated batches per worker. Instrumentation changes the workers,
so the two modes supply independent controls. The phase ranges are
diagnostic and summarize per-worker medians independently.

First-line search measures 1.437–1.452× CPython with original workers and
2.005–2.042× before verification with the diagnostic workers. Lython spends
6.16–6.64 µs less on verification. The configuration edit shows the same
direction. Late search remains faster for Lython with either boundary.
Instrumentation shifts the complete command medians by at most 1.45% in
either direction across these workloads.

The first-line improvement opportunity is in the interval that includes guest
execution and its host, earlier result disposal, and output capture. B1816
makes file acquisition the next concrete target. The split answers how much
verification contributes and leaves the individual acquisition costs open.

The original and instrumented images each match all three independent
stdout and file goldens on Windows and on the VM. Both machine quiet gates
pass, all 168 timed batches verify and all workers join. The RAM-backed
fixtures are removed and the persistent archive verifies. The launch
reuses the previously verified frozen core and auxiliary files. The new
worker overlay and scripts account for 629,012 staged bytes. The first
launch stopped before staging on generated Python syntax and the corrected
command passed its local syntax check.

[Prior comparison](../2026-11-27-compiled-prefix-cpython/README.md) ·
[Prior CPU profile](../2026-11-27-current-short-script-profile/README.md) ·
[Phase medians](summary.json) · [Frozen images](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and verification](procedures.tar.xz).
