# Generator sum: current CPython checkpoint

B887,2026-11-17UTC. **abcbd9fd / frozenca1a8239 versus stock CPython3.13.16**.
Four separated jobs137.80s; no causal comparison with earlier collections.

| Complete script | Phase | Lython | CPython | Lython / CPython | L/C control spread; maximum IQR |
| --- | --- | ---: | ---: | ---: | --- |
| Floating JSON numeric report | Compilation | 0.0561–0.0562ms | 0.0609–0.0614ms | 0.91–0.92× | 0.19/0.83%;0.80% |
| Same | Warm | 1.614–1.633ms | 0.817–0.826ms | **1.95–2.00×** | 1.22/1.10%;1.28% |
| Same | Compile and execute once | 1.771–1.813ms | 0.965–0.966ms | **1.83–1.88×** | 2.39/0.15%;0.52% |
| CSV invoice | Warm | 6.923–7.151ms | 6.058–6.106ms | **1.13–1.18×** | **3.29**/0.79%;1.03% |

Ratio above one means slower. Floating input4,096paired records/56,495B;
CSV4,096records/127,031B. Complete source/input/output goldens are in the catalog.
Ranges pair two independent process medians/engine, not confidence intervals.
CSV's Lython control moves3.29%; retain uncertainty. No source-change gain or
regression debt closure inferred from earlier ratios.

Quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,16GiB/no swap; .NET11RC3
natural workstation GC/normal tiering, CPythonGIL/GC. Interleaved5s full-request
warmups, five rotated approximately0.5s samples. Warm includes synchronous Run,
mediated RAM I/O, complete stdout/file goldens and prior-result cleanup; excludes
compile/startup/reset/protocol/final cache teardown. Fresh adds compile/compiled
owner disposal; compile includes compiled-owner disposal. Wider CPU includes more
protocol/reset work; phase medians are not additive. Native CPython buffered files
have no equivalent capability/shared-budget checks. No startup, async suspension,
RSS, contention or embedding-tail comparison.

Both frozen32file images passed actual-worker/CPython2case goldens before staging.
All identities/outputs/quiet/Closed/exits/joins/archive checks pass, RAM removed and
terminal cached. [Full runtime delivery44041981387 passes](../delivery/README.md). Initial
six-job catalog staging was refused for disk headroom **before mutation or workers**;
its error/configuration is preserved. A smaller two-case/four-job collection fills
material gaps. No timing was repeated to repair that staging failure. Exact
committed replica proofs retired843,980B; present/absent paths and all inode
aliases were checked, preserving images/toolchains/canonicals and254/252MiBfloors.
Earlier text/report/configuration/search measurements keep their source labels.
Publication remains postponed.

[Summary](summary.json),[catalog](representative.json),[identity](identity.json),
[raw observations](observations.tgz),[custody](archive-receipt.json),
[procedures/checks/original staging failure](procedures-and-checks.tar.xz).
