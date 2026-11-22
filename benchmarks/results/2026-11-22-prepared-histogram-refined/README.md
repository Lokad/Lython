# B1402 Prepared histogram with ordinary iteration preserved

Frozen `248266577` and `12934c598` ran on November 22, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
The core DLL is the sole difference in the 32-file images.
The production and test patch matches retained `6e085a0e3`
byte for byte. The reference is the exact B1401 image.

The closed prepared loop reads funded UTF-8 bytes in place and
publishes its Python bindings before fallback or unwind. The
ordinary iterator still uses whole-loop accumulation. Both paths
use the existing dictionary storage update. The copied short-word
packet and its scratch structure are gone.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Nonlocal word report | Warm | −7.85 to +0.20% | −8.39 to +0.56% | 5.09 / 3.47% |
| Ordinary word histogram | Warm | −12.21 to +1.78% | −11.86 to +1.28% | 8.33 / 7.02% |
| Dense 4,095-word Unicode line | Warm | −24.16 to −22.69% | −21.76 to −19.51% | 1.19 / 0.74% |
| Quote-heavy CSV policy invoice | Warm | −2.44 to +1.86% | −3.73 to +1.15% | 2.26 / 2.09% |
| Nonlocal word report | Compile to result | −1.13 to +4.47% | −1.08 to +5.21% | 3.68 / 1.92% |

Each row spans every pairing of two process medians per source
after eight seconds of full-request warmup and seven rotated
batches. Initial worker order is reversed from B1401. Warm
elapsed includes mediated RAM-backed I/O, verification and
prior-result cleanup. Compile to result adds compilation and
compiled-owner cleanup. Wider CPU includes resets and protocol.
Process startup and final teardown use separate boundaries.
The CPython goldens check semantics. This run adds no measured
CPython ratio.

Retain the revised code for the stable dense-line elapsed and
CPU gain and the eliminated copy. The original 37.30–39.69%
ordinary elapsed loss does not recur. Ordinary time spans zero
with elevated worker spread. The nonlocal warm and first-result
ranges also span zero, with three adverse first-result pairings.
Keep that uncertainty and the B1401 result. The quote control
still allows both directions. Local allocations are effectively
flat and admitted peaks match in all four scripts. The shared
allowance returns to baseline after disposal.

Windows .NET 11 and Ubuntu .NET 10 each pass 347 focused Release
tests for aliases, cancellation, admission and cleanup. The exact
images match all four CPython outputs. All 140 timed batches and
both quiet gates pass. Workers join, the guest RAM mount is
removed and the archive verifies under the lease. The first
staging request ran short of space. The successful request retired
only the prior candidate's byte-proven replica and verified
redundant observations whose canonical archives remain available.

[Original design and reference core](../2026-11-22-prepared-histogram/README.md) ·
[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
