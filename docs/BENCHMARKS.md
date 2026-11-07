# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.065–4.190 ms | 3.368 ms | 1.21–1.24× | Variable; 2.99% / 0.69% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.105–2.129 ms | 1.263 ms | 1.67–1.69× | Diagnostic; 1.13% / 0.58% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.305–4.329 ms | 4.186 ms | 1.03–1.03× | Diagnostic; 0.55% / 0.91% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.608–3.665 ms | 2.509 ms | 1.44–1.46× | Variable; 1.56% / 3.30% |
| Configuration read/edit/write | 16 paths / 181 B | 147.184–147.313 µs | 125.544 µs | 1.17–1.17× | Diagnostic; 0.09% / 2.76% |
| CSV with padded numeric fields | 4,096 records / 191,505 B | 4.569–4.586 ms | 4.325 ms | 1.06–1.06× | Diagnostic; 0.37% / 1.15% |
| JSON with numeric strings | 4,096 records / 278,528 B | 7.767–7.807 ms | 4.639 ms | 1.67–1.68× | Diagnostic; 0.52% / 1.62% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 307.328–312.170 µs | 210.604 µs | 1.46–1.48× | Diagnostic; 1.55% / 1.11% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 377.855–380.426 µs | 255.370 µs | 1.48–1.49× | Diagnostic; 0.68% / 2.39% |

**2026-11-07; 0.11 development source `8c9fd0d7`; CPython3.13.16; .NET11RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython elapsed; above
one means slower.** Ranges span two independent identical-build process medians,
not confidence intervals. Control is their absolute difference/candidate; IQR is
the larger Lython-role interquartile range/median. Diagnostic/Variable describe
short observations; they are not design acceptance thresholds.

The first five workloads retain the maintained ordinary inputs. The next two
explicitly exercise general integer conversion: padded CSV keeps the same source;
numeric-string JSON calls int in its helper. They do not replace the ordinary
baseline. Frozen core `d3a6c778` matches the recorded production inventory with
precommit SourceLink3480aa55. Exact production passed Windows/Ubuntu Release, actual.NET11/10, original
Stress and installed core-package consumers in CI42371564043.
[Delivery receipt](../benchmarks/results/2026-11-07-integer-parsing/delivery-ci.json).
The normal optional0.4 adapter is unchanged. No new producer certification.
[Inputs, observations and delivery status](../benchmarks/results/2026-11-07-integer-cpython/README.md).

## Warm execution, compilation and startup

Same source/input and execution/I/O/validation/cleanup boundary, reusing compiled code:

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 3.907–3.936 ms | 3.211 ms | 1.22–1.23× | Diagnostic; 0.72% / 1.29% |
| Text summary/helpers | 2.002–2.050 ms | 1.158 ms | 1.73–1.77× | Variable; 2.33% / 0.85% |
| JSON-lines/helpers | 4.191–4.214 ms | 4.075 ms | 1.03–1.03× | Diagnostic; 0.54% / 1.17% |
| JSON-to-Markdown report | 3.499–3.509 ms | 2.337 ms | 1.50–1.50× | Variable; 0.28% / 4.13% |
| Configuration read/edit/write | 75.156–77.331 µs | 59.996 µs | 1.25–1.29× | Variable; 2.89% / 1.21% |
| CSV with padded numeric fields | 4.407–4.470 ms | 4.160 ms | 1.06–1.07× | Diagnostic; 1.41% / 1.02% |
| JSON with numeric strings | 7.601–7.671 ms | 4.507 ms | 1.69–1.70× | Diagnostic; 0.92% / 1.82% |
| Protected file search | 237.296–245.317 µs | 136.559 µs | 1.74–1.80× | Variable; 3.27% / 3.69% |
| Two-file copy/checksum/manifest | 222.824–224.537 µs | 150.289 µs | 1.48–1.49× | Diagnostic; 0.77% / 1.77% |

Compilation/program disposal only; execution and I/O excluded. Do not subtract
warm and compile-to-result medians to infer compilation; these are separate jobs.

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 68.093–69.668 µs | 59.441 µs | 1.15–1.17× | Variable; 2.26% / 1.41% |
| Text summary/helpers | 49.968–50.839 µs | 41.556 µs | 1.20–1.22× | Diagnostic; 1.71% / 0.82% |
| JSON-lines/helpers | 60.878–61.180 µs | 50.915 µs | 1.20–1.20× | Variable; 0.49% / 6.51% |
| JSON-to-Markdown report | 47.135–47.427 µs | 37.822 µs | 1.25–1.25× | Diagnostic; 0.62% / 1.00% |
| Configuration read/edit/write | 42.327–42.742 µs | 34.603 µs | 1.22–1.24× | Diagnostic; 0.98% / 0.85% |
| CSV with padded numeric fields | 68.578–69.275 µs | 59.421 µs | 1.15–1.17× | Diagnostic; 1.01% / 1.81% |
| JSON with numeric strings | 61.658–61.716 µs | 52.021 µs | 1.19–1.19× | Diagnostic; 0.09% / 1.08% |
| Protected file search | 33.273–33.524 µs | 30.798 µs | 1.08–1.09× | Diagnostic; 0.75% / 0.83% |
| Two-file copy/checksum/manifest | 80.820–81.603 µs | 56.597 µs | 1.43–1.44× | Diagnostic; 0.96% / 2.97% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** Variable controls and IQRs stay visible. This combined checkpoint
does not repay historical CSV debt, explain earlier adverse controls or isolate
each change's CPU benefit. It describes the recorded source, not future HEAD.
[Previous primary comparison, sourcefba865ef](../benchmarks/results/2026-11-07-receiver-cpython/README.md).

The two file workflows were collected in a separate six-job block on the same
qualified source and machine. Search exits after1,025 of2,049 lines; the manifest
copies only two files/4,107B. These do not measure large-file throughput.
[File-workflow inputs and observations](../benchmarks/results/2026-11-07-file-workflows/README.md).

## Machine, included work and fairness

Dedicated AzureStandard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, -I -S -X utf8. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal GC/tiering,
quiet endpoints;5s warmup per role/job, seven rotated150ms rounds.21jobs completed
434.37s; all workers joined and fixtures removed. Source/images/conditions are
preserved with the raw observations.

Both engines run identical scripts/fixtures and check complete output/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include reads/writes; staging/reset are outside elapsed clocks. Warm
includes invocation and prior-result cleanup; compile includes construction/disposal.
Protocol transport and final cached teardown are excluded. CPython retains imported
modules; Lython isolates execution imports. This is an embedding comparison, not
a claim of identical implementations. These tables use synchronous Run; historical
async comparisons use completed mediated I/O, not pending-I/O latency.
[Historical async evidence](../benchmarks/results/2026-11-06-native-read-cpython/README.md)
preserves its distinct sources and adverse controls.

## Improvements within Lython

The later [native integer-width change](../benchmarks/results/2026-11-07-native-integer-width/README.md)
reduces numeric-string JSON allocation about12.5% under both APIs, with controlled
warm elapsed10.7–12.5% lower. Padded CSV is1.0–1.2% slower; that measured tradeoff
is retained rather than hidden behind another parser tier. Ordinary JSON remains
variable. These affected-input source pairs do not replace the CPython tables or
establish async elapsed, larger-input scaling, isolated CPU or embedding latency.

The later [declaration binding layout](../benchmarks/results/2026-11-07-declaration-bindings/README.md)
uses existing module slots while retaining live namespaces. A bounded source-pair
comparison observes warm search4.3–5.1%, text2.1–5.7% and ordinary JSON5.5–7.7%
lower elapsed time. Text/current-role spread3.26% limits its magnitude; allocation
is essentially flat. These later-source measurements are separate from the
external CPython tables above and do not establish isolated CPU or async gains.

The [general integer parser](../benchmarks/results/2026-11-07-integer-parsing/README.md)
improves explicitly affected whole scripts: padded-field CSV warm time falls
12.5–13.4%, and numeric-string JSON 41.4–42.0%, versus source77703a42. Initial
allocation falls13–15%/63%; ordinary controls remain variable. These inputs and
source-pair results are separate from the external CPython table above.

The [ordinary-operation transport consolidation](../benchmarks/results/2026-11-07-operand-transport/README.md)
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
