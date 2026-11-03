# B164 complete call-transport source gate

2026-11-03, exact runtime sourceb087c7b8/corea3aa2347. One bounded current CPU
diagnostic using existing checked workers/perf protocol, not a runtime prototype,
source-pair speedup or CPython ratio. Four complete text/JSON-lines jobs: synchronous
warm and warm-async, same synchronous-completion RAM file host, native helper
returns retained. Six-second warmup and1024 full invocations per capture; normal
GC/tiering plus diagnostic DOTNET_PerfMapEnabled=1. Four actual guest captures
3.72/4.01/5.65/6.46CPU seconds (~20s total), owned service60.933s, exit0.

| Job | Guest samples | Shared prefix leaf | Sync call /async value-op leaf | Call-frame selector leaf | Binding/rent/return leaf |
| --- | ---: | ---: | ---: | ---: | ---: |
|json-async|1285|182|0 /26|20|12|
|json-warm|1126|169|27 /0|25|10|
|text-async|793|162|0 /14|15|7|
|text-warm|739|132|38 /0|13|11|

Prefix is14–20% of named guest samples; it includes inlined work. Ordinary-call
selection/dispatch appears across the prefix and its helpers. Narrow binding names
again cover only about1%; no binding-only storage prototype is justified here.
Async value-operation names alone cover about2%; moving only that wrapper does
not establish a substantial improvement. Unknown leaves remain46–58 per job;
managed/native inlining and incomplete frame-pointer chains prevent treating these
counts as causal CPU costs or upper bounds. Broader source categories overlap.
Do not add inclusive and leaf counts. Capture elapsed is instrumented, not a benchmark.

Source assessment finds a coherent duplication: ordinary Call first reads/classifies
its evaluated target for linear-frame selection, then non-frame calls read it again.
The sync ExecuteExecutableCall and async value-operation helper duplicate native
receiver/unary/function/bound-method/general-target selection and completed result
transport. Existing positional locals already bind directly; function namespaces
are lazy. No context/frame merger or new value representation is selected.

**Next implementation milestone B165:** resolve the already evaluated target once,
share native/general call selection and completed stack-value transfer, retain the
existing linear frame chain/general binder and genuine async invocations. Delete
displaced duplicated selectors and async call-wrapper body; do not create an
operation catalogue, strategy framework, arena, new owner or compatibility mode.
This removes real duplicated work/source. Its whole-script CPU magnitude is an
unproven hypothesis, not a predicted gain. Compare complete CSV/text/JSON/report
under BOTH APIs with controls/allocation/peaks and explicitly decide B162's roughly
2%synchronous debt. If the full route remains adverse, refine/withdraw rather than
claim completion from a small call microcase. Preserve argument order/defaults/
keywords/receiver identity/closures, guest effects and dynamic rebinding, actual
suspension, cancellation, first refusal, retained aliases and awaited cleanup.

All workers verify complete stdout/files before/after, close and exit0; fixtures
removed, quiet gates pass and lease independently free. Explicitly named active
guest worker TIDs are recorded; no max-CPU heuristic. All-thread/process overhead
and embedding latency are not quantified. Original perf.data stays on the VM;
exported raw leaves/maps/records retain all unknown frames. Runtime-internal native
signatures in profiler maps do not introduce unsafe Lython code.

[Assessment/all leaf counts](assessment.json), [raw mapped evidence](observations.tgz),
[exact image identity](identity.json). No new runtime code or qualification repeated.
