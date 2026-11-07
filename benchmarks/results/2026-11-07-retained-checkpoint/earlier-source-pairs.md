# Earlier source-pair account

Snapshot of the previous benchmark guide at documentation revision f22d0346,
preserved as historical evidence. Its primary table measured production8c9fd0d7.
Each source pair has its own revision and measurement limits; none is a new
measurement of the current checkout.

## Improvements within Lython

The later [native integer-width change](../2026-11-07-native-integer-width/README.md)
reduces numeric-string JSON allocation about12.5% under both APIs, with controlled
warm elapsed10.7–12.5% lower. Padded CSV is1.0–1.2% slower; that measured tradeoff
is retained rather than hidden behind another parser tier. Ordinary JSON remains
variable. These affected-input source pairs do not replace the CPython tables or
establish async elapsed, larger-input scaling, isolated CPU or embedding latency.

The later [declaration binding layout](../2026-11-07-declaration-bindings/README.md)
uses existing module slots while retaining live namespaces. A bounded source-pair
comparison observes warm search4.3–5.1%, text2.1–5.7% and ordinary JSON5.5–7.7%
lower elapsed time. Text/current-role spread3.26% limits its magnitude; allocation
is essentially flat. These later-source measurements are separate from the
external CPython tables above and do not establish isolated CPU or async gains.

The [general integer parser](../2026-11-07-integer-parsing/README.md)
improves explicitly affected whole scripts: padded-field CSV warm time falls
12.5–13.4%, and numeric-string JSON 41.4–42.0%, versus source77703a42. Initial
allocation falls13–15%/63%; ordinary controls remain variable. These inputs and
source-pair results are separate from the external CPython table above.

The [ordinary-operation transport consolidation](../2026-11-07-operand-transport/README.md)
emits fewer instructions and reduces instruction/read-span payload. Its controlled
complete-script elapsed results are mostly variable, including inconclusive JSON;
no substantial CPU gain is established. Separate compile allocation rises 0.20–0.87%.
It is included in the recorded CPython revision; the source pair does not isolate
its contribution to that combined checkpoint.

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or broad CPU claims:

| Change / affected workload | Measured result | Limits / evidence |
| --- | --- | --- |
| Shared receiver methods / regex capture summary;1,024records | Process allocation about−29.47% on both APIs; warm elapsed−8.64–10.80% versus1971487a | JSON/text/CSV controls variable; original CSV+2.05–3.41% remains possible debt. Admitted peaks identical; Full platform/Stress/package delivery green. [R41](../2026-11-07-receiver-methods/README.md) |
| Unicode lookbehind / scalar positions;8,192constructed characters | Warm elapsed −78.99–79.47% versusb0889a46, about50ms→10.4ms with the same optional0.4 backend | Growing-input diagnostic; allocation essentially flat. ASCII/forward controls variable, possible ASCII debt up to+6.31%. Full platform/Stress/package delivery green. [R40](../2026-11-07-regex-scalar-cursor/README.md) |
| Demand-bound annotation analysis / compilation | Report compile elapsed −6.51–8.02%; text compile −8.25–9.13%; config compile-to-result −0.54–2.42% versus79fddd4a | Separate compile allocation −4.38–8.94% across five scripts. Regex control +304B/+0.117%, elapsed up to+1.66%; warm text variable. Full platform/Stress/package delivery green; no warm CPU claim. [R38](../2026-11-07-annotation-demand/README.md) |
| Direct formatted instructions / dynamic-width report | Warm elapsed −10.09–13.19% versus2f1f2715; about15.35% less allocation on both APIs | Labelled complete-script variant; static warm favorable, fresh/text variable. CSV+0.13–2.36% accepted possible debt. Full platform/Stress/package delivery green; no broad CPU claim. [R35](../2026-11-07-canonical-format-instructions/README.md) |
| Completed native helper calls / text | Warm elapsed −4.25–7.61% sync / −7.63–8.64% async versus c4729a9f | Allocation/peaks flat. Report variable; CSV up to+3.04% against differing baseline processes. Coarse CPU is diagnostic. [R29](../2026-11-07-execution-transport/implementation.md) |
| Completed CSV record values | Warm elapsed −7.72–8.04% native helper / −4.13–4.78% ordinary helper versusec134e8c | Native CSV allocation −7.31/−6.55% sync/async. Text control spread2.52%; coarse CPU mixed. [R25](../2026-11-07-csv-record-values/README.md) |
| Validated regex programs / extraction | Warm elapsed −8.34–9.64%; compile-to-result −6.47–6.53% versus41c25958 | Programs remain charged until script disposal; larger compilation footprint. Text control spread2.78%, unchanged compilation also moves. [R23](../2026-11-06-regex-program-reuse/README.md) |
| Fixed builtin initialization / configuration | Warm elapsed −1.13–2.44%; fresh −1.43% to +0.13% versus4fad63c5 | Removes2,688B of reflected factory allocation per invocation. Compilation control −1.20–1.63%; text role spread4.10%. No broad CPU gain. [R22](../2026-11-06-builtin-initialization/README.md) |
| Frame resumption / text helpers | Warm elapsed −3.00–5.59%; fresh −0.79% to +0.02% versus f2ce53db | Allocation flat. Unchanged compilation control −1.47–2.23% limits precision; no general CPU or CPython-baseline claim. [R21](../2026-11-06-frame-resume-boundaries/README.md) |
| Shared record keys / JSON and report | JSON warm elapsed −10.89–12.53%; report warm −5.36–8.80%, fresh −8.53–10.52% versus b542b601 | Separate complete-job allocation −14.98–14.99% JSON, −9.43% report. Text confirmation −1.21% to +5.53%; possible regression accepted as debt. CSV coarse CPU adverse. Source pair core67f84196; later primary comparison above includes subsequent changes. [R20](../2026-11-06-record-layout/README.md) |
| Caller-owned regex captures / extraction | Warm elapsed −3.62–5.17%; fresh −4.58–6.40% versus b6650f09 | Same optional 0.4 producer; control −2.13% to +0.10%. Direct cursor allocation −20.34% is a separate measurement. Regex remains about 7× CPython. [U04](../2026-11-06-regex-capture-ownership/README.md) |
| Deferred helper contexts / CSV | Warm elapsed −5.85–6.24%; fresh −5.53–5.83% versus R10 | Warm CPU flat; warm text adverse, JSON/report mixed; exact allocation unmeasured. [R13](../2026-11-06-deferred-contexts/README.md) |
| Contextual key preparation / compound-key aggregation | Elapsed −15.5%/−20.5% versus F05 | CSV roughly flat; text uncertainty. [Q01](../2026-11-06-key-preparation/README.md) |
| Active structural path / compound-key aggregation | Elapsed −14.8–17.1% versus Q01 | CSV roughly flat; text control-limited. [R02](../2026-11-06-structural-active-path/README.md) |
| Final tuple backing / compound-key aggregation | Elapsed −6.17–12.29% versus R02 | CSV inconclusive; no process allocation percentage. [R04](../2026-11-06-executable-tuple-backing/README.md) |
| Ordinary method operands / method-heavy CSV | Allocation −7.26–7.28%; warm elapsed −3.67–4.61% | Separate clock boundaries; controls also improve. [F05](../2026-11-06-direct-method-operands/README.md) |

Native helper returns reduced process allocation19.92%/19.31%; elapsed remains
diagnostic. Direct UTF-8 formatting reduced allocation8.67%; controlled elapsed/CPU
supports retention, while the original adverse result remains unexplained.
[Subsequent canonical formatting](../2026-11-07-canonical-formatting/README.md)
fixes a stale type proof and removes duplicate execution with allocation flat;
its async report and text elapsed results are adverse/variable, with no CPU gain
claimed. It and R29 are included in the primary checkpoint above; individual source-pair limits remain.
[Full experimental evidence](../../EXPERIMENTS.md) preserves earlier
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

The [earlier construction checkpoint](../2026-11-06-construction-cpython/README.md)
retains794e552b and its original adverse conditions. The [previous call/frame checkpoint](../2026-11-06-call-frame-cpython/README.md)
retains the1134d6e8 results and qualification. The [earlier retained checkpoint](../2026-11-05-runtime-cpython-refresh/README.md)
keeps the previous four-script results, full data and closed qualification. Earlier
90785737 search remains inconclusive (fresh270%IQR/218% control drift, warm138%/77%):
[historical evidence](../../HISTORY.md). Native process-varying hashes/buckets
can plausibly influence small-role differences; causation in these runs is unproven.

[Experimental history](../../EXPERIMENTS.md) preserves rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../../README.md)
describes existing qualification procedures; [tests](../../../tests/README.md) cover
semantic equivalence, ownership, cancellation and installed consumers. No timing
claim establishes containment or matching cancellation by itself.
