# B1500 Return a lexical definition with computed defaults directly

`a61b79041` extends the existing direct definition return to a factory
whose final default owners receive checked integer arithmetic and a
read-only capture. The compiler uses the existing seven-instruction tape.
The runtime checks exact operands before choosing the route and reuses the
same admission, default storage, lexical constructor and checkpoints.
Wide, overloaded or failing arithmetic and eager annotation reads retain
the ordinary activation. The actual direct constructor runs in the JIT
diagnostic. The postponed factory and defaults observer select it. All five
workload tapes and capacities agree with the original source.

Compared with `2ab453375` using its source-equivalent B1497 image on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
The measured `546b849ce` candidate matches the retained source and tests.
Both 32-file images verify. Two processes per image warm eight seconds
and supply seven rotated batches. Ranges pair their medians. Warm elapsed
includes applicable mediated RAM-file I/O, output verification and prior
result disposal. Compile to result adds compilation and compiled-owner
disposal. Wider CPU includes request reset and protocol. Startup and
final teardown stay outside elapsed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −24.50 to −20.98% | −24.79 to −20.81% | 1.83 / 2.79% |
| Eager nested factories | Warm | −0.31 to +9.13% | +0.20 to +7.54% | 2.80 / 6.49% |
| Factories observing defaults | Warm | −11.70 to −5.29% | −9.30 to −4.80% | 4.30 / 2.84% |
| Fully supplied helper | Warm | −2.45 to −0.77% | −4.02 to −0.73% | 1.60 / 0.12% |
| Quote-heavy CSV invoice | Warm | +0.43 to +1.71% | 0.00 to +1.75% | 0.95 / 0.32% |
| Postponed nested factories | Compile to result | −22.16 to −9.56% | −21.17 to −11.89% | 7.46 / 8.12% |

Retain the direct return. Postponed warm has a large, consistent effect
with supporting CPU. The defaults-observer gain survives metadata access.
Reference and candidate workers vary in time to result, widening its range.
Carry the adverse eager CPU and quoted-invoice elapsed into the next
constructor comparison. This comparison uses two Lython revisions.

All five local allocations and reported execution peaks match, and
both images refund their shared allowances. Windows .NET 11 and Ubuntu
.NET 10 each pass 138 white-box and 980 public focused Release checks.
The added test checks default identity, exact wide arithmetic, overloaded
arithmetic after real async file suspension and shared-budget recovery.
It also passes on the reference. Both images match five CPython workload
goldens and 13 separate default-expression cases, including signed
arithmetic, zero divisors, wide values and observed defaults. The first
semantic catalog paired an `int` return annotation with a tuple result.
Lython diagnosed the mismatch. The corrected script and both receipts
are archived with the original conditions. All 168 timed batches pass
in 283 seconds. Workers join, fixtures are removed and the persistent
archive verifies under the quiet lease.

[Identity](identity.json) · [Timings](summary.json) · [Allocation](allocation-summary-six.json) ·
[Selected bodies](selected-returns.json) · [Raw timing](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Tests and procedures](procedures.tar.xz) ·
[Exact reference core](../2026-11-23-closure-result-consumer/README.md).
