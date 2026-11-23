# B1522 feed completed integer calls into their adjacent updates

Retained `1b9841606` passes a completed exact integer return directly
to an adjacent augmented operation. The original left operand is already
evaluated. The existing local store consumes the exact result when
the destination is private and retirement can complete immediately.
The original publication path remains available for overflow, protocols,
namespace effects, interrupted execution or pending cleanup.

The reference is `ec162951e`. Both frozen images match four CPython
goldens and all ten inspected executable bodies. A separate diagnostic
adds one to the fused result. The supplied report changes from
8,421,311 to 8,423,359, confirming 2,048 uses of the path. The frozen
measurement images retain their original hashes.

| Complete script and phase | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Fully supplied arithmetic, warm | 3.07–3.42% less | 3.19–3.40% less |
| Fully supplied arithmetic, compile to result | 2.68–3.09% less | 3.74–5.79% less |
| Quote-heavy invoice, warm | 0.82–4.25% less | 1.75–4.65% less |
| Short nonlocal word report, compile to result | −0.80% to +3.21% | −1.10% to +2.94% |
| Explicit-float CSV, warm | −2.99% to +1.04% | −1.64% to +2.54% |

Worker-spread extremes are 0.03–0.36% across the supplied lanes.
The word report's reference spread is 3.57%. Its effect and the
explicit CSV control span zero. Windows managed allocation and
admitted peaks agree exactly in all four diagnostic scripts.
The supplied script allocates 142,664 bytes and peaks at 1,255
admitted bytes in each image.

The quiet Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. Each timed lane uses two processes per image,
eight seconds of warmup and seven rotated batches per process.
Warm elapsed covers execution, applicable mediated RAM-backed I/O,
verification and prior-result disposal. Time to result adds compilation
and code-owner disposal. Wider CPU includes the whole request.
All 140 batches, the two quiet gates, full image hashes, worker joins
and fixture cleanup verify. The persistent archive stays under the lease
through verification.

The retained source passes 722 public and 322 white-box focused Release
checks per platform. A hosted regression covers the original left
snapshot across reentry, signed-range overflow, in-place protocols and
zero division. It passes both APIs on both platforms on either source.

## Allocation attribution

Local Windows EventPipe capture on the reference assigns **91.86%**
of sampled allocation weight to boxed BigInteger. Its default inlined
stack slots establish the dispatch neighborhood, with inconclusive IL
location. A second diagnostic disables inlining only in the local read
and integer publication methods. It resolves the BigInteger samples to
`PublishInteger` through `ReadLocalValue` and `LoadLocal`. The frozen
reference remains unchanged. These profiles measure allocation sources.
A redesign needs to preserve canonical identity and snapshots across
dynamic rebindings and callbacks.

[Timing](summary.json) · [Identity](identity.json) · [Tapes](tape-summary.json) ·
[Execution proof](negative-evidence.json) · [Allocation](allocation-summary-six.json) ·
[Boxing attribution](boxing-summary.json) · [Raw observations](observations.tar.xz) ·
[Procedures and profiles](procedures.tar.xz).
