# B1415 Select native calls with one prefix proof

Both executable function callers now select the supplied or default-bound
native route through the same predicate. This removes a repeated prefix
test on incomplete positional supply. Closure scopes already exclude
the class and annotation contexts that can carry the comprehension
lookup. Mutable defaults are still inspected at call entry, and managed
stack admission, argument binding and suspended transfer stay intact.

The quiet Azure F4as_v7 compared `55424591f` and `b88d14745` on
November 22, 2026 UTC using Ubuntu 24.04.4 and .NET 11 RC3. The
core is the sole difference between the 32-file images. The frozen
patch matches retained `9d2275f06`. Each source has two independent
workers per lane, eight seconds of full-request warmup per worker
and seven rotated batches. Ranges span all pairings of worker medians.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread before / after |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −0.68 to +0.75% | −1.17 to +0.52% | 0.51 / 0.93% |
| Postponed nested factories | Compile to result | −2.05 to +2.74% | −1.30 to +3.00% | 3.82 / 1.03% |
| Eager nested factories | Warm | −5.76 to +2.72% | −5.97 to +1.87% | 5.19 / 3.62% |
| Factory observing defaults | Warm | −2.49 to +5.13% | −3.14 to +6.43% | 6.18 / 1.55% |
| Fully supplied helper | Warm | −0.23 to +1.97% | −0.34 to +3.40% | 0.01 / 2.19% |
| Quote-heavy CSV invoice | Warm | −2.21 to +2.08% | −2.25 to +2.71% | 1.91 / 2.43% |
| Large positional call control | Warm | −3.10 to −1.16% | −2.22 to −0.74% | 1.47 / 0.53% |

Warm elapsed includes golden verification, prior-result disposal and
mediated RAM-backed file I/O where applicable. Compile to result adds
compilation and code-owner disposal. Wider CPU includes request handling
and reset. Startup and final teardown remain separate.

Retain the common eligibility proof. The large positional workload
improves on both clocks in every pairing. This narrows a documented
regression on that workload without establishing recovery against its
earlier revision. The postponed factory and the other controls span zero.
The fully supplied helper allows a small adverse result. Local allocation
matches except for 56 fewer bytes on the invoice, and all admitted peaks
and allowance refunds match.

Windows .NET 11 and Ubuntu .NET 10 each pass 3,281 focused Release
checks. Both images match six CPython goldens. All 196 timed batches
verify. Both machine gates pass, workers join, guest fixtures are removed
and the persistent archive verifies under the lease. This comparison
supplies no new CPython ratio.

[Revisions and images](source.json) · [Timing](summary.json) ·
[Allocation and admission](local-allocation.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) ·
[Reference core](../2026-11-22-current-source-cpython/candidate-core.dll.gz).
