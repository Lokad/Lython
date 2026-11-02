# B119 formatted-output source gate

Question: does the complete JSON-to-Markdown report justify replacing field
strings plus final joining with a shared final UTF-8 output path? Source confirms
CompileFormattedParts pushes each formatted field; JoinFormattedParts copies
them into a governed builder. String fields already use B94 native scalar-aware
padding. Numeric fields still render and pad CLR strings before encoding. These
facts do not establish dominance or a predicted speedup.

Use the existing B29/B107 checked worker/perf driver, exactly one warm report
capture, 1,024 complete invocations, fully qualified ffa3f6f8 baseline. Reuse the
existing catalog and all independent stdout/file goldens. Existing warmup and
two-second settling outside capture. Compilation/startup excluded. Actual named
guest thread, all thread CPU/unknown/native/inlining caveats retained. No elapsed
comparison, new framework or primitive-only justification. External profiler only;
production remains fully managed and memory-safe.

Shared VM lease, before/after quiet gates, owned 90-second service. Join profiler
and worker, remove fixtures, save all raw data. Offline grouping from that same
capture only. Stop after a source-backed selection or explicit inconclusive
decision; no repeated profiling to obtain a desired attribution.

If selected, the completion milestone is a single prepared formatted-output
lifetime on the existing operand stack (which already disposes temporary values),
with a shared append-to-output formatter. Remove displaced join/field transport
for that route rather than retain an optimization backend. Preserve single-field
identity, conversion-before-dynamic-spec evaluation, nested formatting, Unicode
scalar width/precision, numeric sign/grouping, errors, user hooks and actual async
suspension. Existing lowered evaluator still needs its real formatting callers.
Use current scopes/builders; no owner framework, cache or policy. Keep capacity
and replacement overlap admitted and every pending callback/user funded. Refusal
is terminal, cleanup deterministic, and cancellation effective inside long output.

Before implementation, specify actual source deletion and one bounded complete
report/text/CSV control comparison, separating allocation from CPU and elapsed.
Do not promote an intricate builder-stack design solely because allocation falls.
