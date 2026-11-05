# Current compiler attribution and next design

2026-11-05, source ab487dfa, exact frozen core04f3e00d. Quiet AzureF4as_v7,
Ubuntu24.04.4/.NET11RC3. Reuse the existing checked public worker and perf protocol;
no production instrumentation or new benchmark API. This is attribution, not a
before/after comparison or normal-settings elapsed result.

Compile complete text-summary and JSON-to-Markdown scripts separately:6s warmup,
2s settle,65,536 compilations each,199Hz cpu-clock/frame-pointer collection. Timed
captures3.893s/3.658s; actual compiler-thread CPU3.90s/3.66s. Compilation happens on
the active thread-pool worker; the dedicated guest execution worker is idle. Each
capture is attributed to the actual highest-CPU compiler thread. Both pre/post
full stdout/file goldens, exact-core/ordinary-runtime checks, quiet gates and
owned-process/fixture cleanup pass; archive checks the lease is free. Compilation
excludes guest IO/execution; verification executes separately. No public budget-
balance, startup, RSS/heap or embedding-latency claim follows from this worker.

| Declaring-owner stage, inclusive and overlapping | Text /774 samples | Report /728 samples |
| --- | ---: | ---: |
| Parser |217 (28.0%)|245 (33.7%)|
| Static binding/abstract analysis |196 (25.3%)|144 (19.8%)|
| Executable construction |124 (16.0%)|135 (18.5%)|
| Lexical preparation |69 (8.9%)|75 (10.3%)|
| Syntax validity |59 (7.6%)|52 (7.1%)|
| Annotation text/diagnostics |55 (7.1%)|39 (5.4%)|
| Scope facts |18 (2.3%)|38 (5.2%)|
| Tokenization |23 (3.0%)|21 (2.9%)|
| LoweredScript |20 (2.6%)|22 (3.0%)|

These include useful and overlapping work, not removable fractions. Expression
child enumeration has52/42 leaf samples; repeated walks come from independent
validity, class-cell, lexical and binding consumers. LoweredScript's measured
share does not justify a general AST/backend rewrite. Unknown leaves123/102 and
one incomplete perf-map line per capture remain in the raw evidence. Frame-pointer
chains can be incomplete; generated owner names and inlining limit attribution.

Select a bounded reassessment of the complete arithmetic precedence consolidation.
Current parser still descends six fixed grammar layers for every ordinary primary;
ParseLeftAssociativeExpression/ParseNextLayer have54/47 combined leaf samples.
The [earlier B46 design](../2026-11-01-precedence-parser-rejected/README.md) removed
five forwarding methods and30 net lines, with6–8% compilation CPU improvements on
CSV/text. It was rejected for missing a rigid10% threshold; its19.05% fresh-config
control drift remains inconclusive. The superseding evidence-led policy and this
current attribution justify one confirmation, without automatic promotion or
discarding old adverse results. Preserve boolean/comparison/unary/power paths,
AST/spans/diagnostics, per-layer bounds, admission and cancellation. Whole-script
performance remains open even if compilation improves.

[Stage analysis and traversal callers](stage-attribution.json),
[offline classifier](analyze-stages.py), [identity](identity.json),
[premeasurement contract](plan.md), [raw observations](observations.tgz).
SHA256 `a5df4104bc200ed24d19a16dbe28c6ffeee2fd82881d695772c4939fc8eb6858`.
The [diagnostic corrections](diagnostic-corrections.md) preserve an inherited
execution-only disassembly setting, archive permissions and a corrected classifier
that had matched parameter types. No capture was repeated or sample removed.
