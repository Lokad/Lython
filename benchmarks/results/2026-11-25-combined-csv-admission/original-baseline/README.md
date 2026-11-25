# B1739 File-fed map against the pre-comprehension baseline

The reference is `4e8e60923` with the `7f0c5b996` production code. This paired
lane compares the complete CSV candidate to the source before the comprehension
change. The same 2,048-record script, async hosting, RAM-backed I/O, result
verification and cleanup apply. Each of two workers per source warms for seven
seconds and runs seven rotated batches on the same quiet Azure F4as_v7.
The comparison isolates file-fed warm execution from compile and startup costs.

| Pair | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Combined CSV admission against `4e8e60923` | −15.19% to −6.26% | −14.77% to −4.89% | 1.69% / 8.70% |

All four pairings favor the combined candidate in this block. That recovers
the file-fed regression in the earlier comprehension comparison. The wide
candidate spread limits the size of the recovery estimate. The earlier
regression's cause remains unresolved.

Both frozen images pass five independent CPython goldens. All 28 timing
batches, two quiet gates, cleanup and persistent archives verify. The aggregate
source patch and worker identities are recorded with the evidence.

[Identity](identity.json) · [Timing summary](quiet-summary.json) ·
[Raw observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Later-baseline confirmation](../confirmation/README.md).
