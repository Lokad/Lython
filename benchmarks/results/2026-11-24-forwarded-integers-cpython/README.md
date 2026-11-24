# B1633 Forwarded integers against CPython

Three complete rule scripts compare Lython `d6a168491` with stock
CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11
RC3 on November 24, 2026 UTC. Both engines use two independently warmed
workers per lane, with five seconds of warmup and seven rotated batches
per worker. Ranges span the four pairings of worker medians. The ratio
divides Lython elapsed by CPython elapsed.

| Script | Boundary | Lython | CPython | Elapsed ratio | Wider CPU ratio | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Generated rules with defaults | Warm | 0.90023–0.90770 ms | 0.68111–0.68983 ms | 1.305–1.333× | 1.316–1.340× | 0.83 / 1.28% |
| Generated rules with defaults | Compilation only | 0.02356–0.02378 ms | 0.04156–0.04160 ms | 0.566–0.572× | 0.566–0.577× | 0.92 / 0.10% |
| Generated rules with defaults | Compile-to-result | 0.97727–0.98909 ms | 0.77405–0.77755 ms | 1.257–1.278× | 1.237–1.272× | 1.21 / 0.45% |
| Generated rules with captured locals | Warm | 0.89205–0.89483 ms | 0.76652–0.77553 ms | 1.150–1.167× | 1.149–1.171× | 0.31 / 1.18% |
| Generated rules with captured locals | Compilation only | 0.02626–0.02666 ms | 0.04672–0.04684 ms | 0.561–0.571× | 0.563–0.566× | 1.53 / 0.25% |
| Generated rules with captured locals | Compile-to-result | 0.96229–0.97110 ms | 0.87069–0.87170 ms | 1.104–1.115× | 1.103–1.158× | 0.92 / 0.12% |
| Eager annotated rules | Warm | 0.44264–0.46513 ms | 0.41287–0.42079 ms | 1.052–1.127× | 1.047–1.137× | 5.08 / 1.92% |
| Eager annotated rules | Compilation only | 0.01629–0.01641 ms | 0.03060–0.03076 ms | 0.530–0.536× | 0.531–0.550× | 0.71 / 0.50% |
| Eager annotated rules | Compile-to-result | 0.48583–0.48941 ms | 0.43524–0.43622 ms | 1.114–1.124× | 1.086–1.132× | 0.74 / 0.23% |

Each script builds and applies 2,048 rule groups and uses no guest file
I/O. Warm execution includes output verification and previous-result
disposal. Compilation includes compiled-owner disposal. Compile-to-result
combines both phases in one request in a running process. Lython runs
with a shared memory allowance. Wider CPU includes request framing and
fixture reset. Process startup and final teardown are separate.

Defaulted rules have the largest remaining gap, at 1.305–1.333× warm
and 1.257–1.278× through result. Captured rules take 1.150–1.167× warm
and 1.104–1.115× through result. Eager rules reach their result in
1.114–1.124×. Compilation takes 0.530–0.572× across the three scripts.
Use the within-Lython comparisons to assess the effect of the recent
changes under paired conditions. The earlier external comparison keeps
its text and CSV results. [Earlier baseline](../2026-11-24-forwarded-positional-cpython/README.md).

All 252 batches and both quiet gates passed. The complete 32-file image,
CPython executable, output goldens, worker joins, cleanup and persistent
archive verified.

[Summary](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json) ·
[Within-Lython comparison](../2026-11-24-borrow-forwarded-integers/README.md).
