# B1380 Positional call CPU and JIT attribution

Captured the same two 32-file images as B1377 on November 22, 2026
UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
The source images are `927f0ca88` and `a3338b0a8`. Two independent
processes per image warm up, then run for about 5.5 seconds while
Linux perf samples all worker threads at 199 Hz. All workers use
default tiering with the same perf-map and JIT logging overrides.

| Process | CPU samples | Nearest instruction dispatcher | Nearest direct return entry | Guest elapsed per invocation |
| --- | ---: | ---: | ---: | ---: |
| Candidate | 1,115 | 335 | 110 | 1.620 ms |
| Reference | 1,130 | 373 | 114 | 1.641 ms |
| Reference repeat | 1,123 | 398 | 118 | 1.635 ms |
| Candidate repeat | 1,118 | 361 | 97 | 1.625 ms |

Nearest means the closest resolved Lython runtime frame. The rows
include result verification and prior-result disposal. The timed
part of the diagnostic covers warm execution. Wider per-thread
CPU, raw stacks and generated code are available in the archive.

The original default-tier slowdown does not recur with profiling
active. Candidate elapsed in these short captures falls 0.59–1.31%
and wider CPU falls 0.53–1.23%. The native function entry and the
direct integer-return methods have matching normalized instructions
at each observed tier across all four workers. One worker from each
image also emits identical optimized dispatcher instructions. Other
workers get different optimized dispatchers. The captures identify
no new executed method or source-specific generated path that
explains the normal-tier regression. Preserve B1376 and B1377 and
advance the independent runtime work. Revisit after an actual
source correction or a profile that reproduces the adverse result.

Complete image and catalog hashes match. Both quiet gates pass,
all users join and the raw traces verify under the lease before
fixture storage is unmounted. The first local staging attempt
exceeded the Windows command-line limit. Sending the inventory code
over stdin allowed the same read-only check to complete. Remote
staging retired five exact replicas of already archived traces.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Normal-tier confirmation](../2026-11-22-json-positional-control/README.md) ·
[Fixed-tier diagnostic](../2026-11-22-json-positional-fixed-tier/README.md).
