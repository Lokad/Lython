# B1454 Module-owned known-call analysis

`107536d02` retains the source measured at `aa6c836b0`. The frontend
looks up each known call in its module. Runtime and frontend share
signature instances owned by the same module. Argument and return
analysis select the canonical module once per dispatch. The generic
scan through unrelated semantic families and the repeated return
lookup disappear. The existing checks still handle argument shape,
specialized types and values, diagnostics, and return inference.

Compared `c6d2f2b3d` and `aa6c836b0` on November 23, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both
verified 32-file images share every file except the core DLL.
CPython 3.13.16 provides output goldens and stable fresh-process
controls.

| Fresh process through exit | Reference median | Retained median | Median change | Paired-round change |
| --- | ---: | ---: | ---: | ---: |
| Configuration edit | 264.2 ms | 256.8 ms | −2.79% | −4.99 to +0.19% |
| Typed ASCII CSV meter | 321.2 ms | 312.4 ms | −2.74% | −4.40 to +1.55% |
| Small XML selection | 239.6 ms | 229.5 ms | −4.22% | −6.32 to −0.24% |
| Empty control | 123.3 ms | 120.5 ms | −2.29% | −5.20 to +0.39% |
| Tiny arithmetic control | 148.9 ms | 151.4 ms | +1.64% | −2.02 to +4.82% |

Seven fresh starts per workload and revision alternate source order
and rotate workloads. The clock includes worker launch, first
compilation, one run, applicable RAM-backed I/O, and exit with pipe
drain. The OS file caches stay warm. The controls and paired rounds
limit attribution of the fresh-process improvement.

| Warm lane | Elapsed change | Wider CPU change | Worker spread reference / retained |
| --- | ---: | ---: | ---: |
| Configuration compile | −2.96 to −1.94% | −4.92 to −2.22% | 0.03 / 1.02% |
| Typed CSV compile | −1.76 to −0.94% | −1.61 to −0.96% | 0.67 / 0.16% |
| XML compile | −2.48 to −1.01% | −4.57 to +1.40% | 0.76 / 0.75% |
| Read-only validator compile | −4.91 to +0.37% | −4.76 to +1.21% | 4.10 / 1.39% |
| Expanded JSON Lines execution | −0.86 to +0.41% | −1.35 to +1.39% | 0.44 / 0.84% |

Two workers per revision warm for eight seconds and supply seven
rotated batches each. Ranges cover all pairings of process medians.
Compile elapsed includes prior compiled-owner disposal. JSON elapsed
includes applicable mediated I/O, output verification, and disposal
of the previous result. Wider CPU includes fixture reset and framing.
The validator control has 4.1% reference spread. The standalone
module split's adverse validator CPU result remains archived.
[B1453](../2026-11-23-module-known-calls/README.md).

A Windows allocation diagnostic finds **35.60–40.51% less** first-compile
allocation in configuration, typed CSV, expanded JSON and XML.
Warm allocations and admitted peaks match in all seven workloads.
Every compile refunds its allowance on disposal. Constructor events
show configuration initializing JSON and dataclass runtime signatures.
The intermediate module split initialized thirteen signature groups
for the same script.

Both platforms pass 88 white-box and 1,187 public focused Release
checks. All 493 runtime signatures, 487 frontend contracts and 23
module aliases match, including shared signature identity within
each image. All 1,501 compile probes match validity, diagnostics and
source spans. Both images match seven CPython goldens. All 70 fresh
pairs and 140 warm batches verify. The service finished within 4m27s
with both quiet gates passing and all workers joined. The archive
verifies under the lease and the guest fixture mount was removed.

Initial staging stopped at its disk headroom check before launching.
Recovery retired one byte-verified working copy of the archived
B1453 core. Its exact binary remains available. [B1453](../2026-11-23-module-known-calls/README.md).

[Sources and images](source.json) · [Timing](summary.json) ·
[Allocations and allowance](allocation-summary.json) · [Constructor traces](constructor-summary.json) ·
[Signature parity](parity-summary.json) · [Diagnostic parity](diagnostic-summary.json) ·
[Raw observations](observations.tar.xz) · [Checks and procedures](procedures.tar.xz).
