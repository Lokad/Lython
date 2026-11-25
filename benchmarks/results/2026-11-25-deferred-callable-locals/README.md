# B1735 Deferred callable construction for write-only locals

The trial stages evaluated defaults in the admitted lexical workspace and
constructs pending callables before a general call or a frame handoff can
make them observable. The generated factory still executes all 96 definition
starts and 160 default stores. Three returned callables take the ordinary path.
The compiler marks 93 remaining definitions. The initial staging path accepts
computed positional defaults and leaves compilation-owned literal tuples on
the ordinary path.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Complete ordinary definitions | Warm sync | −11.54% to −6.54% | −9.86% to −5.78% | 3.93% / 1.66% |
| Complete ordinary definitions | Warm async | −7.85% to −7.01% | −7.44% to −6.71% | 0.61% / 0.29% |
| Complete ordinary definitions | Compile-to-result | −3.59% to −2.73% | −2.70% to −2.59% | 0.10% / 0.79% |
| Escaping defaulted rules | Warm sync | −1.87% to +3.57% | −1.69% to +3.25% | 2.48% / 2.98% |
| Shipment callback report | Warm async | −5.54% to −0.43% | −7.87% to +1.53% | 4.20% / 1.15% |

The reference is `35eace33b`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 25, 2026 UTC. Two verified processes per image and
phase each ran seven seconds of warmup and seven rotated batches. Ranges
cover the four pairings of process medians. Negative means the candidate
uses less time. Execution includes output validation and cleanup. The combined
phase also includes compilation and disposal of its owner. Process CPU covers
request framing and fixture resets. Ordinary definitions and escaping rules
perform no guest file I/O. Shipment includes contained fixture reads and writes.

Local execution allocation on ordinary definitions falls from 200,296 B to
80,824 B and peak admission falls from 971,730 B to 330,360 B. Escaping rules
and shipment retain the same measured allocation and peak admission. Windows
.NET 11 and Ubuntu .NET 10 each pass 284 focused Release checks. Four separate
CPython output and file goldens pass for each frozen image, including a default
expression that calls an alias of `locals()`.

[Refinement](confirmation/README.md) · [Identity](identity.json) ·
[Shape](shape-summary.json) · [Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
