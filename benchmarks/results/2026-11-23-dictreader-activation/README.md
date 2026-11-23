# B1484 Complete ready DictReader rows directly

Private `d9e57afc0` moves a completed row with an available header
through the existing record builder. A continuation handles an
outstanding input, initial or replaced header, blank rows or dynamic
protocol. It resolves the header after reading the row and keeps the
row funded through release or adoption.

Compared with `9cea1fc0a` on November 23, 2026 UTC. The quiet Azure F4as_v7
ran Ubuntu 24.04.4 and .NET 11 RC3. Only the core DLL differs across the
two verified 32-file images. Two workers per source warm for eight seconds
each and supply seven rotated batches. Ranges span all pairings of worker
medians. Warm elapsed includes applicable RAM-backed I/O, output
verification and prior-result disposal. Compile to result adds compilation
and compiled-owner disposal. Wider CPU includes reset and transport.

| Complete script | Phase | Elapsed change | Wider CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy CSV invoice | Warm | −3.28% to −0.27% | −2.44% to −1.72% | 1.72 / 1.37% |
| Ordinary CSV invoice | Warm | −2.33% to +0.50% | −1.47% to +0.00% | 0.47 / 2.41% |
| Short nonlocal word report | Warm | −6.57% to +5.05% | −6.94% to +5.43% | 9.32 / 2.86% |
| Typed ASCII CSV meter | Warm | −0.70% to +3.25% | −0.35% to +0.72% | 1.95 / 1.99% |
| Quote-heavy CSV invoice | Compile to result | −3.88% to +1.88% | −3.45% to +2.50% | 1.48 / 4.45% |

Keep the full split private. The quote-heavy invoice uses less warm CPU.
The ordinary invoice and quoted compile-to-result elapsed span zero.
The text control's reference workers differ by 9.32% and the typed
control spans zero. Local allocated bytes are within 56 bytes per
invocation, with unchanged admitted peaks and full refunds. The result
supports removing repeated completion work selectively. The larger
rewrite adds ownership handoffs and continuation code for a gain
isolated to one warm lane. Revisit if the header and record paths can
reuse simpler completion without moving the other operations.

Both platforms pass 354 white-box and 1,583 public focused Release checks.
The additional test-only `6acf18c01` passes the 16-case suspended protocol
suite on both platforms, including a header change after a genuine host
read and cancellation while that read is active. Four separate CPython
goldens match both images. The new case for replacing a header inside the
source iterator independently agrees with CPython. The local Tier 1 dump
shows smaller individual entry frames and more generated completion
methods. It does not establish the cost of the consecutive calls.

All 140 timed batches verify in 237 seconds.
Both quiet gates pass, workers join, guest fixtures are removed and the
archive verifies under the lease. Staging retires only the verified
historical reference core. The live baseline candidate remains available.

[Timings](summary.json) · [Allocation and admission](local-allocation.json) ·
[Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
