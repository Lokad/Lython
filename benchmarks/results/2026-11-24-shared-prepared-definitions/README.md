# B1635–B1636 Shared prepared callable definitions

The candidate uses one prepared descriptor for `def` and `lambda`.
It removes the lambda operation and binding types, their separate stack
and dispatch cases, and the constructor's second operation check for
each closed pair member. Class and lowered lambda sites retain their
original namespace capture. Default backing and callable admission remain
separate, including rollback when the second member fails.

## First comparison

The reference runtime is `b807a4eda`. Both 32-file images match their
recorded hashes and differ only in the core DLL. The quiet Azure F4as_v7
ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers
per image warm for at least five seconds of full-request time and each
contribute seven rotated batches per lane. Changes span all four
pairings of their process medians. Positive means more time.

| Workload | Phase | Elapsed change | Process CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | Warm | −2.41 to +5.91% | −2.00 to +6.66% | 0.52 / 7.96% |
| Defaulted rules | Compile to result | −1.62 to +14.16% | −2.88 to +15.80% | 8.18 / 7.27% |
| Eager annotated rules | Warm | −3.28 to +10.66% | −4.21 to +9.24% | 1.68 / 12.53% |
| Eager annotated rules | Compile to result | +1.38 to +2.37% | +0.37 to +5.78% | 0.23 / 0.74% |
| Captured rules | Warm | −6.46 to −0.91% | −3.02 to +1.99% | 4.77 / 1.11% |
| Captured rules | Compile to result | +0.90 to +3.22% | +0.33 to +2.33% | 0.39 / 1.90% |
| Short Unicode report | Warm | −2.99 to +5.06% | −4.01 to +6.47% | 3.71 / 4.43% |

The rule cases run 2,048 groups without guest file I/O. The short report
reads 3,072 lines through the mediated RAM-backed host. Warm elapsed
includes verification and prior-result disposal. Time to result adds a
compilation in the already-running worker. Process CPU also covers
fixture reset and request framing. Startup and final teardown are
outside both elapsed clocks.

## Bounded confirmation

B1636 uses the same full images and catalog with fresh workers. It
repeats the two adverse lanes and changes the job order around the report
control. Both repeat elapsed and CPU ranges cross zero. Worker spread
reaches 5.06% on the affected lanes and 9.20% on the report. Keep the
first adverse results and the unresolved cause. The confirmation also
leaves the warm captured-rule observation without corroborating CPU.

| Workload | Phase | Elapsed change | Process CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Compile to result | −1.10 to +5.53% | −2.84 to +5.69% | 1.56 / 5.06% |
| Short Unicode report | Warm | −6.02 to +7.39% | −5.98 to +5.38% | 4.64 / 9.20% |
| Eager annotated rules | Compile to result | −5.92 to +1.48% | −7.27 to +1.77% | 5.02 / 2.72% |

Retain the shared definition for the removed path and unchanged execution
allocation. These blocks establish neither a whole-script CPU improvement
nor a repeatable time-to-result penalty. Bring the adverse lanes into the
next call/frame checkpoint. Both blocks complete 280 timed batches in
total. Quiet gates pass, workers join, guests' fixtures are removed,
and the persistent archive verifies under the VM lease.

## Correctness and allocation

Both images match four full-workload goldens and five CPython probes in
each API. The probes cover class defaults with a distinct enclosing
binding, an annotation lambda following a live capture, default and
annotation effect order, generator-lambda identity, and failed pair
construction before publication. Windows and Ubuntu each pass 1,087
white-box and 2,709 public .NET 11 Release tests and 128 public .NET 10
tests. Existing focused coverage includes cancellation, shared admission,
retained aliases and compiler cleanup.

Warm allocation and peak execution admission agree on all four scripts.
Compilation allocates 40 bytes more on each of the two scripts with a
lambda. The other two match. Retained compilation admission agrees and
the allowance returns to baseline after disposal. The shared descriptor
carries statement metadata fields even when it represents a lambda.

[First timings](quiet-summary.json) · [Image identity](identity.json) ·
[First raw observations](quiet-observations.tar.xz) · [First terminal status](quiet-terminal-status.json) ·
[Confirmation timings](confirmation/quiet-summary.json) · [Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal status](confirmation/quiet-terminal-status.json) ·
[Runtime allocation](allocation-summary.json) · [Compiler ownership](compiler-ownership.json) ·
[Staging archive custody](staging-custody.json) · [Procedures and exact patch](procedures.tar.xz).
