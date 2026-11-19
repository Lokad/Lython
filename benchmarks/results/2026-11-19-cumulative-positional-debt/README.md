# B1065 cumulative call checkpoint and positional debt

This comparison tests whether the positional warm slowdown observed in
[B1041/B1042](../2026-11-19-prepared-definition-text/README.md) persists across
the retained call work. The reference is `b491250cb` before R611. The other
image is `43c9f2c38` through R629, before the later trim change. The case
definitions are unchanged. Both 32-file images passed the three-case
verification with CPython goldens.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four combinations of duplicate process medians. Negative
changes favor the later revision.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Large positional calls | Warm | −3.81…+9.83% | −3.77…+7.43% |
| Ordinary rule closures | Warm | −4.45…−3.23% | −7.01…−5.79% |
| Expanded forwarding | Compile-to-result | −1.66…+0.89% | −3.72…+1.84% |

Ordinary rules improve, with each same-source elapsed spread below 0.73%.
The positional medians are 2.050 and 2.226 ms on the reference and 2.141
and 2.252 ms on the later revision. Three of the four combinations are
adverse. The 8.58% and 5.16% same-source spreads, plus up to 6.62% batch
IQR, leave the cumulative positional effect unresolved. Forwarding spans
zero. This comparison cannot attribute effects to any single change.

The timing jobs finished in 140.06 seconds. Both quiet gates and all 60
timed goldens passed. Workers joined, guest fixtures were removed and the
archive verified under the lease. Warm elapsed includes mediated I/O,
goldens and previous-result cleanup. Compile-to-result also includes
compilation and compiled-owner disposal. Process CPU has a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
