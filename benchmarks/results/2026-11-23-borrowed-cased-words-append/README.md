# B1457 Reuse the borrowed lookup for new cased keys

`fe081752a` retains the implementation measured at `9b34db3de`.
Callback-free updates can borrow words of up to 4 KiB directly from
the admitted UTF-8 cursor. A new key uses the hash already computed
by that lookup when the dictionary has room. Admission, protocol
fallback and arithmetic outside the native range use the existing
path. The real key and any final binding receive independent prepaid
storage. Wider words still use the cancellable copy.

Compared with the same `09a97a3a0` source and B1454 core as B1456 on
the quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3 on November 23,
2026 UTC. The distinct-word and short controls ran before the two
target reports. Two workers per image supplied seven rotated batches
after eight seconds of warmup each. Ranges cover all pairings of
worker medians. Warm elapsed includes mediated RAM-backed I/O,
verification and prior-result cleanup. Process CPU includes resets and
protocol. Startup, compilation and final teardown are outside elapsed.

| Warm script | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Distinct medium-length words | +1.75 to +3.62% | +1.15 to +3.07% | 0.55 / 1.27% |
| Short nonlocal report | +3.98 to +11.05% | +0.49 to +10.62% | 6.74 / 0.05% |
| Plain split report | −3.29 to −0.67% | −2.38 to −1.44% | 1.14 / 1.55% |
| Quote-heavy invoice | −1.63 to −0.42% | −2.44 to 0.00% | 0.84 / 0.39% |
| Mixed-width direct histogram | −17.40 to −11.31% | −17.45 to −10.34% | 6.63 / 0.70% |
| Repeated Unicode direct histogram | −5.88 to −2.56% | −8.82 to −2.15% | 2.81 / 0.69% |

Managed allocation falls 46.37% and admitted peak falls 41.25% on the
mixed-width histogram. The repeated Unicode histogram allocates 40.15%
less and its admitted peak falls 39.34%. The distinct control's
allocation and admitted peak match. The 1,100,000-byte shared allowance
again admits the candidate mixed report and refuses the reference.
Both release allowance after cleanup.

Retain the repeated-word improvements with the measured control costs.
The distinct control's elapsed increase falls from 9.52–13.40% in
B1456 to 1.75–3.62% here. The short report still takes longer against a
moving reference and has unchanged allocation and admitted memory.
The results leave the size and source of the short-word cost open.
Keep it in the next integrated comparison.

Both 32-file images match six CPython goldens and all 168 timed batches
verify. The archive is verified under the quiet lease after worker joins
and fixture cleanup. Windows .NET 11 and Ubuntu .NET 10 each pass 44
white-box and 183 public Release checks. The new binding cases match
CPython at the bounded-copy boundary and on numeric promotion after a
miss. The first staging refusal is retained. The measured run removed
temporary duplicate upload storage before checking the active disk floor.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Shared allowance](shared-allowance.json) · [Image identities](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz) ·
[Initial borrowed path](../2026-11-23-borrowed-cased-words/README.md).
