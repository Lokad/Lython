# Numeric CSV report against CPython

B891,2026-11-17UTC. **d8611c85 / frozen0315de0a**, stock **CPython3.13.16**.
[Full runtime delivery passes](../delivery/README.md); measurement is diagnostic.
Complete script parses4,096records/127,031B, converts numeric fields in both module
and helper code, aggregates values and writes a text report with exact goldens.

| Phase | Lython | CPython | Lython / CPython | L/C control spread; maximum IQR |
| --- | ---: | ---: | ---: | --- |
| Compilation | 0.0561–0.0563ms | 0.0443–0.0449ms | 1.25–1.27× | 0.26/1.18%;0.95% |
| Warm execution | 4.120–4.198ms | 4.573–4.608ms | **0.89–0.92×** | 1.89/0.79%;0.65% |
| Compile and execute once | 4.284–4.412ms | 4.633–4.643ms | **0.92–0.95×** | **2.98**/0.21%;0.96% |

Ratio below one means lower elapsed. Two independent processes/engine; ranges
pair process medians,not confidence intervals. Fresh Lython controls move2.98%:
retain uncertainty. This does not attribute the external ratio to R477 or imply
a universal Lython advantage. Its separate source pair shows allocation−5.26%,
small confirmed warm improvement and essentially flat fresh timing.

Three separated jobs104.51s; quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,
16GiB/no swap. .NET11RC3 natural workstation GC/normal tiering, CPythonGIL/GC.
Interleaved5s full-request warmups,five rotated approximately0.5s samples. Warm
includes synchronous Run,mediated RAM I/O,complete stdout/file goldens and prior-
result cleanup; excludes compile/startup/reset/protocol/final cache teardown.
Fresh adds compile/compiled-owner disposal; compile includes compiled-owner
disposal. Wider CPU includes reset/protocol; phase medians are not additive.
CPython native buffered files have no equivalent capability/shared-budget checks.
No startup,async suspension,RSS,contention or embedding-tail claim.

Existing actual-worker-verified3case catalog and copied32file image reused. All
identity/golden/quiet/Closed/exit/join/archive checks pass; RAM removed and terminal
cached. No staging/timing failure in this collection. Fresh UID/path/hash/link/
all-alias proof retires only465,169Bof exact committed working raw/archive/
bytecode-cache replicas, preserving both measured cores/toolchains/canonicals and
254/252MiBfloors. Publication and tagging remain postponed.

[Summary](summary.json),[catalog/source/goldens](../catalog.json),
[raw observations](observations.tgz),[identity](identity.json),
[custody](archive-receipt.json),[procedures/checks](procedures-and-checks.tar.xz).
