# B1538 run the prepared split only at its marked member instruction

Retained `43688c753` carries the cased-split source measured at
`66c2a5525`. The measured source refines
[B1537](../2026-11-23-early-cased-split/README.md).
The new guard runs only at a marked instruction and calls an out-of-line
handler. Custom receivers continue through their original method calls.
The Windows full-optimization diagnostic restores the ordinary member
loader to 947 bytes. The native runner grows by 32 bytes over the
`e4392967b` baseline and native call code shrinks by 433 bytes. The
separate code-size diagnostic disables tiering.

| Complete warm script | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Short Unicode word report | −10.26% to −5.51% | −10.26% to −4.23% | 3.49% / 1.75% |
| Mixed-width Unicode histogram | −6.29% to −4.87% | −7.48% to −3.76% | 0.83% / 0.68% |
| Quote-heavy policy invoice | −1.93% to −1.44% | −2.50% to +0.18% | 0.38% / 0.12% |
| Function-scoped supplied arithmetic | −2.15% to −1.30% | −2.46% to −0.35% | 0.44% / 0.42% |

Ranges span all four worker median pairings. Both text scripts use
less CPU and elapsed time. The short result retains some reference
worker spread. The integration also includes independent regex work.
These observations compare the frozen B1538 source pair.

Windows .NET 11 and Ubuntu .NET 10 each pass 234 public and 461 white-box
focused Release checks. The four goldens match CPython. Local allocations
are flat within 120 bytes per execution and admitted peaks match.
The combined runtime revision passes 391 focused public checks on each
platform, including the new regex projection unwind cases.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
23, 2026 UTC. Each worker warms twelve seconds and contributes seven
rotated batches. Warm includes mediated RAM-backed I/O, verification
and prior-result disposal. Wider CPU includes request overhead.
All 112 timed batches, both quiet gates and joined cleanup verify.
The first staging attempt stopped before
launch at its free-space gate. The completed attempt keeps a 14 MiB
working margin and a separate receipt reserve and reclaims only the
exact redundant B1537 core with a verified archived copy.

[Timing](summary.json) · [JIT size](jit-summary.json) ·
[Allocation](allocation-summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
