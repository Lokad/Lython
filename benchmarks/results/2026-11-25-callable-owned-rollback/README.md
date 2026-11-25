# B1717 Callable-owned rollback for private definitions

A failed member construction retires its unfinished defaults. A completed
callable owns those slots, so the pair keeps only its two callable references
until publication. A later failure retires those callables in reverse order.
Single closed computed definitions also skip the rollback path on success.
Default and callable admission remain separate. The candidate changes runtime
construction only. Managed allocation for the three affected scripts and all
retained compilation and execution peaks match.

The reference is `60a293724`. Both frozen images share every file except
the core library and verified five complete CPython-derived goldens.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Two workers per image warmed for at least five full-request seconds
each and contributed seven rotated batches per phase. Each range spans the
four pairings of worker medians. Negative changes mean less time.

Warm elapsed includes applicable mediated RAM-backed I/O, goldens and result
cleanup. Compile-to-result includes compilation, execution and disposal of the
result and compiled owner in an already-running host. Compile-only includes
disposal of the compiled owner. Wider CPU includes fixture reset and request
framing. Startup and final process teardown sit outside these clocks.

| Workload and phase | Elapsed change | Wider CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted pairs, warm | −4.02 to −2.99% | −4.33 to −0.94% | 0.80 / 0.27% |
| Defaulted pairs, compile-to-result | −6.37 to −2.83% | −5.37 to −1.52% | 2.12 / 1.62% |
| Eager annotations, warm | −3.49 to −2.98% | −4.23 to −3.20% | 0.06 / 0.48% |
| Eager annotations, compile-to-result | −6.90 to −0.94% | −8.27 to −0.11% | 6.22 / 0.17% |
| Postponed annotations, warm | −0.74 to +6.27% | −1.88 to +5.53% | 6.29 / 0.72% |
| Postponed annotations, compile-to-result | −0.69 to +9.07% | −1.41 to +9.01% | 6.22 / 3.39% |
| Ordinary definitions, compilation control | −1.31 to −0.62% | −0.24 to +0.74% | 0.18 / 0.53% |
| Shipment, warm async control | +0.13 to +1.37% | +1.48 to +3.77% | 0.45 / 0.78% |

## Confirmation

The same worker images measured four phases with reversed starting role order,
seven seconds of warmup per process, and 112 batches. Both quiet checks, worker
joins and archive verification passed. The shipment control ran first.

| Workload and phase | Elapsed change | Wider CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Shipment, warm async control | −1.56 to +1.69% | −2.89 to +2.85% | 2.44 / 0.84% |
| Postponed annotations, warm | +7.96 to +24.44% | +9.42 to +25.00% | 2.73 / 12.20% |
| Defaulted pairs, compile-to-result | −5.43 to +2.91% | −4.99 to +4.11% | 5.92 / 2.74% |
| Postponed annotations, compile-to-result | +4.57 to +8.32% | +3.31 to +6.91% | 3.23 / 0.34% |

Keep the combined prototype private. Postponed compile-to-result uses more
CPU in the confirmation. The warm candidate workers disagree on magnitude,
and defaulted compile-to-result and shipment timings remain uncertain.
The postponed script constructs one callable per computed definition and
exercises the changed single-return path. The defaulted pair exercises the
separate transfer of rollback ownership. A pair-only variant can isolate that
change while preserving the existing single-return path.

[Confirmation timing](confirmation/quiet-summary.json) ·
[Observations](confirmation/quiet-observations.tar.xz) ·
[Terminal receipt](confirmation/quiet-terminal-status.json) ·
[Procedures](confirmation/procedures.tar.xz).

## Correctness

Windows .NET 11 and Ubuntu .NET 10 each passed 4,172 focused Release checks.
When an unpublished pair fails, the shared budget returns to baseline with
its scope alive. The test exercises positional defaults, a multi-entry
keyword backing and eager annotation storage. A separate CPython 3.13.2
probe matched wide integer defaults, signed arithmetic and retained keyword
aliases in both hosting modes. All 224 batches and both quiet gates passed.
Workers joined, fixtures were removed and the archive was verified under the
lease.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).
