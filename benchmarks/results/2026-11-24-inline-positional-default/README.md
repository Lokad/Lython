# B1655 Keep one evaluated positional default on the callable

Dynamically evaluated singleton defaults have a stable reference on the
function. The first `__defaults__` read materializes a canonical tuple from
the admission already held since before evaluation. The change removes the
singleton tuple subclass and its member resolver. Multiple evaluated defaults
keep their existing slot-based tuple, and literal tuples stay in compiled
code. Large values retain their backing admission through preparation. A
reassignment preserves earlier selected values and any exposed tuple aliases.

The reference is `ad893eb3b`. The candidate contains the recorded patch.
Both verified 32-file images match five complete CPython 3.13.2 workload
goldens and two untimed boundary cases, including large values, tuple-valued
defaults, replacement after binding a generator, and an escaping decorator.
The first implementation confused compiled tuples with runtime singleton
slots. The regression logs and the corrected slot-marker approach are archived.

## Managed allocation and admission

| Complete script | Reference | Candidate | Change | Peak admission |
| --- | ---: | ---: | ---: | ---: |
| Defaulted rule pairs | 934,584 B | 803,512 B | −14.02% | 2,786,655 B in both |
| Eager annotated rules | 423,304 B | 357,768 B | −15.48% | 2,444,081 B in both |
| Postponed annotated rules | 636,416 B | 570,880 B | −10.30% | 2,902,945 B in both |
| Generated ordinary definitions | 290,192 B | 257,592 B | −11.23% | 971,730 B in both |
| Quote-heavy CSV invoice | 3,281,382 B | 3,281,438 B | +0.002% | 9,040,266 B in both |

The Windows .NET 11 RC3 diagnostic covers warmed execution and result
disposal. Every measured allowance returns to baseline. Both platforms pass
5,120 focused .NET 11 and 133 .NET 10 Release checks. Windows also passes
73 Debug checks. These include cancellation during suspended default
evaluation, refused storage and preservation of retained values.

## Controlled timing

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Ranges include the four pairings of two worker medians per image.
The workers warm for five seconds and provide seven rotated batches each.
Warm elapsed includes applicable mediated RAM I/O, verification and
previous-result disposal. Time to result also includes compilation and
compiled-owner disposal. Wider process CPU includes request framing and
fixture reset. Process startup is outside these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rule pairs | Warm | −2.88 to +2.41% | −3.26 to +1.96% | 4.01 / 1.39% |
| Eager annotated rules | Warm | −3.65 to +0.80% | −2.75 to +1.30% | 4.37 / 0.23% |
| Generated ordinary definitions | Warm | −0.51 to −0.03% | +0.35 to +4.08% | 0.26 / 0.22% |
| Quote-heavy CSV invoice | Warm | −0.97 to +0.61% | −1.25 to +1.27% | 0.86 / 0.72% |
| Postponed annotated rules | Warm | −0.84 to +8.65% | −2.49 to +7.39% | 6.88 / 2.51% |
| Defaulted rule pairs | Compile to result | −2.38 to +1.24% | −1.06 to +0.66% | 2.43 / 1.25% |

The generated lane spends more wider CPU with nearly unchanged elapsed.
Postponed rules have material same-source spread. Both lanes and the
invoice control are repeated with longer requested batches to reduce
process CPU rounding and check worker stability.

## Confirmation with longer batches

The same images and catalog repeat three lanes with a 0.82-second
target instead of 0.28 seconds. The four medians per lane still come
from seven rotated batches per worker.

| Workload | Warm elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Generated ordinary definitions | −0.38 to +0.24% | −0.97 to +0.39% | 0.57 / 0.05% |
| Quote-heavy CSV invoice | −1.77 to +0.47% | −1.52 to +0.76% | 1.10 / 1.18% |
| Postponed annotated rules | −20.39 to −0.84% | −20.91 to −0.54% | 21.10 / 2.86% |

The generated CPU increase does not repeat. The postponed-rule reference
workers differ by 21.10%, so the second block does not establish a speedup.
Keep the representation for the demonstrated allocation reduction and
removal of the singleton subclass. Timing remains inconclusive. Both runs
pass their quiet gates, verify all 252 batches, join their workers and archive
the observations after fixture cleanup. The staging receipt records three
historical binary replicas retired after verifying their local copies and
preserving the archived observations.

[Identity](identity.json) · [Allocation](allocation-summary.json) ·
[Original timings](quiet-summary.json) · [Original observations](quiet-observations.tar.xz) ·
[Original terminal receipt](quiet-terminal-status.json) ·
[Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal receipt](confirmation/quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz).
