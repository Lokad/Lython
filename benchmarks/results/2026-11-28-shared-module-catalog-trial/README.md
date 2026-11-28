# B1988 Shared builtin registry using delegates

This shelved prototype uses one builtin-module registration for both the
frontend inventory and runtime resolver. It creates one compiler member
snapshot per requested module. Configuration compilation materializes only
`json`, XML selection materializes only `xml.etree.ElementTree`, and the
generated branch script leaves all 66 surfaces dormant. Sixteen concurrent
readers see the same member and export snapshots on first access to another
module. The shared initialization creates frontend and runtime delegates.

The two images agree on 66 modules, 874 members, 871 exports, 2,138 member
queries and 13 compiler diagnostic sequences. Windows .NET 10 and 11 and
Ubuntu WSL .NET 11 each pass 740 public and 67 white-box focused Release
checks. Four stock CPython 3.13.16 output and file goldens match both images.

## First compilation and fresh commands

November 28, 2026 UTC. Forty-two fresh processes on Windows .NET 11 RC3
measure caller-thread allocation during the first compile. The reference
core is `d2d09e50a`, and the candidate is the archived patch. Both retain
the same compilation charges and refund to baseline.

| Script | Reference CLR allocation | Candidate CLR allocation |
| --- | ---: | ---: |
| Configuration edit | 400,288 B | 407,480 B |
| XML selection | 336,544 B | 343,832 B |
| Generated read-only branches | 1,008,824 B | 1,008,824 B |

Separately, 48 fresh commands on Ubuntu WSL use verified images on its
native disk and RAM-backed fixtures. The parent clock includes process
startup, compilation, execution, I/O, verification, result cleanup and
exit. Configuration takes 258.08 → 263.88 ms elapsed, and XML takes
232.02 → 233.36 ms. Median process CPU declines about 2.3% for both.
All paired elapsed and CPU ranges span zero.

## Warm compilation and execution

Ubuntu WSL, .NET 11 RC3, one logical CPU. Two workers per image warm for
five seconds and provide seven rotated batches each. Ranges span pairings
of worker medians. Warm compile includes retired compiled storage.
Compile-to-result and execution include applicable RAM-backed I/O,
verification and result disposal. Process CPU also includes framing and
fixture reset. All 140 batches verify.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | −0.24% to +3.25% | −0.38% to +0.26% |
| Configuration edit | Compile to result | +1.44% to +3.90% | +2.31% to +2.56% |
| XML selection | Compile to result | −3.51% to +5.85% | −0.17% to +0.61% |
| Generated read-only branches | Compile | −4.40% to −2.56% | −0.65% to +2.10% |
| Builtin module imports | Warm execution | +2.20% to +4.29% | +0.67% to +1.77% |

The prototype stays out of production. Its increased first-compile
allocation and adverse warm observations warrant testing a shared
index without eager factory delegates.

[Allocation](allocation.json) · [Fresh timing](cold-summary.json) ·
[Warm timing](warm-summary.json) · [Image identity](identity.json) ·
[Warm observations](warm-observations.tar.xz) · [Patch and procedures](procedures.tar.xz).
