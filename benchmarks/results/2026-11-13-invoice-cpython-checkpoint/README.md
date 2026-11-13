# Qualified-source invoice and representative checkpoint

Collected 2026-11-13 UTC on the quiet Azure F4as_v7, Ubuntu24.04.4, four AMD
EPYC9V45 vCPUs,16GiB/no swap. Qualified runtime63389c9d/frozen2e74ef4f,
CPython3.13.16, ordinary.NET11RC3 GC/tiering; common workerfec79afd and adapter0.4.
All six jobs finish250.895s within the360s owner. Identity, complete stdout/file
goldens, shared-budget cleanup, quiet/join and persistent SHA-archive gates pass;
no remaining workers or RAM fixtures. [Source-matched full delivery](../2026-11-13-native-call-stack-handoff/delivery/README.md).

| Workload / phase | Lython ms | CPython ms | Lython / CPython | L/P control spread; max IQR |
| --- | ---: | ---: | ---: | --- |
| Invoice warm |8.788–8.819|6.038–6.075|1.45–1.46×|0.36/0.62%;1.30%|
| Invoice compilation |0.1024–0.1033|0.0929–0.0935|1.10–1.11×|0.85/0.63%;1.49%|
| Invoice compile + run |8.914–9.067|6.288–6.305|1.41–1.44×|1.71/0.26%;1.25%|
| Captured text warm |1.477–1.478|0.892–0.893|1.65–1.66×|0.10/0.12%;2.44%|
| JSON helpers warm |3.410–3.410|4.084–4.165|0.82–0.83×|0.001/1.97%;1.69%|
| File search warm |0.257–0.263|0.157–0.162|1.59–1.67×|2.44/2.70%;3.10%|

Invoice is4096records/127031bytes. Warm includes execution in a reused compiled
script, mediated RAM-file I/O, output validation and prior-result cleanup.
Compilation includes construction/disposal under the shared allowance. Fresh adds
compilation and compiled-owner disposal; startup, fixture reset/staging, protocol
and final cache teardown are excluded. Phase medians need not add. Captured input
3072lines/35840bytes; JSON4096records/110592bytes; search2049lines/42010bytes,
stopping after1025. Identical scripts/fixtures, complete output checks per invocation.

Five rotating500ms rounds and5s warmups (12s captured); two independent processes
per engine. Ranges span process medians, ratios all four pairings, not confidence
intervals. Search remains variable. Wider process CPU also includes reset/protocol;
it cannot isolate guest CPU. No startup, pending-I/O or embedding-latency claim.

Separately, the invoice warm job includes qualified earlierfc2531b8/6b468bf1 and
its independent control: corrected2e improves elapsed8.38–10.72% and wider CPU
8.81–10.71%. Reference control spread2.26%. This qualifies the shallow composed-call
gain on corrected sources. Allocation−19.51% was measured on earlier2f34a48f;
it is not silently relabelled as a current2e measurement. Captured text still has a
material external gap. Its older source-pair adversity remains unresolved; comparing
separate external checkpoints cannot attribute that debt to a particular change.

Raw originals, adverse controls, identities and worker responses remain in
[observations](observations.tgz); [computed summary](summary.json), [collector](summarize.py)
and [archive receipt](archive-receipt.json). The broader performance objective and
independent regex work remain active. Publication is postponed.
