# Consolidated-runtime representative CPython checkpoint

R451/B854,2026-11-16UTC,committed R45081f60210/frozen a44f65a8 versus stock
CPython3.13.16. Production source matches the final cursor-consolidation snapshot;
its full delivery qualification is recorded separately. Complete CSV,text,JSON
and early-exit file scripts use the existing six-case catalog36c0a528. Only these
four cases are measured; configuration/report retain their named earlier evidence.
Both frozen-worker and CPython preflight pass all six cases before staging.

| Script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | compile | 0.099–0.100 ms | 0.094–0.094 ms | 1.06–1.06× | Diagnostic; controls L/C 0.19/0.23%; IQR≤1.27% |
| CSV invoice | warm | 6.790–6.809 ms | 6.019–6.064 ms | 1.12–1.13× | Diagnostic; controls L/C 0.29/0.74%; IQR≤0.62% |
| CSV invoice | compile-run | 6.920–6.994 ms | 6.213–6.316 ms | 1.10–1.13× | Diagnostic; controls L/C 1.07/1.67%; IQR≤0.92% |
| Text normalization | compile | 0.035–0.036 ms | 0.037–0.038 ms | 0.94–0.97× | Diagnostic; controls L/C 1.69/1.37%; IQR≤1.96% |
| Text normalization | warm | 1.125–1.140 ms | 0.508–0.564 ms | 2.00–2.24× | Diagnostic; controls L/C 1.31/10.85%; IQR≤1.32% |
| Text normalization | compile-run | 1.215–1.257 ms | 0.597–0.613 ms | 1.98–2.11× | Diagnostic; controls L/C 3.48/2.64%; IQR≤0.95% |
| JSON-lines aggregation | compile | 0.054–0.054 ms | 0.053–0.053 ms | 1.02–1.03× | Diagnostic; controls L/C 0.39/0.34%; IQR≤4.14% |
| JSON-lines aggregation | warm | 3.456–3.458 ms | 4.087–4.100 ms | 0.84–0.85× | Diagnostic; controls L/C 0.05/0.33%; IQR≤0.66% |
| JSON-lines aggregation | compile-run | 3.590–3.609 ms | 4.177–4.312 ms | 0.83–0.86× | Diagnostic; controls L/C 0.55/3.22%; IQR≤3.80% |
| File search / defensive helper | compile | 0.031–0.031 ms | 0.036–0.036 ms | 0.85–0.86× | Diagnostic; controls L/C 0.16/1.14%; IQR≤1.05% |
| File search / defensive helper | warm | 0.275–0.283 ms | 0.171–0.176 ms | 1.56–1.65× | Diagnostic; controls L/C 2.79/2.98%; IQR≤1.15% |
| File search / defensive helper | compile-run | 0.363–0.372 ms | 0.237–0.245 ms | 1.48–1.57× | Diagnostic; controls L/C 2.44/3.18%; IQR≤0.76% |

Above one means Lython is slower. Four independent-process median combinations
are ranges,not confidence intervals. Normalization CPython copies move10.85%,
making the precise warm ratio uncertain; both still show a substantial gap.
CSV remains slower and JSON faster in this fixture; search has2.79/2.98%control
movement. JSON fresh CPython moves3.22%,normalization fresh Lython3.48%.
All adverse pairings,IQRs,raw observations and earlier measurements are retained.
These are external ratios,not causal gains from previous Lython source revisions.

Twelve jobs400.971s,two independent processes per engine,5s full-request warmups
and five rotated approximately0.5s samples. Identical catalog bytes in all jobs;
candidate image32files frozen after nonincremental build,corea44f65a8,normal
optional adapter0.4.0. Reused immutable image verified before/after; no worker or
fixture change. Quiet AzureF4as_v7/Ubuntu24.04.4,four AMD EPYC9V45vCPUs,16GiB/no
swap,.NET11RC3normal tiering/workstation GC,CPython GIL/GC. Image/Ready/golden/
quiet/Closed/exit/join/archive gates pass. No runtime changes for this checkpoint.

Warm includes synchronous Run,mediated RAM I/O,complete stdout/file checks and
prior-result cleanup. Excludes compilation/startup,fixture reset/protocol and
final cache teardown. Compile includes construction/compiled-owner disposal;
compile-to-result adds one execution and disposal. Independently warmed phases
are not additive. Wider process CPU includes reset/protocol. CPython uses native
buffered files without equivalent host-capability/shared-budget containment.
No startup,pending-I/O,heap/RSS,contention or embedding-tail claim.

All users joined/RAM fixtures removed; archive SHAverified persistently under
lease before teardown. Terminal cached forever. Only B852/B853working raw JSON
replicas,total978,877B,were retired after equality to committed canonical archive
members plus fresh UID/path/hash/nlink/all-alias proof. All adverse canonicals,
selected images/toolchains and254/252MiB staging/final floors remain.

The current guide refreshes four workloads and their phases. Earlier config/report
rows remain identified as312f5911/0443b5cf. Earlier normalization CPython0.73→0.50ms
shift remains unexplained; this collection's moving unchanged CPython control is
additional uncertainty,not its explanation. No automatic repeat to obtain a
preferred sign. Performance work continues; publication/tagging remain held.

[Summary](summary.json),[exact images/conditions](identity.json),
[all raw observations](observations.tgz),[procedures/preflight/custody](procedures.tar.xz),
[preceding guide](prior-BENCHMARKS.md),[runtime source/evidence](../2026-11-16-native-consumer-consolidation/README.md).
