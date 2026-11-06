# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.959–5.067 ms | 3.383 ms | 1.47–1.50× | Diagnostic; control 2.14%, IQR 1.61% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.447–2.562 ms | 1.290 ms | 1.90–1.99× | Variable; control 4.69%, IQR 1.20% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.800–4.841 ms | 4.206 ms | 1.14–1.15× | Diagnostic; control 0.86%, IQR 0.87% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 4.017–4.103 ms | 2.747 ms | 1.46–1.49× | Variable; control 2.13%, IQR 4.60% |

**2026-11-06; 0.11 development source `794e552b`; CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython elapsed; above
one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is their absolute difference relative to candidate; IQR is the
larger Lython-role interquartile range/median. Fresh text and report remain variable.
These results describe the recorded source, not future HEAD.
[Identity and raw evidence](../benchmarks/results/2026-11-06-construction-cpython/identity.json),
[adverse results and qualification scope](../benchmarks/results/2026-11-06-construction-cpython/README.md).

## Warm execution, compilation and startup

Same source/input and execution/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4.698–4.734 ms | 3.203 ms | 1.47–1.48× | 0.78% / 0.54% |
| Text summary/helpers | 2.324–2.369 ms | 1.165 ms | 2.00–2.03× | 1.92% / 0.68% |
| JSON-lines/helpers | 4.629–4.686 ms | 4.091 ms | 1.13–1.15× | 1.22% / 0.44% |
| JSON-to-Markdown report | 3.751–3.826 ms | 2.594 ms | 1.45–1.47× | 1.99% / 4.63% |

Compilation/program disposal alone; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation:

| Compile-only script | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 71.607–72.198 µs | 59.251 µs | 1.21–1.22× | 0.83% / 1.60% |
| Text summary/helpers | 53.231–53.388 µs | 41.528 µs | 1.28–1.29× | 0.29% / 1.71% |
| JSON-lines/helpers | 64.244–65.187 µs | 50.976 µs | 1.26–1.28× | 1.45% / 2.60% |
| JSON-to-Markdown report | 50.136–50.710 µs | 37.768 µs | 1.33–1.34× | 1.14% / 2.30% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** There is no broad primary-workload speedup. Against R02, fresh CSV
is2.18–4.41% slower and fresh text3.11–7.95% slower; identical text roles differ4.69%.
These are explicit possible regression debt, not evidence of improved CPU time.
[All paired/adverse observations](../benchmarks/results/2026-11-06-construction-cpython/assessment.json)
remain visible. Report is variable; cross-block CPython timings also change.

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Twelve jobs completed327.85s; all workers joined and fixtures were removed.

Both engines run identical scripts/fixtures, checking complete output/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include reads/writes; staging/reset is outside elapsed clocks. Warm
includes invocation and prior-result cleanup. Compile includes construction/
disposal; compile-to-result also executes a fresh script. Transport and final
program/result/process/fixture teardown are outside clocks. CPython retains imported
modules between invocations; Lython isolates execution imports. This is an embedding
comparison, not a claim of identical implementations or import caching.

These primary tables use `Run`. The separate [async hash/read diagnostic](../benchmarks/results/2026-11-06-async-key-reads/README.md)
uses `RunAsync` with completed mediated I/O and records a3.72–5.13% text regression
against51db4209. This remains explicit performance debt; it measures no genuinely
pending-I/O latency or broad CPU improvement.

## Improvements within Lython

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or broad CPU claims:

| Change / affected workload | Measured result | Limits / evidence |
| --- | --- | --- |
| Contextual key preparation / compound-key aggregation | Elapsed −15.5%/−20.5% versus F05 | CSV roughly flat; text uncertainty. [Q01](../benchmarks/results/2026-11-06-key-preparation/README.md) |
| Active structural path / compound-key aggregation | Elapsed −14.8–17.1% versus Q01 | CSV roughly flat; text control-limited. [R02](../benchmarks/results/2026-11-06-structural-active-path/README.md) |
| Final tuple backing / compound-key aggregation | Elapsed −6.17–12.29% versus R02 | CSV inconclusive; no process allocation percentage. [R04](../benchmarks/results/2026-11-06-executable-tuple-backing/README.md) |
| Ordinary method operands / method-heavy CSV | Allocation −7.26–7.28%; warm elapsed −3.67–4.61% | Separate clock boundaries; controls also improve. [F05](../benchmarks/results/2026-11-06-direct-method-operands/README.md) |

Native helper returns reduced process allocation19.92%/19.31%; elapsed remains
diagnostic. Direct UTF-8 formatting reduced allocation8.67%; controlled elapsed/CPU
supports retention, while the original adverse result remains unexplained.
[Full experimental evidence](../benchmarks/EXPERIMENTS.md) preserves earlier
source pairs, rejected designs and uncertainty. Substantial whole-script gaps
remain; the primary CSV/text regressions above are explicit debt.

## Historical measurements and unresolved workloads

The following were not refreshed to794e552b and do not describe its performance:

| Workload | Earlier revision/boundary | Lython / CPython | Status |
| --- | --- | ---: | --- |
| CSV/native helpers |13d69be6; RAM; compile-to-result|1.27–1.29×|Diagnostic|
| Config read/edit/write;1,024 paths |13d69be6; RAM; compile-to-result|1.03–1.04×|Variable|
| CSV long fields;16records/524,717B |13d69be6; RAM; compile-to-result|0.14×|Variable; does not describe ordinary CSV|
| Search with early exit;42,010B |bd3f9dee; ordinary filesystem|2.09×|Diagnostic|
| Unicode/ASCII filename filter;768names |bd3f9dee; ordinary filesystem|2.14×/2.70×|Variable/diagnostic; filters supplied names, not768files|
| Copy/checksum/manifest;4,107B |bd3f9dee; ordinary filesystem|1.69×|Diagnostic|

The [previous call/frame checkpoint](../benchmarks/results/2026-11-06-call-frame-cpython/README.md)
retains the1134d6e8 results and qualification. The [earlier retained checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-refresh/README.md)
keeps the previous four-script results, full data and closed qualification. Earlier
90785737 search remains inconclusive (fresh270%IQR/218% control drift, warm138%/77%):
[historical evidence](../benchmarks/HISTORY.md). Native process-varying hashes/buckets
can plausibly influence small-role differences; causation in these runs is unproven.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../benchmarks/README.md)
describes existing qualification procedures; [tests](../tests/README.md) cover
semantic equivalence, ownership, cancellation and installed consumers. No timing
claim establishes containment or matching cancellation by itself.
