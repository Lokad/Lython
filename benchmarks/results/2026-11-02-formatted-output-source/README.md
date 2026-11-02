# Formatted-output source gate — B119

2026-11-02, fully qualified runtime ffa3f6f8; read-only assessment. One complete
JSON-to-Markdown report capture uses the existing B29/B107 checked worker and
external perf driver on the quiet F4as_v7 Ubuntu/.NET11RC3 VM. No new benchmark
framework, prototype, allocation result, elapsed comparison or CPython multiplier.

1,024 warm complete scripts:11.146s capture wall,8.70s actual named guest-thread
CPU,1,728 samples,380 unknown. Compilation/startup and existing warmup/two-second
settling are outside capture. Normal tiering/GC; only profiling perf-map override.
Native profiler remains outside production; Lython stays managed and memory-safe.
The boundary includes actual mediated JSON input/report output and full goldens.
Other thread CPU is retained separately; wall minus guest CPU is not an I/O phase.

| Recorded work | Samples | Interpretation |
| --- | ---: | --- |
| Formatted-field conversion |405 inclusive (~23%)|Contains parsing, native string/numeric formatting and admission; not all removable |
| JSON value parser |571 inclusive (~33%)|Another substantial complete-script cost; overlaps call/executable chains |
| Native string padding |119 inclusive|B94 already removed UTF-16 string-padding transport |
| Format-spec parser |27 self|Repeated spec parsing exists; a cache is not automatically justified |
| Interpreter ExecuteUntilCall |108 self|Useful dispatch plus remaining operand/call work |
| Monitor enter/exit |279 self|Across allocation/ownership paths; not a removable memory-governor fraction |
| Unknown leaves |380|Kept explicit, alongside one partial perf-map line and native/inlining gaps |

Source confirms multipart f-strings materialize each field and then copy fields
into a final builder. Numeric padding still creates CLR repeat/concatenated text
before UTF-8 encoding. These facts plus the capture support a shared formatted-
output milestone, without attributing the full Python gap to formatting or
predicting a speedup. Preserve the already-native scalar-aware string padding.
JSON/call/operand work remains competing evidence for later coherent changes.

Selected direction: use existing operand-stack temporary cleanup and governed
builders for one final output lifetime; append native fields directly, retain
actual user-hook outputs and specialized numeric/date digit renderers. Remove
displaced field/join transport rather than accumulate a dormant second backend.
Preserve single-field identity, conversion/dynamic-spec ordering, nested fields,
Unicode scalar width, hooks and genuine async suspension. Existing lowered
formatting callers remain real. Keep admission/overlap/retained aliases and
pending users funded, cancellation active, first refusal terminal and unwind
deterministic. Concrete implementation/acceptance/debt decisions live in PLAN.md.

Both quiet gates, exact loaded core/host identities, stdout/file goldens and
joined profiler/worker cleanup pass. Service18.343s, exit0, inactive, lease free.
An initial shell CRLF launcher failed before capture in2ms; its original journal
and terminal result remain. The corrected launcher ran the same source/question;
no adverse runtime observations were discarded.

[Summary](summary.json), [prospective boundary](pre-collection-plan.md),
[identities](identity.json), [all raw perf/maps/records](evidence.tar.gz), compressed
catalog and driver/analyzers preserve exact conditions. Reuse saved captures for
offline grouping; inclusive stacks overlap and must not be summed as saved CPU.
This assessment does not change the named historical CPython comparisons or
constitute qualification of a future implementation. Publication stays postponed.
