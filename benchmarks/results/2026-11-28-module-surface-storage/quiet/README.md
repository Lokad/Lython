# B1948 Confirm module surface timings on the quiet VM

This November 28, 2026 UTC comparison reuses both exact B1947 images,
their input catalog and the two lanes whose local candidate medians had
the largest spreads. Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3.
Both 32-file images match their recorded hashes and verify all four stock
CPython 3.13.16 goldens. The stage reuses 62 immutable donor files.

Two workers per image warm for five seconds and supply seven rotated
batches per lane. The ranges pair all worker medians. Compilation includes
retirement of the previous compiled result. Compile-to-result adds execution,
verification and result disposal. Process CPU also includes request framing
and fixture reset. All 56 timed batches and both quiet gates pass.

| Script | Boundary | Elapsed change | Process CPU change | Within-image CPU spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| XML selection | Compile to result | −0.70% to +0.10% | −2.19% to −1.51% | 0.64% / 0.06% |
| Configuration edit | Compile | −4.26% to −0.61% | −5.08% to −0.06% | 2.64% / 2.58% |

The quiet XML CPU result reverses the adverse local finding. Configuration
compilation favors the candidate with smaller worker spread than in WSL.
Keep both collections, including the original XML and function-free
control findings. Retain the single member representation for its measured
allocation reduction and the quiet warm results. Fresh-command timing
remains inconclusive. All processes join and the observation archive verifies
after RAM-backed fixture removal.

[Original B1947 comparison](../README.md) · [Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz).
