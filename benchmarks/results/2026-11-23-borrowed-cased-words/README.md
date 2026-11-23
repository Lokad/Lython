# B1456 Borrow cased UTF-8 words for dictionary updates

Private `1439cecac` lets the existing callback-free dictionary loop
probe with the prepared UTF-8 bytes for words up to 4 KiB. Repeated
keys can update without a string or payload allocation. A miss or
observable binding still gets an independent prepaid word. Larger
words use the existing cancellable copy.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. The reference is `09a97a3a0` with the
production source and exact core of B1454. Two workers per source
supplied seven rotated batches after eight seconds of warmup each.
Ranges cover all pairings of worker medians. Warm elapsed includes
mediated RAM-backed I/O, verification and previous-result disposal.
Process CPU also includes resets and protocol. Startup, compilation
and final teardown are outside elapsed.

| Warm script | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Mixed-width direct histogram | −14.07 to −12.24% | −12.81 to −12.06% | 2.13 / 0.00% |
| Repeated Unicode direct histogram | −5.34 to −5.01% | −4.52 to −4.31% | 0.31 / 0.04% |
| Distinct medium-length words | +9.52 to +13.40% | +10.82 to +14.53% | 3.07 / 0.45% |
| Short nonlocal report | +0.04 to +5.25% | 0.00 to +4.14% | 4.06 / 1.10% |
| Plain split report | −1.41 to 0.00% | −1.55 to +3.17% | 0.28 / 1.15% |
| Quote-heavy invoice | +0.06 to +2.34% | 0.00 to +1.79% | 0.13 / 2.15% |

Local managed allocation falls 46.37% for the mixed-width histogram and
40.15% for the repeated Unicode histogram. Admitted peaks fall 41.25%
and 39.34%. At a shared allowance of 1,100,000 bytes including compiled
code and projection, the candidate completes the mixed report and
matches stdout and file bytes. The reference refuses admission. Both
return to baseline after result disposal and to zero after code disposal.
The distinct workload has the same admitted peak across both images.

Keep this source private. The repeated-word gains are established for
these workloads. The distinct-word regression warrants preserving native
insertion on a miss using the already computed hash. The timing leaves
the separate cost of the lookup and interpreter handoff unresolved.

Windows .NET 11 and Ubuntu .NET 10 each pass 44 white-box and 182 public
focused Release checks for casing, cancellation, exact key identity and
retained funding. Both 32-file images match all six CPython goldens and
all 168 quiet batches verify. Workers join, guest fixtures are removed
and the archive is verified under the lease. The first staging attempt
refused before launch because the generic staging estimate exceeded
space. The measured run sized the unpacked files explicitly, preserved
the active floor and retired one hash-verified copy of the archived
B1455 working core.

[Timing](summary.json) · [Allocations](allocation-summary.json) ·
[Shared allowance](shared-allowance.json) · [Image identities](identity.json) ·
[Raw observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
