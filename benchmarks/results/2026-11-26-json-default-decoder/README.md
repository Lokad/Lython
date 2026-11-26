# B1780 Enter the JSON decoder directly for default options

This trial splits the immutable, hook-free default entry from option
preparation and the outer decoder state machine. Both hosting APIs run
the same guarded construction under those defaults. Nondefault options
still use the existing continuation and await callbacks. Both paths check
trailing data and the managed stack, and release their frame on return.
[Patch](candidate.patch) · [Source profile](../2026-11-26-json-record-attribution/README.md)

The reference runtime matches `e222c5221`. A separate instrumented
image records 4,096 direct entries per JSON Lines invocation and one
per document totals invocation in either API. Neither non-JSON control
enters the decoder. All four independent CPython output/file goldens
match both images. Local median CLR allocation changes by at most
8 bytes per run, admitted peaks match and cleanup restores the
allowance on every case. [Route counts](routes.json) ·
[Allocation and admission](allocation-summary.json)

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
26, 2026 UTC. The two 32-file images differ only in the core DLL. Two
workers per source warm for at least five seconds and provide seven
rotated batches targeting 440 ms each. Ranges span the four pairings
of worker medians. Negative changes mean less time. Warm elapsed
includes mediated RAM-backed I/O, verified outputs and previous-result
cleanup. Process CPU also covers fixture reset and protocol framing.
Compilation, startup and last-result teardown sit outside these clocks.

| Workload and lane | Initial elapsed change | Initial process CPU change |
| --- | ---: | ---: |
| JSON Lines helper report, sync | −3.54% to −3.36% | −5.14% to −1.52% |
| JSON Lines helper report, async | −6.77% to +0.03% | −6.98% to 0.00% |
| Document service totals, sync | −1.17% to +0.17% | −0.32% to +0.81% |
| Unicode report, sync | −1.32% to +6.50% | +0.80% to +7.82% |
| Ordinary definitions, sync | −1.62% to +0.26% | −0.13% to +0.90% |

All 140 batches complete. Both quiet gates pass, all workers join and
the persistent archive verifies. The first disk preview found
insufficient room in the primary archives' duplicate expansions.
Including completed confirmation archives recovered 7.24 MB of verified
duplicates while keeping the compressed evidence. Both platforms pass
231 white-box and 922 public focused Release checks across Windows
.NET 11 and Ubuntu .NET 10. The selection includes JSON errors and
offsets, nested hooks, reentry, refusal, cancellation, ownership and the
managed stack guard. [Initial timing](summary.json) ·
[Observations](observations.tar.xz) · [Frozen images](identity.json) ·
[Terminal status](terminal-status.json) · [Procedures and checks](procedures.tar.xz)

The [reordered confirmation](confirmation/README.md) reproduces the
synchronous JSON Lines gain and the Unicode control's CPU increase.
Keep the existing decoder. The measured source pair has a useful
synchronous JSON Lines improvement and a reproducible adverse result
on the Unicode report. Async remains unresolved. A subsequent design
can isolate that adverse movement or eliminate the same entry within a
larger decoder consolidation.
