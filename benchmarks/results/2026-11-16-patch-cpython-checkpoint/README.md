# Current patch-generation CPython checkpoint

R421/B816,2026-11-16UTC. Runtime **c023042d / measured0393331a**,
source/evidence head09bf8cbb, versus stock **CPython3.13.16**. Runtime already
[fully qualified](../2026-11-15-runtime-format-operation/delivery/README.md);
no production edit, new backend qualification or source speedup is claimed.

Existing complete patch script reads two1,024-line UTF8 Python files (67,597B),
generates unified_diff and writes change.patch. One central replacement between
unique lines; independent CLR-constructed exact patch/stdout goldens reused from
[R229](../2026-11-11-patch-workflow/README.md), not new arbitrary-diff coverage.
Current patch source hash matches the committed maintained script. CSV invoice
control uses4,096records/127,031B. Original private setup assertion found that the
reduced recent catalog omits patch: before remote work, reuse its existing canonical
case plus the CSV case in a two-case catalog. No fixture/golden/worker redesign.

| Workload / phase | Lython ms | CPython ms | Lython/CPython | L/P control spread | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| Patch / warm Run | 0.471–0.472 | 0.572–0.576 | 0.817–0.825× | 0.14/0.72% | 1.37% |
| Patch / compilation only | 0.033–0.033 | 0.034–0.034 | 0.96–0.97× | 0.49/0.81% | 1.37% |
| Patch / compile and execute once | 0.521–0.534 | 0.651–0.654 | 0.80–0.82× | 2.37/0.57% | 2.08% |
| CSV / warm Run | 6.718–6.917 | 6.017–6.071 | 1.11–1.15× | 2.95/0.88% | 1.99% |

All four pairings of process medians, not confidence intervals. Four jobs complete
in **137.167s**, two processes per engine,5s full-request warmups,
five rotated approximately0.5s rounds. Keep every moving/adverse control and IQR;
this is diagnostic complete embedding cost, not isolated matcher/interpreter CPU.
Quiet Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45vCPUs/16GiB/no swap,.NET11RC3
ordinary tiering/natural workstationGC, CPython GIL/GC.

Warm includes synchronous Run, mediated RAM-file I/O, full stdout/file validation
and prior-result cleanup; excludes compile/startup/reset/protocol/finalcache teardown.
Compilation includes compiled-owner disposal; fresh includes compilation, execution
and compiled-owner disposal. Separate medians are not additive. Wider process CPU
includes reset/protocol. CPython native buffered files provide different containment.
No pending-I/O, contention, heap/RSS, startup or embedding-tail comparison.

This fills a current useful-workload gap: patch warm0.817–0.825× and fresh0.797–0.820×
CPython do not establish a performance problem for this fixture. CSV1.107–1.149×
has2.95%Lython control movement; retain it without inferring a source regression.
Do not infer a causal improvement over R229
or select a matcher redesign from private Python-facing collections alone. The
single-edit fixture does not establish arbitrary diff throughput. Call/operand and
captured-text questions remain separate; broader performance work remains open.

All image/catalog/golden/budget/quiet/Closed/exit/join/archive gates pass. Persistent
archive hash-verifies under the lease; all workers and RAM fixtures are gone.
No retirement or floor change; current images/toolchains and254/252MiB floors remain.
Terminal cached forever. [Summary/all controls](summary.json),[identity](identity.json),
[raw observations](observations.tgz),[procedure/inputs](procedure-and-inputs.tar.xz),
[previous guide](prior-BENCHMARKS.md). Publication held.
