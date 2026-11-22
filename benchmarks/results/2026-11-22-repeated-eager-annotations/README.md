# B1398 Keep one value for repeated eager annotations

The quiet Azure F4as_v7 compared frozen `32c50290d` and `70ea1083d`
on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3. The
measured source and test patch is retained at `586a90c72`.

When all annotation operands read the same global or constant without
intervening guest work, the compiled definition keeps one read. The
function retains that evaluated reference in its existing metadata
slot. The original preflight funds a complete mutable dictionary before
the function escapes. The first metadata read fills the final record.
Deleting unobserved metadata refunds its backing, and a published
dictionary keeps its charge through surviving aliases.

| Complete workload | Boundary | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −13.12 to −9.51% | −13.52 to −9.69% | 0.62 / 3.52% |
| Eager nested factories | Compile to result | −9.82 to −2.87% | −10.55 to −2.15% | 4.23 / 3.33% |
| Postponed nested factories | Warm | −2.02 to +1.15% | −2.80 to −0.21% | 2.09 / 1.12% |
| Factories observing default tuples | Warm | −1.94 to −1.38% | −2.54 to −1.31% | 0.40 / 0.17% |
| Fully supplied helper | Warm | −1.08 to +1.10% | −1.23 to +1.21% | 2.18 / 0.02% |

Two independent processes per source warm for eight seconds and
complete seven rotated batches each. Ranges span pairings of the
process medians. Warm execution includes output verification and
disposal of the prior result. Time to result also includes compilation
and compiled-owner disposal. These scripts use no guest file I/O.
Wider CPU includes request handling. Startup and teardown are outside
these elapsed clocks.

The eager factory allocates 114,632 fewer bytes locally, a 9.95%
reduction, with matching requests and admitted peak. The other
scripts' allocation and admitted peaks match. The postponed factory's
warm elapsed spans zero. The earlier CPython comparison remains a
separate checkpoint for postponed factory performance.

Windows .NET 11 and Ubuntu .NET 10 each pass 611 focused Release
checks. The cases cover annotation snapshot identities, global
rebinding, post-publication replacement, managed stack admission,
refused preflight and cleanup. Both exact worker images match the
four CPython goldens. All 140 timed batches and both quiet gates pass.
Workers join, guest fixtures are removed and the archive verifies
under the lease.

[Source and images](source.json) · [Timings](summary.json) ·
[Local allocation and admission](local-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
