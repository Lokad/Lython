# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.881–4.914 ms | 3.377 ms | 1.45–1.46× | Diagnostic; control 0.67%, IQR 2.22% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.382–2.388 ms | 1.260 ms | 1.89× | Diagnostic; control 0.25%, IQR 1.29% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.646–4.721 ms | 4.263 ms | 1.09–1.11× | Diagnostic; control 1.62%, IQR 0.93% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.747–3.788 ms | 2.476 ms | 1.51–1.53× | Variable; control 1.10%, IQR 7.49% |

**2026-11-06; 0.11 development source `1134d6e8`; CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete result/file validation and prior-result cleanup.
Startup and fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython
elapsed; above one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is the absolute difference relative to the candidate median;
IQR is interquartile range/median, showing the larger Lython-role IQR. Report
variation remains visible. Results describe the recorded source and binary,
not later HEAD. [Identity and raw evidence](../benchmarks/results/2026-11-06-call-frame-cpython/identity.json),
[qualification and adverse results](../benchmarks/results/2026-11-06-call-frame-cpython/README.md).

## Warm execution, compilation and startup

Same source, input and invocation/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4.632–4.700 ms | 3.200 ms | 1.45–1.47× | 1.43% / 1.36% |
| Text summary/helpers | 2.212–2.257 ms | 1.188 ms | 1.86–1.90× | 1.98% / 0.60% |
| JSON-lines/helpers | 4.515–4.573 ms | 4.162 ms | 1.08–1.10× | 1.30% / 1.00% |
| JSON-to-Markdown report | 3.690–3.719 ms | 2.607 ms | 1.42–1.43× | 0.78% / 5.10% |

Compilation/program disposal alone; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation:

| Compile-only script | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 72.404–73.704 µs | 59.027 µs | 1.23–1.25× | 1.76% / 0.56% |
| Text summary/helpers | 53.579–54.377 µs | 41.381 µs | 1.29–1.31× | 1.49% / 0.63% |
| JSON-lines/helpers | 63.435–64.456 µs | 51.213 µs | 1.24–1.26× | 1.61% / 2.47% |
| JSON-to-Markdown report | 50.464–50.539 µs | 37.779 µs | 1.34–1.34× | 0.15% / 6.60% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** Warm CSV/text remain about1.45–1.47×/1.86–1.90×CPython. Against
the F04 reference, primary CSV and warm text are mixed, JSON is small/mixed, and
fresh text is1.75–2.01% slower. That possible regression remains visible in the
[comparison evidence](../benchmarks/results/2026-11-06-call-frame-cpython/README.md);
there is no broad speedup claim.
Report elapsed and compilation remain variable. Cross-block CPython timings also
change, so earlier ratios alone do not establish a source regression or improvement.

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Twelve jobs completed328.12s; all workers joined and fixtures were removed.

Both engines run identical scripts/fixtures, checking complete output/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include reads/writes; staging/reset is outside elapsed clocks. Warm
includes invocation and prior-result cleanup. Compile includes construction/
disposal; compile-to-result also executes a fresh script. Transport and final
program/result/process/fixture teardown are outside clocks. CPython retains imported
modules between invocations; Lython isolates execution imports. This is an embedding
comparison, not a claim of identical implementations or import caching.

## Improvements within Lython

CPython remains the primary external baseline above. Separate source pairs show:

- One active structural cycle path: complete compound-key aggregation elapsed
  −14.8–17.1% versus Q01; CSV roughly flat and text control-limited. The managed
  nesting bound remains, with linear active-path membership. No allocation or
  broad CPU claim. [Evidence](../benchmarks/results/2026-11-06-structural-active-path/README.md).
- One contextual key-preparation walk: complete compound-key aggregation elapsed
  −15.5%/−20.5% for widths 2/8 against F05; CSV roughly flat. Original adverse
  text and the unchanged-source confirmation remain visible; no allocation or
  broad CPU claim. This later change does not update the primary table's revision.
  [Evidence](../benchmarks/results/2026-11-06-key-preparation/README.md).
- Ordinary method operands remove temporary bound-method packaging: whole method
  CSV process allocation−7.26–7.28%, warm elapsed−3.67–4.61%, fresh−1.04–1.99%.
  Controls also change; elapsed is diagnostic. [Evidence](../benchmarks/results/2026-11-06-direct-method-operands/README.md).
- Unified invocations remove a duplicate interpreter object. Initial CSV process
  allocation−1.77–1.79%; refined method-return elapsed−20.1–20.6% versus that
  prototype. Refined allocation was not remeasured. [Evidence](../benchmarks/results/2026-11-06-unified-invocations/README.md).
- Nonlocal owner-map removal: affected nested CSV elapsed−3.25–3.94%, ordinary
  CSV flat/slightly adverse. [Evidence](../benchmarks/results/2026-11-06-nonlocal-owners/README.md).
- Prepared patterns: record warm−27–29%, fresh−25–30%; compile/text inconclusive.
  [P01 evidence](../benchmarks/results/2026-11-06-prepared-patterns/README.md).
- Canonical key storage: bounded set−47%, dataclass keys−30–31%; machinery removed
  and custom hashes corrected. Warm CSV/text adverse results remain historical
  debt. [K01 evidence](../benchmarks/results/2026-11-06-canonical-key-storage/README.md).
- Native helper returns: allocation−19.92%/−19.31%; elapsed diagnostic. UTF-8
  formatting allocation−8.67%; controlled elapsed/CPU supports retention, with
  the original adverse timing unexplained and preserved.

These percentages are not additive or claims about every script. Substantial
complete-script performance work remains active.

## Historical measurements and unresolved workloads

The following were not refreshed to1134d6e8 and do not describe its performance:

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
