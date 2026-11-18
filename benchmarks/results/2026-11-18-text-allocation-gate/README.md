# Current text materialization allocation gate

R512,2026-11-18UTC. Committed2e585229/current frozen1bce52ad, unmodified32-file
image reverified. One local warmed Windows allocation diagnostic of existing
text-normalization.large, using installed dotnet-trace10/gc-verbose,32MiB trace
buffer/six-second collection. One256-invocation warmup batch,512subsequent verified
invocations spanning trace startup/collection/rundown. All original output/file
goldens/Closed and owned fixture cleanup pass. No quiet-VM elapsed comparison,
new production instrumentation, forced GC or benchmark framework.

| Sampled type | Allocation ticks | Approximate tick weight |
| --- | ---: | ---: |
| PyString |2296|35.18%|
| Byte arrays |1828|31.16%|
| Triple-int range arrays |909|17.03%|
| Object arrays |873|16.50%|

Tick weight attributes an allocation interval to its sampled type: these are
**not exact bytes, object counts, removable CPU or predicted savings**. Wider
worker reset/I/O/protocol/validation is included. The capture supports a coherent
materialization gate, not an assumption that argument/frame rebuilding dominates.

Source confirms4096physical lines/52224B; normalized split4097parts with lengths
0,3,6,19,28bytes and3072changed rstrip results. PyString already shares large UTF8
backing and inlines short bytes; native slots already hold numbers. Generic join
must drain before validation. A universal buffer/tagged-value change is not chosen.

Select a bounded ordinary-split construction redesign: reuse deterministic managed
scanners to count/admit final capacity, then write final arrays/strings directly.
Remove eager split/splitlines range inventories, growth/copies and temporary-loan
lifecycle. Retain range storage genuinely needed by private cased iteration. Preserve
Unicode/default/custom separators, maxsplit/reverse/line boundaries, same-value
identity, capacity overlap, terminal refusal, cancellation and source funding.
Trade a second bounded scan for removed storage; no CPU prediction. Whole-text
warm/fresh and a floating/call control pair decide measured value, not a fixed cutoff.
Any remaining regression debt needs an explicit decision. No prototype yet here.

Original local harness used an unsupported fixture prefix; the worker correctly
rejected it before warmup/trace. Preserve that source/stderr/condition; use the
existing supervisor prefix thereafter. All users were joined, the first empty
owned fixture and final fixture removed. This was no runtime or design failure.

[Capture identity/responses](capture.json),[approximate types](allocation-types.json),
[raw EventPipe trace](text.nettrace.gz),[procedures](procedures.tar.xz),
[original harness condition](initial-condition.json).
Full44225923745qualification remains independent; publication postponed.
