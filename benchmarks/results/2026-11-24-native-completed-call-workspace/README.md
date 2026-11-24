# B1582 Complete eligible native calls before workspace setup

`36d8f5ef7` shares the existing completed-call checks between the framed
and native-prefix drivers. Closed lexical definition returns and defaulted
scalar returns can finish before the caller prepares an unpack window and
before the callee enters its 16-slot native workspace. The remaining native
activation still owns binding, argument copying, return slots and transfer
to a funded continuation. Supplied integer returns keep their existing
publication and adjacent augmentation behavior.

A separate Windows x64 .NET 11 RC3 full-optimization JIT inspection, with
tiering disabled, shows that the completed factory and defaulted branches
bypass `StartNativeCall`. The former prologue subtracts 520 bytes from the
stack pointer and clears the inline workspace before either branch. The new
completion routine subtracts 104 bytes and has no workspace clear. The
remaining `StartNativeCall` subtracts 440 bytes. These sizes exclude saved
registers and describe the inspected code. The quiet timed workers use
normal runtime settings.

## Complete-script timing

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Compared `840eba2a7` and `36d8f5ef7` with two independent workers
per revision. Only the core DLL differs across the 32 verified image
files. All five CPython goldens passed; there was no timed CPython lane.

| Warm script | Elapsed change | Wider process CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Postponed nested factories | −9.53 to +0.15% | −9.12 to −0.36% | 5.70 / 4.73% |
| Captured rules | −1.70 to +0.18% | −3.80 to +0.93% | 0.31 / 1.60% |
| Defaulted rules | −9.16 to −1.90% | −8.83 to −0.32% | 7.68 / 0.29% |
| Short text report | −1.73 to +0.11% | −3.46 to +0.78% | 0.81 / 1.06% |
| Quoted CSV invoice | −5.35 to +1.11% | −5.13 to +1.30% | 5.21 / 1.53% |

Each worker warms for eight seconds and provides seven rotated batches.
Ranges cover all pairings of independent worker medians. Warm elapsed
includes applicable mediated RAM-backed I/O, verification and prior-result
disposal. Wider CPU includes fixture reset and the protocol. Compilation,
startup and final teardown are outside both clocks.

The defaulted script has a favorable elapsed and CPU direction in every
pairing with an unstable reference pair. The magnitude remains uncertain.
Postponed factories use less process CPU in these samples and their elapsed
range includes zero. All remaining elapsed and CPU ranges include zero.
Retain the change for the removed workspace step, matching allocations and
admission peaks, and the measured direction on defaulted rules. Carry
forward the earlier text and invoice debt independently of these results.

## Validation and evidence

Windows and Ubuntu each pass 95 white-box and 332 public focused Release
checks on .NET 11. A dynamic-callee fixture exercises both runtime unpack
errors and distinct returned closure identity through both entry points.
The first fixture named the callee directly and triggered the existing
static destructuring diagnostic on both images. CPython 3.13.2 agrees with
the dynamic fixture. The final fixture also passes on the reference.

Independent local allocation diagnostics agree exactly on all five
allocation medians and admitted peaks. Each execution returns the shared
allowance after cleanup. Both quiet gates, all 140 timed batches, worker
joins, fixture removal and persistent archive verification pass. The first
staging attempt stopped before mutation because the disk margin was short.
The retry preserved the observations and verified redundant core copies
against their exact Git archives before retiring those copies.

[Timings](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and JIT inspection](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
