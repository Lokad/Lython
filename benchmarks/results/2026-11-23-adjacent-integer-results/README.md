# B1513 Publish adjacent integer arguments within one dispatch

Private `c2b73b688` uses the current operand slots for the next independent
integer argument. The existing chain can publish a completed prefix and
resume at the first unfinished operation. The fully supplied helper tape
marks its first modulo and keeps the second modulo and add in the same
dispatch. The quoted invoice also has a marked site. The first-use text and
explicit-float scripts have no marked sites. Both images pass four CPython
3.13.16 goldens. Windows .NET 11 and Ubuntu .NET 10 pass 181 focused checks
each, including big-integer promotion, operator callbacks, exception order,
terminal refusal, cancellation, and budget reuse.

The comparison uses retained `b7fdb4fec` on the quiet Azure F4as_v7 on
November 23, 2026 UTC, with Ubuntu 24.04.4 and .NET 11 RC3. Only the core DLL
differs between the two verified 32-file worker images. Ranges pair medians
of two workers per revision after eight seconds of warmup. Warm elapsed
includes applicable mediated RAM-backed I/O, verification and prior-result
disposal. Compile to result adds compilation and code-owner disposal. Wider
CPU includes worker resets and transport. Startup and final teardown sit
outside elapsed.

| Complete script | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied arithmetic | Warm | +0.74 to +4.16% | −0.38 to +7.96% | 1.68 / 1.69% |
| Fully supplied arithmetic | Compile to result | −0.50 to +0.66% | −1.72 to +1.09% | 0.14 / 1.03% |
| Quote-heavy invoice | Warm | −2.30 to −0.07% | −4.15 to 0.00% | 2.01 / 0.26% |
| Short nonlocal word report | Compile to result | −1.44 to +4.61% | −2.30 to +4.49% | 4.66 / 1.41% |
| Explicit-float CSV | Warm | −2.56 to +0.75% | −4.51 to +2.50% | 1.63 / 1.73% |

Keep the runtime variant private. The target script takes longer warm
despite skipping a dispatch. Its first-use effect is inconclusive. The
quote-heavy invoice moves in the opposite direction, and the unmarked
workloads have mixed results. Allocation and admitted peaks are effectively
unchanged in the four local diagnostics, and each restores its allowance.
The source pair does not isolate the cost of partial chain publication.
The regression passes with the retained interpreter in **becdbce3d**,
with 13 focused checks each on Windows .NET 11 and Ubuntu .NET 10.

All 140 timed batches verify and both quiet gates pass. Workers join and
guest fixtures are removed. The quiet-VM archive verifies under the lease.
The local preparation log retains a tape probe that misclassified a flag on
a different opcode and a write denied by a running verifier. Corrected
preparation limits the flag probe to arithmetic and reuses existing files
only after checking their digests.

[Identity](identity.json) · [Timing](summary.json) ·
[Tape sites](marked-tape-sites.json) · [Allocation](allocation-summary-six.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks and procedures](procedures.tar.xz).
