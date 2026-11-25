# B1704 Narrow confirmation

The exact same `701f0b1e5` and candidate images rerun three lanes in
a different order with 0.83-second batches. Each source has two
processes, five seconds of warmup per process, and seven rotated
batches. The machine, hosting boundaries, CPU clock and catalog
match the initial comparison. All 84 timed batches, both quiet
gates, worker joins and archive verification pass.

| Complete script and boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Text summary control, warm async | −8.32 to −0.22% | −7.79 to −0.58% | 5.89 / 2.79% |
| File-fed map, warm async | −2.81 to −1.56% | −3.29 to −1.37% | 0.39 / 0.89% |
| Quoted invoice, warm sync | −0.34 to −0.31% | −0.32 to 0.00% | 0.03 / 0.00% |

Retain the completed-read path. File-fed map improves on both clocks
in both blocks. The initial invoice CPU gain remains diagnostic.
The unchanged text control moves in opposite directions across the
two blocks. The control still runs zero CSV reads. Its variation
leaves the original adverse result unresolved and gives no claim of
a text improvement. No consumer runs out of its admitted owner
during pending release.

[Initial comparison](../README.md) · [Timings](quiet-summary.json) ·
[Images](identity.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
