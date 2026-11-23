# B1545 share the private transfer with completed parallel assignments

Private `81444bec1` uses one binding-range proof and slot transfer for
fixed CSV unpack and parallel assignments. The explicit-float meter
commits three converted operands without reversal or individual stores,
in addition to committing each five-field row together. All right-hand
expressions finish before any assignment. The ordinary path still handles
captured and mirrored locals and targets that can invoke guest code.

| Warm workload | Elapsed change | Process CPU change | Worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Explicit-float meter | −4.50 to −2.42% | −4.55 to −3.88% | 1.54 / 0.62% |
| Typed Unicode meter | −3.57 to +1.82% | −4.48 to +1.13% | 0.49 / 5.07% |
| Quote-heavy invoice control | −0.74 to +1.39% | −0.55 to +1.27% | 0.80 / 1.34% |
| Short Unicode report control | +0.96 to +2.44% | +2.69 to +6.52% | 1.06 / 0.41% |

The compiled tape proves the parallel transfer on the explicit meter.
Both controls contain no assignment shaped for this new path. The
explicit target speeds up with supporting CPU. The short control has
consistent adverse elapsed and CPU. Keep this version private. A local
JIT diagnostic with tiering disabled finds 254 more bytes in the
ordinary dispatch method and 133 more in the native loop. That identifies
a footprint to change independently. It leaves the cause of the measured
control regression unresolved.

Compared with `3e5826f7d` on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on November 23, 2026 UTC. Each of two independent processes
per source warms for 12 seconds and contributes seven rotated samples.
Ranges cover all pairings of worker medians. Warm reuses compiled code
and includes mediated RAM-backed I/O, verified outputs and disposal of
prior results. Process CPU includes the wider worker request.

Both 32-file images match four CPython goldens. All 112 samples, both quiet
gates, joined cleanup and the persistent archive verify. Managed
allocations and admitted peaks match, and the local budgets return to
baseline. Windows .NET 11 and Ubuntu .NET 10 each pass 257 focused public
Release checks. Windows passes another 145 focused assignment and file
checks, and Ubuntu passes 58 focused ownership and stack checks. These
filters overlap the earlier verification.

[Timings](summary.json) · [Parallel binding proof](parallel-bindings.json) · [Allocation](allocation-summary.json) ·
[Images](identity.json) · [Raw observations](observations.tar.xz) · [Procedures and JIT evidence](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Original fixed-unpack trial](../2026-11-24-csv-unpack-bindings/README.md).
