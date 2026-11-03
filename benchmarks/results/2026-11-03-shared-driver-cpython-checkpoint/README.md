# B163 current runtime versus CPython

2026-11-03, source **b087c7b86a02f3ba24e9926bf8e41526f39ba8b2**,
corea3aa23470c2437acf40e4d26db47bdc1b9e3bd35b62b5e0aeb3dd783fcfe54a6.
Current whole-script diagnostic checkpoint, not a source-pair improvement claim.
**CPython3.13.16** (PGO/LTO, ordinary GIL/GC, no JIT/debug), isolated `-I -S -X utf8`;
.NET11RC3, ordinary GC/tiering. Quiet Standard_F4as_v7 Ubuntu24.04.4 as in B162.

Existing checked comparison protocol2 and workers. Ten sequential jobs, each with
current Lython, identical-current control and CPython. Three-second warmups,
nine rotated~200ms batches; all source/fixture/output hashes and complete file
checks pass. Same RAM-backed actual file APIs for both engines; quiet gates pass.
Controller164.770s, owned service165.371s, exit0; all30 worker processes and fixtures
join. Shared lease independently free, no abandoned work. Publication postponed.

| Workload/phase | Lython ms | CPython ms | Lython /CPython | Lython /CPython IQR | Identical-current Lython control |
| --- | ---: | ---: | ---: | ---: | ---: |
|csv-summary-helpers/warm|5.291813|3.318611|1.595×|0.88% /1.23%|-0.71%|
|csv-summary-helpers/compile-run|5.636731|3.493580|1.613×|0.68% /1.08%|-3.07%|
|text-summary-helpers/warm|3.376848|1.194892|2.826×|0.68% /0.23%|+5.02%|
|text-summary-helpers/compile-run|3.842503|1.273670|3.017×|0.90% /0.37%|-3.25%|
|json-summary-helpers/warm|5.816754|4.304100|1.351×|0.67% /0.52%|-1.16%|
|json-summary-helpers/compile-run|5.740335|4.195077|1.368×|1.26% /0.40%|+0.25%|
|report-render/warm|5.464039|2.340383|2.335×|1.51% /1.66%|-0.79%|
|report-render/compile-run|5.938166|2.482121|2.392×|3.94% /2.03%|-5.23%|
|csv-summary-helpers/compile|0.086140|0.054728|1.574×|0.58% /0.20%|+2.08%|
|report-render/compile|0.059127|0.037938|1.559×|0.39% /0.10%|+0.00%|

Ratios greater than one mean Lython is slower. Text warm's5.02%control difference,
text fresh−3.25%, CSV fresh−3.07% and report fresh−5.23%control/3.94%IQR limit
precision. Keep these cases visible as variable/diagnostic. They still show
substantial text/report gaps. Fresh and warm use independent workers; do not
subtract their medians or interpret JSON fresh being lower as negative compile cost.

Warm reuses compiled code; compile-run compiles/disposes every invocation inside
an already-running host. Actual I/O, invocation/host-boundary work, full stdout/file
reads/checks and prior-result disposal are included. Fixture reset is subtracted
symmetrically from both clocks; transport/response/hash encoding, final result,
compiled caches, worker/fixture teardown and startup are excluded. Final users
and cleanup still join outside the batch timer. Compile-only contains no guest
execution/I/O; source sizes CSV599B, report454B, distinct from fixture bytes.
No phase-subtraction estimates, physical RSS/embedding-latency claim or startup
measurement. Lython host overrides ranged reads/appends efficiently.

Source/context identification is explicit in identities and raw observations.
The core was frozen before the matching commit; source is unchanged. Both APIs
are semantically tested separately; this external comparison uses synchronous Run.
B162's shared dispatch debt and API comparisons are separate, as are historical
ordinary-filesystem points. Full runtime delivery41642981787 remains pending when
this record is written; this checkpoint does not substitute for package qualification.

[Assessment](assessment.json), [raw measurements/identities/gates](observations.tgz),
[frozen source identity](identity.json), [existing collector adaptation](collect.py).
[Source-pair variants and unresolved debt](../2026-11-03-shared-instruction-driver/README.md).
