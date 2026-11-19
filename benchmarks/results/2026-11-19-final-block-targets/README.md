# B1086 direct executable branch targets

The private `943793987` compiler deletes the pass that rewrites every
instruction to use compacted block indices. Operand analysis reads the
compacted target map, then emission writes physical instruction positions.
Abrupt jumps still reach the original protected region before cleanup.
Ordinary jumps and match misses follow their existing threaded targets.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Configuration manifest, compilation | −3.11 to −1.60% | −3.65 to −1.59% | 0.94 / 0.61% |
| Configuration manifest, compile-to-result | −1.48 to −0.58% | −1.94 to +0.92% | 0.12 / 0.78% |
| Generated validator, compilation | −3.29 to +22.60% | −6.85 to +20.73% | 0.44 / 26.22% |
| Generated validator, compile-to-result | −1.29 to −0.05% | −2.87 to 0.00% | 0.69 / 0.56% |
| Annotated rules, compilation | −0.51 to +1.10% | −0.82 to +1.10% | 0.81 / 0.80% |
| Shipment report, warm | −1.95 to +0.57% | −1.53 to −0.39% | 0.73 / 1.82% |

Manifest compilation improves with lower CPU. Validator compilation remains
uncertain. Its two identical candidate processes each sustain a separate
rate after warmup. Their timed medians are approximately 2.467 and 3.113 ms
per invocation, and each process has elapsed IQR below 2%. The cause of the
split remains open. Preserve both processes and keep the candidate private
pending a bounded confirmation of the compile-only result.

B1088 repeats the exact images with longer warmup and two validator blocks.
The candidate split did not recur. The change was retained on structural and
correctness evidence with mixed timing. [Confirmation](confirmation/README.md).

Six instruction and protected-region inventories match exactly, including a
nested `try`/`finally`, abrupt loop control and match probe. Both complete
32-file images match the five catalog goldens against CPython. Local
compilation allocation is effectively unchanged, and all compilation
owners return their allowance. Each local target passed 375 focused
Release cases across the white-box and public suites. The two subprocess
cases passed after building the probe and putting the pinned .NET runtime
first on `PATH`. The original Windows setup failures are retained.

The source pair ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on 2026-11-19 UTC. Ranges cover all four pairs of medians from
two processes per source. All 120 timed batches verified in 277.81 seconds.
Both quiet gates passed, workers joined, fixtures were removed and the
persistent archive verified under the lease.

Compilation includes compiled-owner disposal. Compile-to-result also
includes execution, mediated RAM-backed I/O, golden checks and cleanup.
Warm includes mediated I/O, goldens and previous-result cleanup. Process CPU
includes wider request costs. This pair measures Lython revisions.

[CPU, elapsed and allocation observations](summary.json) ·
[Raw observations](observations.tgz) ·
[Exact source, checks and procedures](checks-and-procedures.tar.xz).
