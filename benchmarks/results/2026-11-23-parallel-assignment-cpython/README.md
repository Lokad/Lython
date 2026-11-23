# B1518 CSV meters against CPython after direct assignment lowering

Retained Lython `fb3894153` and stock CPython 3.13.16 ran on a quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
Both 4,096-record reports consume 139,299-byte fixtures.

| Workload and phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Explicit-float, warm | 3.51957–3.70307 ms | 2.94642–2.97157 ms | 1.184–1.257× |
| Explicit-float, time to result | 3.71991–3.73078 ms | 3.18543–3.18743 ms | 1.167–1.171× |
| Explicit-float, compilation only | 0.04422–0.04533 ms | 0.07102–0.07147 ms | 0.619–0.638× |
| Typed ASCII, warm | 2.62617–2.64465 ms | 2.48422–2.53945 ms | 1.034–1.065× |

Ratios span the four pairings of two process medians per engine. The
explicit warm Lython workers differ by 5.21%. The explicit time-to-result
workers differ by 0.29% for Lython and 0.06% for CPython. Use the matched
[B1517 source pairs](../2026-11-23-parallel-assignment-operands/README.md)
for causal attribution.

Workers warm for eight seconds and contribute seven rotated batches.
Warm uses compiled code in a running process. Time to result includes
compilation and compiled-owner disposal. Both execution lanes include
applicable RAM-backed I/O, stdout and file verification, and prior-result
disposal. Compilation measures front-to-back compile and owned code
disposal. Startup, fixture reset and protocol framing sit outside the
elapsed clocks. The wider process CPU samples retain those request costs.

Both quiet gates pass. All 112 batches verify, workers join, guest
fixtures are removed and persistent evidence verifies under the lease.
The initial stage named an older reference as the donor. The full-image
hash check rejected it before starting workers. The corrected stage
reuses the exact B1517 refined image, with the original refusal preserved.

[Image](identity.json) · [Summary](summary.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
