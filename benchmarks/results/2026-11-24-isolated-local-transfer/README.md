# B1546 keep the local transfer outside hot dispatch

Private `9f8986913` keeps the shared transfer for fixed CSV unpack and
completed parallel assignments in the ordinary driver. The new assignment
handler runs out of line. The native loop uses its original reversal and
stores. A separate Windows JIT diagnostic with tiering disabled restores
the native loop from 5,609 to its reference 5,476 bytes and reduces the
ordinary dispatcher from 12,791 to 12,404 bytes. The ordinary reference
is 12,537 bytes. The VM runs with ordinary tiering.

| Warm workload | Elapsed change | Process CPU change | Worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Explicit-float meter | −7.19 to −2.82% | −7.55 to −3.36% | 3.34 / 1.33% |
| Short Unicode report control | +1.49 to +3.10% | +1.39 to +2.62% | 0.85 / 0.74% |
| Quote-heavy invoice control | +0.16 to +1.66% | +0.72 to +2.53% | 0.74 / 0.75% |

The explicit meter improves again. The previously adverse short control
still uses more CPU and elapsed despite restoring the native loop. The
quoted control also moves adversely with stable workers. Keep the runtime
change private and leave the cause of the control differences unresolved.
The footprint diagnostic records the code change. The quiet measurements
determine the effect observed in complete scripts.

Compared with `3e5826f7d` on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on November 23, 2026 UTC. Each lane has two independent
processes per source, 12 seconds of warmup and seven rotated samples per
worker. The ranges cover all pairings of worker medians. Warm reuses
compiled code and includes mediated RAM-backed I/O, verification and
prior-result disposal. Wider CPU includes reset and request handling.
Startup and compilation are outside those clocks.

Both 32-file images match all four CPython goldens, including the typed
Unicode meter used for verification and allocation only in this round.
All 84 samples, quiet gates, joined cleanup and archives verify.
Admitted peaks match and managed allocation differs by at most 56 bytes
per invocation in the local checks. The shared budget returns to baseline.
Windows .NET 11 and Ubuntu .NET 10 each pass 669 public and 58 white-box
focused Release checks, including namespace observation, suspension,
memory refusal, cleanup and managed stack admission.

[Timings](summary.json) · [Tape proof](parallel-bindings.json) · [Allocation](allocation-summary.json) ·
[Image](identity.json) · [Raw observations](observations.tar.xz) · [Procedures and JIT evidence](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json) · [B1545 and the reference image](../2026-11-24-parallel-local-transfer/README.md).
