# B1349 Fuse regex range initialization and scalar projection

Measured `eed0f0b8b` against `12cecc6cf` on November 22, 2026 UTC.
ASCII matches write final byte and scalar ranges in one pass. Unicode
initializes byte ranges while staging scalar endpoints. Provider
validation, unmatched groups, original-subject offsets, cancellation
and admitted output and scratch remain in effect.

Both frozen 32-file images use the same worker and optional PythonRe
0.4.0 adapter on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11
RC3. Two processes per revision each warmed through eight seconds of
complete requests, then completed seven rotated batches. The ranges
span pairings of process medians. Warm elapsed includes mediated
RAM-backed I/O, verification and prior-result cleanup. Process CPU
covers the wider request. Startup and compilation are outside the
timed boundary.

| Workload | Warm elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| ASCII group text | −3.80 to −0.83% | −4.35 to −1.45% | 2.34 / 0.73% |
| Unicode group text | −2.35 to +0.42% | −1.69 to +1.54% | 1.19 / 1.62% |
| Unicode subgroup spans | −2.60 to +0.11% | −3.08 to +0.11% | 2.50 / 0.27% |
| Unicode wide registers | −0.84 to +1.09% | −2.26 to +2.05% | 1.12 / 0.82% |

Retain the combined pass. ASCII uses less warm CPU and elapsed in this
source pair. The Unicode timings span zero. Local managed allocation,
admitted peaks and allowance refunds match on all four workloads.

Windows .NET 11 and Ubuntu .NET 10 each pass 91 white-box and 608
public focused Release checks. Both frozen images match four CPython
goldens. Both machine gates pass during the original 221-second
collection. All 112 timed batches verify. The original service exited
with code 1 after sampling, worker joins, the exit gate and fixture
unmount. Its archiver expected five lanes from the previous collection.
The four complete records persisted on disk. A new exclusive lease
revalidated the worker images and archived the existing observations.
The original failure and recovery receipt are retained.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) ·
[Original service status](terminal-status.json) ·
[Recovery receipt](archive-receipt.json) ·
[Source and checks](checks-and-procedures.tar.xz).
