# B1377 Confirm the positional-call control

Repeated the two B1376 controls on the same 32-file frozen images
from `927f0ca88` and `a3338b0a8` on November 22, 2026 UTC. The quiet
Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3. Put the positional
case first and reverse worker startup and timed rotation. Each phase
uses two workers per image with eight seconds of warmup per worker
and seven timed batches.

| Complete warm script | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Positional calls | +2.87 to +4.07% | +2.57 to +5.22% | 0.19 / 0.97% |
| Guest keyword forwarding | −2.46 to +0.06% | −1.92 to +0.24% | 0.76 / 1.80% |

Ranges span all pairings of process medians. Warm elapsed includes
execution, stdout verification and prior-result disposal. Wider CPU
also includes reset and worker request handling. Compilation and
startup are separate. All 56 timed batches verified in 114 seconds.
Both quiet gates, worker join and archive verification passed. Staging
reused both immutable catalogs and retired a verified duplicate raw
archive with its exact contents preserved in Git.

The positional slowdown reproduces with tighter spreads. Guest
forwarding remains inconclusive. Preserve the original result and
keep the positional regression open. This workload has only ordinary
calls, so it never executes the new JSON mapping binder.

Local JIT diagnostics verify the same images and workload. The
native function entry and direct integer return produce matching
normalized instruction listings on Windows and in four WSL processes.
The instruction dispatcher and ordinary-call method differ in
optimized listings, including between processes using the same
image. With tiering disabled in WSL, the normalized instructions and
code size match across all seven inspected methods. The captures
locate variation in generated code and leave the quiet-VM slowdown
unexplained. They use JIT logging and produce no timing comparison.

[Timing](summary.json) · [Raw observations](observations.tgz) ·
[Code generation and procedures](procedures.tar.xz) ·
[Original source pair](../2026-11-22-json-direct-mapping/README.md).
