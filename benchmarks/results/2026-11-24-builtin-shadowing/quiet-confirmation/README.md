# Quiet confirmation for builtin binding analysis (B1620)

The comparison uses the original B1615 image pair for `0bf516c79`
and `556c8d71c`. Both 32-file images were verified before staging
and reused from completed quiet runs. Windows and Ubuntu have
qualified the correction on both Release targets, original stress
and installed package consumers. [Delivery CI](https://github.com/Lokad/Lython/actions/runs/45423954431).

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
24, 2026 UTC. Each source had two independently warmed workers and
seven rotated batches per worker. Ranges cover all pairings of
worker medians. Compile includes compiled-owner cleanup.
Compile-to-result adds execution, mediated RAM I/O and result and
file verification. Wider CPU also includes fixture reset and
request framing. Startup is outside these clocks.

| Script | Phase | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Compile | −1.94 to +0.78% | −1.99 to +0.50% |
| Configuration manifest | Compile-to-result | +0.74 to +1.40% | +0.17 to +1.69% |
| Sparse validator | Compile | −2.46 to +0.30% | −3.64 to −1.37% |
| Sparse validator | Compile-to-result | −1.72 to +0.86% | +0.00 to +0.56% |
| JSON summary helpers | Compile | −1.51 to +2.29% | −0.94 to +3.03% |

The adverse manifest compile signal from WSL does not repeat.
Manifest time to result has a small elapsed and CPU cost. Sparse
compile uses less CPU with mixed elapsed, and the JSON control
spans zero. Keep the correctness fix and record the manifest cost
for the next frontend or invocation redesign.

All 140 batches, both quiet gates, worker joins and fixture cleanup
passed. The original WSL observations remain available.

[Quiet summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedure](procedures.tar.xz) ·
[Original source and test evidence](../README.md).
