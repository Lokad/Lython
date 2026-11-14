# Native-eligibility CPython checkpoint

2026-11-14UTC, exact committed1778c30d / frozen38c0562f,581production inputs,
stockCPython3.13.16, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. The combined
R338/R339 full platform/stress/installed-consumer[delivery43545993574](https://github.com/Lokad/Lython/actions/runs/43545993574)
is pending at collection; focused637internal/1562public per actual11/10 pass.
The separate R341prototype is not part of these sources or observations.
Publication/tagging remain postponed.

| Complete script / phase | Lython | CPython | Lython / CPython | Lython/CPython process spread | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
|csv-invoice.large /warm|7.399542–7.489581ms|6.030762–6.138508ms|1.21–1.24×|1.22/1.79%|1.09%|
|csv-invoice.large /compile|0.101212–0.101836ms|0.092635–0.092850ms|1.09–1.10×|0.62/0.23%|1.25%|
|csv-invoice.large /compile-run|7.585361–8.037213ms|6.291825–6.303117ms|1.20–1.28×|5.96/0.18%|0.85%|
|text-summary-helpers.nonlocal-counter.large /warm|1.352806–1.448032ms|0.890669–0.894701ms|1.51–1.63×|6.58/0.45%|1.90%|
|json-summary-helpers.large /warm|3.370526–3.406941ms|4.084995–4.090377ms|0.82–0.83×|1.08/0.13%|0.83%|
|file-search.large /warm|0.263980–0.268713ms|0.159066–0.160740ms|1.64–1.69×|1.79/1.04%|2.54%|
|report-render.large /warm|2.771066–2.799524ms|2.311578–2.377912ms|1.17–1.21×|1.02/2.79%|4.23%|
|csv-fulfillment.large /warm|8.647809–8.869225ms|6.695523–6.727629ms|1.29–1.32×|2.50/0.48%|1.62%|

Ratio is Lython elapsed divided by CPython elapsed; above one means slower.
These are diagnostic complete embedding jobs, not isolated interpreter CPU.
Invoice fresh has5.96%Lython process spread; captured text6.58%. Keep their
wider ranges/inconclusive stability visible; low within-process IQR does not
remove this disagreement. Report has2.79%CPython spread/maxIQR4.23%. No automatic
design veto, control neutrality or attribution from older collections is inferred.

The previous five representative sources/fixtures/goldens are unchanged. Add the
4096-row/nine-field fulfillment/pricing/aggregation/report job used in R339; local
isolatedCPython3.13.2 supplies independent file/stdout goldens, and every VM engine
verifies them before timing. The fixed B685.NET worker, Python protocol worker and
optional0.4adapter retain identical bytes. No core producer dependency or pin change.

Two fresh processes per engine/job, five rotating0.5s batches sized by full-request
wall time; five-second full-request warmups, twelve seconds for captured text/report.
Ordinary GC/tiering, stock Python GIL/GC; no performance overrides. Warm guest clocks
include mediated RAM-file I/O, complete stdout/file validation and prior-result
cleanup, excluding compilation/startup, reset/protocol and final cache teardown.
Compilation measures frontend/compiled owner disposal separately. Compile-to-result
adds compilation/disposal to execution. Wider CPU includes reset/protocol. These
independently warmed phase medians cannot be added/subtracted as attribution.
No startup, RSS, shared-budget contention, genuine pending I/O or embedding-tail claim.

B710eight jobs finish327.287s within one420s owner. All exact image/
source/golden/allowance/quiet/join/archive gates pass; no workers or RAM fixtures
remain. Persistent rawSHA4f8b7cc929a5db73d07e4e40ca39c43996be5885e67fbc9ab78ff746110f3078. All adverse observations,
warmup responses and controls survive. No floor relaxation, evidence retirement,
framework expansion or repeated unchanged qualification. The first source remains
independently[archived](../2026-11-14-native-owned-slots/source-checks.tar.xz).

[Raw and wider CPU](observations.tgz), [summary/identities](summary.json),
[collection tools](collection-tools.tar.xz), [previous guide](prior-BENCHMARKS.md).
This checkpoint informs subsequent removals; it does not complete the performance objective.
