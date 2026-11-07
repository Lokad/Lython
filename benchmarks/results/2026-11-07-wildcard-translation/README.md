# Native UTF-8 wildcard translation — 2026-11-07

R51 replaces the complete intermediate representation of `fnmatch.translate` and
`glob.translate`: decoded inputs, copied glob segments, UTF-16 regex builders,
per-class builders and final encoding. Both use the original UTF-8 and one
governed output builder, with input-sized initial capacity and ordinary funded
growth/overlap. Completed output takes over its backing. Matching itself and the
provider contract are unchanged; no producer release is required.

Baseline production is 80833f6e, frozen R50 core (informational revision predates
that commit); candidate is the exact source inventory in [focused.json](focused.json).
The [identity](identity.json) records cores, unchanged worker and reused driver.
No claim that informational metadata alone proves source/binary correspondence.

Two complete scripts read 512 filename/path patterns, translate Unicode classes
and recursive paths, retain a result list and write a report. Independent isolated
CPython 3.13.2 supplies complete output/file goldens. Three original CSV/text/JSON
scripts are unchanged controls. Windows newline mismatch initially failed the
unchanged baseline before any samples; its exact catalog/source/error remains.
Explicit file newline and canonical stdout corrected the fixture.

Normal .NET 11 GC/tiering, isolated images sharing CoreLib/hash seed/process GC,
32 warm jobs per image, five rotated rounds of four complete jobs. Warm includes
host fixture reset, mediated I/O, complete golden checks and result disposal;
compilation/startup/final cached teardown are excluded. This is a desktop
allocation diagnostic, not a backend or CPython timing qualification.

| Workload | Refined sync allocation | Refined async allocation |
| --- | ---: | ---: |
| fnmatch report | −25.029% | −25.017% |
| glob report | −31.797% | −31.809% |

Original pipeline allocation improves 21–23%; reserving useful initial capacity
removes repeated small admission/growth phases. Both stages are preserved.
Unchanged control allocation remains within 0.01% in the refined diagnostic.
Elapsed is **inconclusive**: initial async glob is about 8% adverse; refined
fnmatch sync ranges +0.59–23.83% with 23.1% identical-before drift, and async glob
ranges −0.96–+7.09% with −7.51% control drift. Do not transfer desktop gains or
declare that the original cost has been resolved. A quiet source-pair confirmation
is pending; architectural simplification and allocation reduction are established.

Focused Release checks pass on actual .NET 11 and .NET 10: 29 internal and 133
public per target, plus two real in-flight separator cancellation checks per
target. They cover Unicode/ranges/anchors/recursive-hidden paths, provider-free
translation, retained result funding, first refusal, scratch cleanup and allowance
reuse. The dedicated cancellation worker is joined before refund; its duration
does not establish a latency guarantee. Full platform/stress/package qualification
is pending; the earlier R50 run does not qualify this source.

[Initial observations](local-summary.json), [refined observations](refined-local-summary.json),
[decision and archive hash](decision.json), and [development evidence](development.tar.xz)
retain source/cores, checks, all raw diagnostics and original failed conditions.
Conservative initial output capacity changes tiny-budget thresholds; output and
replacement storage remain admitted. No startup, CPU, RSS, tail, matching speed or
universal performance claim. Publication remains postponed.
