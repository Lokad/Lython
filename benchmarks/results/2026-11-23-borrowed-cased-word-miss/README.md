# B1459 Publish cased words on the dictionary miss path

Private `4abc38067` moves prepaid key publication inside the existing
dictionary miss operation. The interpreter loop no longer carries an
optional hash or dispatches a separate append. Native insertion,
ordinary fallback and final-binding ownership remain available.

Compared with `fe081752a` using the exact retained B1457 image on
November 23, 2026 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. Each source has two workers with eight seconds of
warmup and seven rotated batches. Ranges cover all pairings of
process medians. Warm elapsed includes mediated RAM-backed I/O,
verification and previous-result cleanup. The wider CPU clock also
covers worker protocol and fixture resets.

| Warm script | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Short nonlocal word count | −3.12 to +2.38% | −3.61 to +2.58% | 3.40 / 2.21% |
| Mixed-width direct histogram | +0.26 to +2.35% | −2.03 to +1.27% | 1.57 / 0.50% |
| Distinct medium-length words | +1.13 to +2.06% | +0.95 to +3.66% | 0.55 / 0.37% |
| Plain split report | −3.82 to +0.41% | −3.80 to +2.55% | 2.73 / 1.63% |

Keep the candidate private. The short-word timing spans zero, while
distinct words use more CPU. The caller loses a branch and the miss
handoff, but dictionary storage now receives the cursor for that
operation. This tradeoff leaves the original short-word cost open.

Local managed allocation is effectively flat. All four admitted peaks
match. Both images complete the mixed report at a 1,100,000-byte
shared allowance and release their owners. The local Windows Tier 1
diagnostic shows layout changes in both the dispatcher and the lookup.
The matching Linux candidate run captures earlier tiers only. The
disassembly remains diagnostic.

Windows .NET 11 and Ubuntu .NET 10 each pass 44 white-box and 183
public Release checks. Both frozen 32-file images match all four
CPython goldens, and all 112 timed batches verify. Workers join and
the archive verifies after cleanup. Staging retires three links to
one older working core whose binary and observations remain archived.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Shared allowance](shared-allowance.json) · [Image identities](identity.json) ·
[JIT diagnostic](short-jit-summary.json) · [Raw observations](observations.tar.xz) ·
[Source and checks](procedures.tar.xz).
