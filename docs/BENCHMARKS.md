# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper |4,096 records /57,361 B|4.912–4.973 ms|3.436 ms|1.43–1.45×|Diagnostic;1.23% control /2.79% IQR|
| Text summary/helpers |3,072 lines /35,840 B|2.407–2.477 ms|1.336 ms|1.80–1.85×|Diagnostic;2.93% control /1.46% IQR|
| JSON-lines/helpers |4,096 records /110,592 B|4.710–4.744 ms|4.201 ms|1.12–1.13×|Diagnostic;0.71% control /1.28% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|3.771–3.913 ms|2.805 ms|1.34–1.40×|Diagnostic;3.63% control /5.03% IQR|

**2026-11-06;0.11development K01 source based ondb3d1908, frozen coreadbd1b6b;
CPython3.13.16; .NET11RC3.** These are **compilation plus one execution in an
already-running host**, including mediated RAM-file I/O, complete result/file
validation and prior-result cleanup. Process startup and fixture staging/reset are
excluded. **Ratio=Lython elapsed/CPython elapsed; above one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is their relative spread; IQR is interquartile range/median,
showing the larger Lython-role IQR. These short observations are diagnostic, with
report/control variation visible. They describe the frozen source/core, not later
HEAD. [Exact source, hashes, machine gates and raw evidence](../benchmarks/results/2026-11-06-canonical-key-storage/checkpoint/identity.json)
are retained with the [qualification and adverse results](../benchmarks/results/2026-11-06-canonical-key-storage/README.md).
Full local4207internal/16072public tests pass on actual.NET11/10; K01 cross-platform,
original stress and installed-consumer qualification is pending. Publication is postponed.

## Warm execution, compilation and startup

Same source, input and invocation/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper |4.836–4.939 ms|3.226 ms|1.50–1.53×|2.08% /0.81%|
| Text summary/helpers |2.245–2.299 ms|1.187 ms|1.89–1.94×|2.36% /0.96%|
| JSON-lines/helpers |4.406–4.587 ms|4.093 ms|1.08–1.12×|4.12% /0.50%|
| JSON-to-Markdown report |3.654–3.707 ms|2.629 ms|1.39–1.41×|1.46% /4.65%|

Compilation/program disposal alone; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation:

| Compile-only script | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper |74.59–74.82µs|62.02µs|1.20–1.21×|0.31% /2.54%|
| Text summary/helpers |53.93–55.61µs|41.40µs|1.30–1.34×|3.03% /6.16%|
| JSON-lines/helpers |63.87–65.45µs|50.86µs|1.26–1.29×|2.47% /0.69%|
| JSON-to-Markdown report |50.35–50.63µs|37.89µs|1.33–1.34×|0.56% /2.87%|

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** Warm CSV/text are still about1.5×/1.9×CPython. Their observed warm
regressions versus P01 remain in the evidence; fresh results differ, so there is
no broad speedup claim.

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Twelve jobs completed in328.85s; all workers joined and fixtures were removed.

Both engines run the same scripts and fixtures, checking complete output/file
digests on every invocation. Efficient host overrides mediate Lython file access;
RAM fixtures include reads/writes, while staging/reset is outside elapsed clocks.
Warm includes invocation and prior-result cleanup. Compile includes construction/
disposal; compile-to-result also executes a fresh script. Transport and final
program/result/process/fixture teardown are outside clocks. CPython retains imported
modules between invocations; Lython isolates execution imports. This is an embedding
comparison, not a claim that the implementations or import caching are identical.

## Improvements within Lython

CPython remains the primary external baseline above. Separate source pairs show:

- Prepared patterns: record warm−27–29%, compile-to-result−25–30%; compile/text
  inconclusive. [P01 evidence](../benchmarks/results/2026-11-06-prepared-patterns/README.md).
- Canonical key storage: set bulk−47%, dataclass keys−30–31% in the bounded
  refinement; substantial machinery removed and valid custom hashes corrected.
  Warm CSV/text adverse observations remain; final fresh CSV is flat/mixed,
  text improves5–8%, JSON+1–2% is small and report is variable.
  [K01 evidence](../benchmarks/results/2026-11-06-canonical-key-storage/README.md).
- Native helper returns: allocation−19.92%/−19.31%; elapsed observations remain
  diagnostic. UTF-8 formatting allocation−8.67%; controlled elapsed/CPU evidence
  supports retention while the original adverse timing remains unexplained.

These are different source pairs and workloads; percentages are not additive or
claims about every script. Further complete-script performance work remains active.

## Historical measurements and unresolved workloads

The following were not refreshed to K01 and do not describe its performance:

| Workload | Earlier revision/boundary | Lython / CPython | Status |
| --- | --- | ---: | --- |
| CSV/native helpers |13d69be6; RAM; compile-to-result|1.27–1.29×|Diagnostic|
| Config read/edit/write;1,024 paths |13d69be6; RAM; compile-to-result|1.03–1.04×|Variable|
| CSV long fields;16records/524,717B |13d69be6; RAM; compile-to-result|0.14×|Variable; does not describe ordinary CSV|
| Search with early exit;42,010B |bd3f9dee; ordinary filesystem|2.09×|Diagnostic|
| Unicode/ASCII filename filter;768names |bd3f9dee; ordinary filesystem|2.14×/2.70×|Variable/diagnostic; filters supplied names, not768files|
| Copy/checksum/manifest;4,107B |bd3f9dee; ordinary filesystem|1.69×|Diagnostic|

The [earlier retained checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-refresh/README.md)
keeps the previous four-script results, full data and closed qualification. Earlier
90785737 search remains inconclusive (fresh270%IQR/218% control drift, warm138%/77%):
[historical evidence](../benchmarks/HISTORY.md). Native process-varying hashes/buckets
can plausibly influence small-role differences; causation in these runs is unproven.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../benchmarks/README.md)
describes existing qualification procedures; [tests](../tests/README.md) cover
semantic equivalence, ownership, cancellation and installed consumers. No timing
claim establishes containment or matching cancellation by itself.
