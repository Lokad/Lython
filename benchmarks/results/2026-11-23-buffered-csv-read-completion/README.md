# B1479 Buffered CSV read completion

Private `c00739787` uses the existing complete-read path for an already
buffered CSV row, with a state machine reserved for refill, staging or
pending cleanup. It uses the same scanner, descriptor grammar, read
ownership, refusal and cancellation checks as the reference.

Compared with `a94f78fc7` on November 23, 2026 UTC. The quiet Azure F4as_v7
ran Ubuntu 24.04.4 and .NET 11 RC3. Only the core DLL differs across the
two verified 32-file images. Two workers per revision warm for eight
seconds each and supply seven rotated batches. Ranges span pairings of
worker medians. Warm elapsed includes applicable RAM-backed I/O, output
verification and prior-result disposal. Compile to result adds compilation
and compiled-owner disposal. Wider process CPU includes resets and framing.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII CSV meter | Warm | −3.20% to +0.60% | −3.36% to +0.69% | 1.06 / 2.83% |
| CSV meter with explicit float | Warm | +2.35% to +5.27% | +1.98% to +5.61% | 1.14 / 1.69% |
| Short nonlocal word report | Warm | +1.10% to +9.51% | +1.95% to +9.62% | 0.38 / 7.91% |
| Quote-heavy policy invoice | Warm | −1.21% to +0.82% | −4.15% to +1.75% | 1.52 / 0.53% |
| CSV meter with explicit float | Compile to result | −3.54% to −2.68% | −5.31% to +0.49% | 0.66 / 0.24% |

Keep the candidate private. The explicit meter takes longer warm by
both clocks. Its compile-to-result elapsed improves, with mixed CPU.
The typed meter and quote-heavy invoice span zero. The short text
control slows and candidate workers differ by 7.91%, leaving attribution
of the control regression unresolved. All four local allocation medians
and admitted peaks are identical, with full allowance recovery. The
prototype removes state-machine entry from the buffered CSV path and
adds a separate completion branch. These results leave insufficient
benefit to retain that split.

Windows .NET 11 and Ubuntu .NET 10 each pass 354 white-box and 1,582
public focused Release tests. Both images verify four independent CPython
goldens. All 140 timed batches verify in 235 seconds, both quiet gates
pass, and workers join. The fixtures are removed and the archive verifies
under the lease. The first launch stopped at the disk-space gate before
timing. The second kept the margin and retired two working cores whose
bytes matched the existing B1474 and B1475 archives.

[Timings](summary.json) · [Local allocation](local-allocation.json) ·
[Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
