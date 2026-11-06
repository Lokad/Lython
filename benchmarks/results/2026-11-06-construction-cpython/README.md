# Construction checkpoint against CPython — 2026-11-06

Primary refresh COMPLETE, not a broad speedup claim. Committed794e552b includes
Q01 contextual key preparation, R02 active structural paths and R04 final tuple
backing. Frozen4011fcad was built before commit with1863cb3e assembly metadata;
all recorded source hashes match. R02/core867ebf7a is the source-pair reference.
[Identity](identity.json), [contract](contract.md), [raw evidence](observations.tgz),
[summary](summary.json), [assessment and adverse points](assessment.json).

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16; unchanged b94 worker/protocol2,
normalGC/tiering. Two independent identical current processes, R02 reference and
CPython. Five guest warmup seconds/role, seven rotated150ms rounds.12jobs327.85s;
quiet endpoints, full source/fixture/output/file goldens, Closed/final funding,
actual worker joins and RAM-fixture removal all pass. No live VM work remains.

## Compilation plus one execution in an already-running host

| Workload | Input | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.959–5.067 ms | 3.383 ms | 1.47–1.50× | Diagnostic; control 2.14%, IQR 1.61% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.447–2.562 ms | 1.290 ms | 1.90–1.99× | Variable; control 4.69%, IQR 1.20% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.800–4.841 ms | 4.206 ms | 1.14–1.15× | Diagnostic; control 0.86%, IQR 0.87% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 4.017–4.103 ms | 2.747 ms | 1.46–1.49× | Variable; control 2.13%, IQR 4.60% |

Ranges are two identical-role medians, not confidence intervals. Ratio is Lython
elapsed/CPython elapsed: above1 is slower. Control is absolute role difference
relative to candidate; IQR is the larger current-role IQR/median.

## Separate warm and compile observations

| Warm workload | Lython | CPython | Ratio | Control / IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4.698–4.734 ms | 3.203 ms | 1.47–1.48× | 0.78% / 0.54% |
| Text summary/helpers | 2.324–2.369 ms | 1.165 ms | 2.00–2.03× | 1.92% / 0.68% |
| JSON-lines/helpers | 4.629–4.686 ms | 4.091 ms | 1.13–1.15× | 1.22% / 0.44% |
| JSON-to-Markdown report | 3.751–3.826 ms | 2.594 ms | 1.45–1.47× | 1.99% / 4.63% |

| Compile-only script | Lython | CPython | Ratio | Control / IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 71.607–72.198 µs | 59.251 µs | 1.21–1.22× | 0.83% / 1.60% |
| Text summary/helpers | 53.231–53.388 µs | 41.528 µs | 1.28–1.29× | 0.29% / 1.71% |
| JSON-lines/helpers | 64.244–65.187 µs | 50.976 µs | 1.26–1.28× | 1.45% / 2.60% |
| JSON-to-Markdown report | 50.136–50.710 µs | 37.768 µs | 1.33–1.34× | 1.14% / 2.30% |

## Adverse and inconclusive results

Against R02 reference: fresh CSV+2.18–4.41%, fresh text+3.11–7.95%, JSON+0.86–1.73%,
report-0.53/+1.59%. Text's4.69% identical-role spread limits attribution; CSV's
2.14% spread also matters. Warm CSV-0.80–1.56%, text+0.34–2.30%, JSON+0.14–1.38%,
report-1.19–3.12% (variable). All exact numbers are in assessment.json.

Retain R02/R04 for measured compound-key gains, corrected/simple ownership paths
and deleted mechanisms, with possible primary fresh CSV/text regression as explicit
debt. Do not infer that these primary scripts improved or add the earlier source-
pair percentages. No further runtime patch follows solely from noisy controls.
Later justified refinement/controlled confirmation remains allowed; no permanent
closure. The next common text-discovery milestone must keep these original
conditions and separate warm execution from compile-to-result behavior.

Execution clocks include mediated RAM I/O, full validation and prior-result
cleanup. Fresh adds compilation/disposal; compile-only constructs/disposes without
guest execution or I/O. Startup/staging/reset/transport/final teardown excluded.
CPython imports persist; Lython isolates imports. Full sources/fixtures and all
measured responses are retained. Startup, genuinely suspended-I/O latency, RSS and
embedding latency remain unmeasured. Both installed Release consumers pass; final
platform/stress/package CI42170841788 on794e552b is pending, separate from timing.
Publication/tagging postponed; the complete-script performance objective stays open.
