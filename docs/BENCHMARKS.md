# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records /57,361 B | 4.109–4.110 ms | 3.400 ms | 1.21–1.21× | Diagnostic; 0.04% / 1.06% |
| Text summary/helpers | 3,072 lines /35,840 B | 2.153–2.196 ms | 1.263 ms | 1.71–1.74× | Diagnostic; 1.99% / 1.29% |
| JSON-lines/helpers | 4,096 records /110,592 B | 4.365–4.630 ms | 4.242 ms | 1.03–1.09× | Variable; 6.07% / 0.79% |
| JSON-to-Markdown report | 3,072 records /189,441 B | 3.593–3.762 ms | 2.789 ms | 1.29–1.35× | Variable; 4.48% / 5.30% |
| Configuration read/edit/write | 16 paths /181 B | 144.745–145.047 µs | 124.272 µs | 1.16–1.17× | Variable; 0.21% / 3.67% |

**2026-11-07; 0.11 development source `fba865ef`; CPython3.13.16; .NET11RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython elapsed; above
one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is their absolute difference relative to candidate; IQR is the
larger Lython-role interquartile range/median. Diagnostic/Variable labels describe
short observations; they are not design acceptance thresholds. Variable cases stay
visible. These timings describe the recorded source, not future HEAD. Frozen core
`5d2ca8e1` matches that production source, including R36/R38/R40/R41, with precommit
SourceLink1971487a. Exact sourcefba865ef passed Windows/Ubuntu, actual.NET11/10,
original Stress and installed-package consumers. The optional normal0.4 adapter
is identical across Lython roles; private development producers are outside this
comparison. [Inputs, observations and qualification](../benchmarks/results/2026-11-07-receiver-cpython/README.md).

## Warm execution, compilation and startup

Same source/input and execution/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 3.868–3.997 ms | 3.238 ms | 1.19–1.23× | Variable; 3.22% / 0.47% |
| Text summary/helpers | 2.040–2.086 ms | 1.163 ms | 1.75–1.79× | Variable; 2.22% / 0.61% |
| JSON-lines/helpers | 4.221–4.227 ms | 4.072 ms | 1.04–1.04× | Diagnostic; 0.14% / 0.75% |
| JSON-to-Markdown report | 3.549–3.594 ms | 2.603 ms | 1.36–1.38× | Variable; 1.24% / 8.69% |
| Configuration read/edit/write | 75.062–75.250 µs | 60.212 µs | 1.25–1.25× | Diagnostic; 0.25% / 0.93% |

Compilation/program disposal only; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation: these are independent job blocks.

| Compile-only script | Lython | CPython | Ratio | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 67.955–68.465 µs | 59.471 µs | 1.14–1.15× | Diagnostic; 0.75% / 2.73% |
| Text summary/helpers | 49.876–50.284 µs | 41.640 µs | 1.20–1.21× | Diagnostic; 0.82% / 2.09% |
| JSON-lines/helpers | 58.505–58.685 µs | 50.710 µs | 1.15–1.16× | Diagnostic; 0.31% / 2.30% |
| JSON-to-Markdown report | 46.770–46.959 µs | 38.039 µs | 1.23–1.23× | Variable; 0.40% / 5.82% |
| Configuration read/edit/write | 41.959–42.187 µs | 34.605 µs | 1.21–1.22× | Diagnostic; 0.54% / 1.65% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** Text remains the largest maintained warm gap,1.75–1.79×. Report warm
IQR8.69%, text warm control2.22%, CSV warm3.22% and fresh JSON control6.07% limit
interpretation. This combined checkpoint does not isolate each change's CPU benefit,
repay earlier CSV debt or explain historical adverse/unstable source-pair results.
[Previous primary comparison, source42101cd5](../benchmarks/results/2026-11-07-formatted-cpython/README.md).

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Fifteen jobs completed311.66s; all workers joined and fixtures were removed.

Both engines run identical scripts/fixtures, checking complete output/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include reads/writes; staging/reset is outside elapsed clocks. Warm
includes invocation and prior-result cleanup. Compile includes construction/
disposal; compile-to-result also executes a fresh script. Transport and final
program/result/process/fixture teardown are outside clocks. CPython retains imported
modules between invocations; Lython isolates execution imports. This is an embedding
comparison, not a claim of identical implementations or import caching.

These primary tables use `Run`. Historical `4ace68ff` `RunAsync` jobs use completed
mediated I/O: text2.630–2.636ms versus CPython1.203ms (2.19×), CSV5.586–5.670ms
versus3.220ms (1.74–1.76×). Async text elapsed is6.91–7.14% below Q02;
CSV is inconclusive. These do not measure genuinely pending-I/O latency.
[Historical async evidence](../benchmarks/results/2026-11-06-native-read-cpython/README.md),
[earlier async hash/read debt](../benchmarks/results/2026-11-06-async-key-reads/README.md)
and [original native-read comparison](../benchmarks/results/2026-11-06-native-read-prefix/README.md)
retain their distinct source pairs and adverse results.

## Improvements within Lython

The later [general integer parser](../benchmarks/results/2026-11-07-integer-parsing/README.md)
improves explicitly affected whole scripts: padded-field CSV warm time falls
12.5–13.4%, and numeric-string JSON 41.4–42.0%, versus source77703a42. Initial
allocation falls13–15%/63%; ordinary controls remain variable. These inputs and
later source are separate from the primary CPython table above.

The later [ordinary-operation transport consolidation](../benchmarks/results/2026-11-07-operand-transport/README.md)
emits fewer instructions and reduces instruction/read-span payload. Its controlled
complete-script elapsed results are mostly variable, including inconclusive JSON;
no substantial CPU gain is established. Separate compile allocation rises 0.20–0.87%.
It is not included in the CPython revision measured above.

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or broad CPU claims:

| Change / affected workload | Measured result | Limits / evidence |
| --- | --- | --- |
| Shared receiver methods / regex capture summary;1,024records | Process allocation about−29.47% on both APIs; warm elapsed−8.64–10.80% versus1971487a | JSON/text/CSV controls variable; original CSV+2.05–3.41% remains possible debt. Admitted peaks identical; Full platform/Stress/package delivery green. [R41](../benchmarks/results/2026-11-07-receiver-methods/README.md) |
| Unicode lookbehind / scalar positions;8,192constructed characters | Warm elapsed −78.99–79.47% versusb0889a46, about50ms→10.4ms with the same optional0.4 backend | Growing-input diagnostic; allocation essentially flat. ASCII/forward controls variable, possible ASCII debt up to+6.31%. Full platform/Stress/package delivery green. [R40](../benchmarks/results/2026-11-07-regex-scalar-cursor/README.md) |
| Demand-bound annotation analysis / compilation | Report compile elapsed −6.51–8.02%; text compile −8.25–9.13%; config compile-to-result −0.54–2.42% versus79fddd4a | Separate compile allocation −4.38–8.94% across five scripts. Regex control +304B/+0.117%, elapsed up to+1.66%; warm text variable. Full platform/Stress/package delivery green; no warm CPU claim. [R38](../benchmarks/results/2026-11-07-annotation-demand/README.md) |
| Direct formatted instructions / dynamic-width report | Warm elapsed −10.09–13.19% versus2f1f2715; about15.35% less allocation on both APIs | Labelled complete-script variant; static warm favorable, fresh/text variable. CSV+0.13–2.36% accepted possible debt. Full platform/Stress/package delivery green; no broad CPU claim. [R35](../benchmarks/results/2026-11-07-canonical-format-instructions/README.md) |
| Completed native helper calls / text | Warm elapsed −4.25–7.61% sync / −7.63–8.64% async versus c4729a9f | Allocation/peaks flat. Report variable; CSV up to+3.04% against differing baseline processes. Coarse CPU is diagnostic. [R29](../benchmarks/results/2026-11-07-execution-transport/implementation.md) |
| Completed CSV record values | Warm elapsed −7.72–8.04% native helper / −4.13–4.78% ordinary helper versusec134e8c | Native CSV allocation −7.31/−6.55% sync/async. Text control spread2.52%; coarse CPU mixed. [R25](../benchmarks/results/2026-11-07-csv-record-values/README.md) |
| Validated regex programs / extraction | Warm elapsed −8.34–9.64%; compile-to-result −6.47–6.53% versus41c25958 | Programs remain charged until script disposal; larger compilation footprint. Text control spread2.78%, unchanged compilation also moves. [R23](../benchmarks/results/2026-11-06-regex-program-reuse/README.md) |
| Fixed builtin initialization / configuration | Warm elapsed −1.13–2.44%; fresh −1.43% to +0.13% versus4fad63c5 | Removes2,688B of reflected factory allocation per invocation. Compilation control −1.20–1.63%; text role spread4.10%. No broad CPU gain. [R22](../benchmarks/results/2026-11-06-builtin-initialization/README.md) |
| Frame resumption / text helpers | Warm elapsed −3.00–5.59%; fresh −0.79% to +0.02% versus f2ce53db | Allocation flat. Unchanged compilation control −1.47–2.23% limits precision; no general CPU or CPython-baseline claim. [R21](../benchmarks/results/2026-11-06-frame-resume-boundaries/README.md) |
| Shared record keys / JSON and report | JSON warm elapsed −10.89–12.53%; report warm −5.36–8.80%, fresh −8.53–10.52% versus b542b601 | Separate complete-job allocation −14.98–14.99% JSON, −9.43% report. Text confirmation −1.21% to +5.53%; possible regression accepted as debt. CSV coarse CPU adverse. Source pair core67f84196; later primary comparison above includes subsequent changes. [R20](../benchmarks/results/2026-11-06-record-layout/README.md) |
| Caller-owned regex captures / extraction | Warm elapsed −3.62–5.17%; fresh −4.58–6.40% versus b6650f09 | Same optional 0.4 producer; control −2.13% to +0.10%. Direct cursor allocation −20.34% is a separate measurement. Regex remains about 7× CPython. [U04](../benchmarks/results/2026-11-06-regex-capture-ownership/README.md) |
| Deferred helper contexts / CSV | Warm elapsed −5.85–6.24%; fresh −5.53–5.83% versus R10 | Warm CPU flat; warm text adverse, JSON/report mixed; exact allocation unmeasured. [R13](../benchmarks/results/2026-11-06-deferred-contexts/README.md) |
| Contextual key preparation / compound-key aggregation | Elapsed −15.5%/−20.5% versus F05 | CSV roughly flat; text uncertainty. [Q01](../benchmarks/results/2026-11-06-key-preparation/README.md) |
| Active structural path / compound-key aggregation | Elapsed −14.8–17.1% versus Q01 | CSV roughly flat; text control-limited. [R02](../benchmarks/results/2026-11-06-structural-active-path/README.md) |
| Final tuple backing / compound-key aggregation | Elapsed −6.17–12.29% versus R02 | CSV inconclusive; no process allocation percentage. [R04](../benchmarks/results/2026-11-06-executable-tuple-backing/README.md) |
| Ordinary method operands / method-heavy CSV | Allocation −7.26–7.28%; warm elapsed −3.67–4.61% | Separate clock boundaries; controls also improve. [F05](../benchmarks/results/2026-11-06-direct-method-operands/README.md) |

Native helper returns reduced process allocation19.92%/19.31%; elapsed remains
diagnostic. Direct UTF-8 formatting reduced allocation8.67%; controlled elapsed/CPU
supports retention, while the original adverse result remains unexplained.
[Subsequent canonical formatting](../benchmarks/results/2026-11-07-canonical-formatting/README.md)
fixes a stale type proof and removes duplicate execution with allocation flat;
its async report and text elapsed results are adverse/variable, with no CPU gain
claimed. It and R29 are included in the primary checkpoint above; individual source-pair limits remain.
[Full experimental evidence](../benchmarks/EXPERIMENTS.md) preserves earlier
source pairs, rejected designs and uncertainty. Substantial whole-script gaps
remain; possible earlier warm-text regression remains explicit debt.

## Historical measurements and unresolved workloads

The following were not refreshed to `fba865ef` and do not describe its performance:

| Workload | Earlier revision/boundary | Lython / CPython | Status |
| --- | --- | ---: | --- |
| CSV/native helpers |13d69be6; RAM; compile-to-result|1.27–1.29×|Diagnostic|
| Config read/edit/write;1,024 paths |13d69be6; RAM; compile-to-result|1.03–1.04×|Variable|
| CSV long fields;16records/524,717B |13d69be6; RAM; compile-to-result|0.14×|Variable; does not describe ordinary CSV|
| Search with early exit;42,010B |bd3f9dee; ordinary filesystem|2.09×|Diagnostic|
| Unicode/ASCII filename filter;768names |bd3f9dee; ordinary filesystem|2.14×/2.70×|Variable/diagnostic; filters supplied names, not768files|
| Copy/checksum/manifest;4,107B |bd3f9dee; ordinary filesystem|1.69×|Diagnostic|

The [earlier construction checkpoint](../benchmarks/results/2026-11-06-construction-cpython/README.md)
retains794e552b and its original adverse conditions. The [previous call/frame checkpoint](../benchmarks/results/2026-11-06-call-frame-cpython/README.md)
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
