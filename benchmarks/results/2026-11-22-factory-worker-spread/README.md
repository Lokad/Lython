# B1414 Warm factory spread on one exact runtime image

Four independent workers ran the postponed nested factory on November 22,
2026 UTC on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
All workers used the 32-file Lython `55424591f` image from
[B1406](../2026-11-22-current-source-cpython/README.md). The original
CPython golden verified. Managed JIT maps were enabled for diagnostic
attribution. This run supplies no new CPython ratio.

| Warmup per worker | Warm elapsed medians | Wider process CPU medians | Elapsed / CPU spread |
| --- | ---: | ---: | ---: |
| 8 seconds of full requests | 1.106–1.229 ms | 1.117–1.237 ms | 11.14 / 10.74% |
| 24 seconds of full requests | 1.113–1.236 ms | 1.113–1.245 ms | 11.06 / 11.83% |

Seven rotating batches per worker follow each warmup checkpoint.
Warm elapsed includes golden verification and prior-result cleanup.
Process CPU includes request handling and reset. Compilation, process
startup and final teardown are outside the elapsed clock. The same
worker remains slow at both checkpoints and after CPU sampling.

The fastest and slowest workers then run approximately six seconds of
verified requests while `perf` samples all threads at 199 Hz. The active
Lython execution thread accounts for about 98% of the measured process
CPU in each trace. Both run optimized Tier 1 code. Sample addresses map
to different emitted lengths for several of the same methods.

| Method actually sampled | Faster worker code bytes | Slower worker code bytes |
| --- | ---: | ---: |
| `CreateFunctionValue` | 1,304 | 1,420 |
| `TryExecuteNativeCall` | 3,333 | 3,329 |
| `NativeExecution.RunCore` | 6,679 | 6,660 |
| `BindFunctionArgumentsInto` | 2,790 | 2,800 |
| `StartNativeCall` | 749 | 749 |

The faster trace contains 1,192 samples over 5,390 invocations. The
slower one contains 1,185 over 4,820. Nearest call samples per thousand
invocations rise from 17.81 to 22.41, callable construction from 30.61
to 35.27 and ownership from 18.18 to 20.54. The binder itself remains
close at 6.68 and 6.43. These samples show distributed work and cannot
assign the full gap to a single method, code size or collection cost.

The split therefore persists through longer warmup on the same image.
It limits conclusions from small factory changes and supports holding
multiple independent workers in subsequent source comparisons. The
next implementation removes duplicated native call-entry checks. This
profile does not estimate the effect of that source change.

All 56 checkpoint, 20 profile and 12 follow-up batches verify. Both
quiet gates pass. The workers and collectors join, the guest fixtures
are removed and the raw archive verifies under the lease. Staging
retires only the already archived B1413 candidate binary duplicate.

[Exact image](identity.json) · [Timings and samples](summary.json) ·
[Active compiled versions](active-jit-versions.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
