# Direct CPython baseline for complete helper workloads

R466/B872,2026-11-17UTC. Committedccf20ba3 / frozen7a599347 versus stock
CPython3.13.16. Only the two new helper-list text/report cases are measured;
previous CSV,generator normalization,JSON,search and older config/report evidence
remain unchanged. Same fixed eleven-case catalog as the receiver source pair.
Both frozen images/local isolated CPython already pass all11goldens; reused
candidate assets/catalog are verified exactly. Each VM job additionally verifies
the actual CPython3.13.16 and core/output/file identities before timing.

| Complete script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Text normalization through a helper | compile | 0.047–0.047 ms | 0.044–0.044 ms | 1.05–1.06× | Diagnostic; controls L/C 0.36/0.24%; IQR≤1.01% |
| Text normalization through a helper | warm | 1.236–1.250 ms | 0.872–0.905 ms | 1.37–1.43× | Diagnostic; controls L/C 1.15/3.76%; IQR≤2.39% |
| Text normalization through a helper | compile-run | 1.345–1.394 ms | 0.795–0.802 ms | 1.68–1.75× | Diagnostic; controls L/C 3.65/0.82%; IQR≤1.93% |
| Report rendering through a helper | compile | 0.053–0.054 ms | 0.049–0.049 ms | 1.08–1.10× | Diagnostic; controls L/C 1.69/0.19%; IQR≤0.74% |
| Report rendering through a helper | warm | 2.655–2.685 ms | 2.200–2.220 ms | 1.20–1.22× | Diagnostic; controls L/C 1.13/0.92%; IQR≤4.68% |
| Report rendering through a helper | compile-run | 2.850–2.944 ms | 2.432–2.481 ms | 1.15–1.21× | Diagnostic; controls L/C 3.30/2.03%; IQR≤4.68% |

Above one means slower. Four independent-process median pairings are ranges,not
confidence intervals. Warm helper-text1.37–1.43× retains a gap; CPython control
moves3.76%. Report1.20–1.22× is slower,IQRup to4.68%. Compile-to-result text
1.68–1.75×,report1.15–1.21×; Lython controls3.65/3.30%move. Compile-only
ratios1.05–1.06×text/1.08–1.10×report. No subtraction of separately warmed phases
or attribution of the fresh-text gap to compilation alone. All adverse/moving
results remain; no preferred-sign retiming. CPython helper-text fresh0.795–0.802ms is also below its warm0.872–0.905ms;
the ratio alone does not establish a Lython setup defect. The phase difference
motivates bounded cross-engine first-use/cleanup attribution,not an assumed cause.

These are ordinary additional scripts: a helper appends each rstripped line,or
each pre-rendered record,before joining/writing. Original inputs/renderer/stdout/
file goldens are reused. Identical source/fixture in both engines. They are not
causal comparisons to earlier generator/streaming shapes or to older Lython
collections. Separate R464source-pair gains cannot be multiplied into CPython
ratios. No async/startup,pending-I/O,RSS,contention or embedding-tail comparison.

Six jobs203.837s,two independent processes per engine,5s full-request warmups,
five rotated approximately0.5s samples/process. Quiet AzureF4as_v7/Ubuntu24.04.4,
four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural workstation GC/normal tiering,
CPythonGIL/GC. Existing workers/protocol/tooling,no new core copy. Warm includes
synchronous Run,mediated RAM I/O,complete stdout/file checks and prior-result
cleanup; excludes compilation/startup,fixture reset/protocol and final cache
teardown. Compilation includes compiled-owner disposal;fresh adds execution.
Wider CPU includes reset/protocol. CPython uses native buffered files without
equivalent capability/shared-budget containment. Independently warmed phases
are not additive. Full combined runtime delivery is tracked separately.

All identities/goldens/quiet/Closed/exit/join/archive gates pass. All users joined;
RAMfixtures removed after persistent archive SHAverification under lease. Only
one exact committed old receiver-ops raw JSON replica1,400,532Bretired after fresh
UID/path/hash/nlink/all-inode-alias proof. Current images/catalogs/toolchains and
canonical evidence retained;254/252MiB staging/final floors preserved. Terminal
cached permanently. Publication/tagging held; whole-script roadmap continues.

R465bounded record-storage source gate is also retained here: existing exact-key
updates already avoid shared-layout copying. A repeated pure probe alone gives
no demonstrated allocation/copy stage to remove. No new container prototype or
unchanged timing campaign; no permanent design ban. Existing async mutation
limitations stay documented,not reclassified as a new fixed bug.

[Summary](summary.json),[identities](identity.json),[raw observations](observations.tgz),
[procedures/catalog/custody](procedures.tar.xz),[preceding guide](prior-BENCHMARKS.md),
[record source gate](record-source-assessment.json),[receiver source pair](../2026-11-17-receiver-calls/README.md).

The historical report fixture has3,072decoded rows although its generation size
parameter is1024. The current guide corrects that count without changing old
timings; immutable earlier guides remain untouched. [Fixture proof](historical-report-input-proof.json).
