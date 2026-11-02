# Current whole-script CPU source gate

2026-11-02, fully qualified runtime8f07764a. Existing B29 checked worker/perf driver, two current synchronous warm captures on the quiet F4as_v7 Ubuntu/.NET11RC3 VM. No prototype, new benchmark framework, elapsed speedup or CPython multiplier. Native profiling/perf-map override stays outside production; core remains managed and memory-safe.

| Complete job | Jobs | Capture wall | Actual guest CPU | Guest samples | Unknown leaves |
| --- | ---: | ---: | ---: | ---: | ---: |
| File search/early exit |4,096|8.651s|2.00s|393|31|
| Text summary/helpers |1,024|6.384s|4.59s|913|68|

Both quiet gates/full stdout+file goldens pass; exact loaded core/host identities verified. Existing driver warmup then2s settle, cpu-clock199Hz/frame-pointer chains/CLR maps. Actual named guest thread is recorded, never selected by max-CPU heuristics. Every worker/profiler closes and joins, fixtures removed, service28.733s/15.910CPU seconds, exit0/inactive/MainPID0/empty group/lease free. Exact records/unknown/malformed-map/native/inlining caveats preserved.

Search has82 self samples in ExecuteUntilCall,15 BeginPendingCall,12 CreateExecutableInterpreter,8 positional binding and7 AdvanceCallFrames. These disjoint self buckets contain required work, not a removable fraction. Its393 samples are modest. Tiered-JIT CPU0.43s and thread-pool work0.70s remain separate from the actual2.00s guest CPU. Wall-minus-guest CPU is NOT a measured I/O or handoff phase. The comparison host's byte file already uses synchronous reads/writes and advertises synchronous completion; no asynchronous-I/O-default explanation is supported.

Text has158 ExecuteUntilCall self samples,46 SetSubscriptValue,26 SplitWhitespace,23 InvokeDictionaryGet and10 BeginPendingCall. Monitor leaves total68 and unknown68; native origins and complete caller chains remain linked. Inclusive splitting/call chains overlap useful work and must not be summed into a frame-overhead percentage. Eager sigma-property work has been removed, but this capture is not a controlled before/after proof of its magnitude.

Source audit confirms ordinary calls ALREADY use explicit executable frames, parameters bind to final locals, ordinary presence is an inline ulong, namespaces are lazy, string hashing is UTF-8, and splitting already produces PyList directly. Do not propose removing an imagined bool array, second locals copy, decoded hash or intermediate list. The remaining call path still creates a complete mutable interpreter per nonsimple helper and preserves it for its caller chain. Stable lexical frame lookup/closures/generators explain why blind pooling or a shared-offset arena is unsafe or slow.

Outcome: investigate one coherent reusable-dispatch/value-continuation design, preserving direct existing local/operand arrays and stable lexical frame views. Estimate/prove actual construction removed before promoting it; prior arena/control-record regressions remain lessons. No universal tagged value or general frontend rewrite follows from these samples. Text dictionary/iteration/splitting remain competing hypotheses. Current facts and prototype acceptance belong in PLAN/private design; no arbitrary permanent closure after uncertainty.

[Summary/all thread deltas](summary.json), [prospective boundary](pre-collection-plan.md), [exact identities](identity.json), [all raw perf/maps/driver/analyzers](evidence.tar.gz). First artifact export needed file ownership/FIFO correction; raw captures were unchanged and not repeated.
