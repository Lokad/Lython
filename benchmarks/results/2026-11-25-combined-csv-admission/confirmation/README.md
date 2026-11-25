# B1738c Confirm combined CSV record admission

The reordered block uses the same verified 32-file images, frozen catalog,
reference `0374493f9`, quiet machine and clocks as the [first block](../README.md).
The text and plain reader controls run first. File-fed map and shipment run last.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed CSV quantity map | Warm async | −13.30% to −11.77% | −13.40% to −10.00% | 1.75% / 0.01% |
| Shipment callback report | Warm async | −7.38% to −2.18% | −7.43% to −4.00% | 3.75% / 1.80% |
| Quoted policy invoice | Warm sync | −3.00% to +2.89% | −3.80% to +3.36% | 1.65% / 4.35% |
| Plain CSV reader | Warm sync | −0.73% to +2.15% | −0.81% to +2.08% | 1.34% / 1.54% |
| Unicode text report | Warm sync | −8.04% to −1.90% | −7.93% to −0.78% | 6.20% / 0.45% |

File-fed map and shipment repeat their warm CPU improvements. Quoted invoice
timing spans zero. The plain reader's wider adverse range from the first block
does not recur. The text control still moves favorably, so use the measured
ranges as whole-script observations. The route probe separately establishes
which records skip the second reservation and header validation.

Retain the combined admission path. The separate check against the
pre-comprehension baseline also favors the final source on file-fed map,
with wide candidate spread. [Original-baseline check](../original-baseline/README.md).

On the original source a budget
callback can catch a nested read refusal and leave the outer parser with an
empty record instead of `['30', '40']`. The two new tests reproduce it on
the original source and pass with the guard alone. With combined admission,
819 focused Release tests per platform check header exposure, retained key
identity, cancellation, terminal refusal and funding through cleanup. The
platforms are Windows .NET 11 and Ubuntu .NET 10.

[Identity](identity.json) · [Timing summary](quiet-summary.json) ·
[Raw observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz).
