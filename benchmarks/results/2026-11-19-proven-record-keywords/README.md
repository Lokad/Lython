# B1124 and B1126 proven keys in record forwarding

When a dense record shares an immutable layout and the callee has no
keyword-bindable parameters, the layout already proves every key is a
string and collisions are impossible. Lython now copies the independently
owned values directly after admission and source revalidation. Calls
with named parameters still check collisions. Other mappings keep the
existing key validation.

The reference is `40ab5132a` and the measured prototype is `d4e749cc3`.
The runtime change was retained at `f6cbcdb8d`. Windows .NET 11 and Ubuntu
.NET 10 each passed 122 focused Release checks. The added case covers
positional-only names, named collisions and an invalid key after a layout
change. Both 32-file images passed five CPython goldens. Local changes in
allocation were no more than 0.02%. Accounted peaks matched and shared
allowances returned to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. The confirmation reverses the initial worker order on all four
lanes. Warm elapsed includes mediated RAM-backed I/O, goldens and
prior-result cleanup. Process CPU includes the wider worker boundary.
Ranges cover all four combinations of duplicate process medians.

| Workload | Initial warm elapsed | Initial CPU | Confirmation warm elapsed | Confirmation CPU |
| --- | ---: | ---: | ---: | ---: |
| JSON keyword transform | −0.26…+3.31% | 0.00…+4.85% | −1.68…−0.25% | −0.87…+0.24% |
| CSV keyword invoice | −2.56…−0.25% | −2.94…0.00% | −2.07…−1.61% | −3.34…−1.49% |
| Expanded text report | −0.38…+3.03% | +0.78…+3.99% | −0.66…+0.63% | −2.76…+1.16% |
| Positional JSON transform | +2.68…+3.69% | +2.36…+5.21% | −3.04…+0.03% | −4.22…−0.77% |
| Positional CSV invoice | −1.42…+1.32% | 0.00…+1.47% | — | — |

The CSV keyword invoice is faster in both runs, with clear CPU support
in the confirmation. JSON keyword timing remains uncertain across the
two runs. The original CPU increase on the positional JSON control
reversed and the expanded text control became inconclusive. Keep the
earlier adverse results alongside the confirmation. Retain the change for
the eliminated scan and the repeatable CSV result.

All 180 timed batches and both sets of quiet gates passed. The VM joined
workers, removed guest fixtures and verified the observation archives
under the owned lease. Full delivery checks are pending.

[Initial summary](summary.json) · [Initial observations](observations.tgz) ·
[Confirmation summary](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
