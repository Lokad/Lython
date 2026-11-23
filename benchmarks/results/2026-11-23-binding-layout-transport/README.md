# B1461 Carry the compiled layout through prepared default reads

Private `37239d932` keeps B1460's code-owned signature and takes the
layout once from the known executable code for native call selection
and prepared default reads. Mutable default objects remain on their
own callable. The reference is `fe081752a` with its exact B1457 core.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. Two fresh processes per source warmed for
eight seconds each and supplied seven rotated batches. Ranges span
all pairings of process medians. Warm elapsed includes applicable
mediated RAM-backed I/O, verification and previous-result disposal.
Compile to result adds compilation and compiled-owner cleanup.
Process CPU includes fixture resets and worker protocol. Startup
and final teardown are outside elapsed.

| Workload | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −2.42 to +6.01% | −0.80 to +4.46% | 2.51 / 5.99% |
| Fully supplied helper | Warm | −1.32 to +2.19% | −0.55 to +1.55% | 1.80 / 1.72% |
| Postponed nested factories | Warm | −2.04 to +3.57% | −2.01 to +5.76% | 5.20 / 0.50% |
| Wide native-buffer calls | Warm | −0.88 to +9.34% | −2.22 to +9.50% | 5.06 / 5.00% |
| Factories observing defaults | Warm | −0.60 to +6.51% | −3.28 to +6.67% | 3.40 / 3.63% |
| Postponed nested factories | Compile to result | +4.43 to +8.46% | +6.16 to +9.56% | 3.20 / 0.64% |
| Quoted CSV invoice | Warm | −0.40 to +1.47% | −2.44 to +1.75% | 1.22 / 0.65% |

Both regular factories still allocate 32,776 fewer bytes per run.
Their warm compilation allocates 24 fewer bytes, with matching
execution and compilation allowance peaks. All owners refund.

Keep the refinement private. The local Windows and Linux Tier 1
diagnostics show removal of the virtual layout calls. The native
entry's stack subtraction rises 16 bytes on each platform. The new
eager and observer intervals span zero. The postponed script remains
slower from compilation to result. Removing the virtual calls leaves
the overall cause open. The source benefit and allocation reduction
remain useful evidence for a design that can also replace physical
construction or argument transport. [B1460](../2026-11-23-canonical-function-metadata/README.md).

Windows .NET 11 and Ubuntu .NET 10 pass 348 white-box and 624 public
focused Release checks each. The two 32-file images match six CPython
goldens and 20 code tapes, operand layouts, slot facts and exception
ranges. All 196 timed batches verify. The workers join and the archive
verifies under the lease after guest fixtures are removed.

[Timing](summary.json) · [Run allocation](allocation-summary.json) ·
[Compile allocation](compilation-allocation-summary.json) · [Code parity](executable-parity.json) ·
[Windows JIT](jit-summary.json) · [Linux JIT](jit-linux-summary.json) ·
[Image identities](identity.json) · [Raw observations](observations.tar.xz) ·
[Source and checks](procedures.tar.xz).
