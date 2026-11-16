# Current complete-report call and frame attribution

R420 / B815, captured2026-11-15UTC; recorded2026-11-16UTC.
Committedc023042d / frozen0393331a, subsequently fully qualified by
[43774159426](../2026-11-15-runtime-format-operation/delivery/README.md).
Existing1,024record/189,441B JSON-to-Markdown method-format report, unchanged
stdout/file goldens. Quiet F4as_v7 Ubuntu24.04.4, four EPYC9V45vCPUs,
16GiB/no swap,.NET11RC3 normal tiering/natural workstation GC.

Eight guest seconds warmup,5.342guest seconds/1,280invocations sampled at199Hz
with perf CPU-clock/frame-pointer and diagnostic DOTNET_PerfMapEnabled=3.
Whole worker includes mediated RAM I/O, reset/protocol/goldens and prior-result
cleanup; wider than execution clocks. No comparison or speedup is measured.

**1,016active-thread samples /146unknown leaves**, all unknowns and other-thread
samples/CPU retained. Inclusive overlapping groups:568call/packet,251method
operation,170JSON input,157byte storage,140native formatting,97entry/frame
retirement,86admission/checks. These include useful/inlined work and are not
removable overhead shares. Nearest frames include95instruction prefix,76Reserve,
53string append,51general target invoke,41template executor,40JSON construction,
39method operation and29StartInline. Active thread5.11CPU seconds, GC.22/JIT.05;
no conclusion that either accounting or all call activity can be eliminated.

Compiler facts confirm native-eligible/unmirrored render helpers, capacity6for
f-strings/9for method formatting; ordinary format still promotes at LoadCallMember.
Its frequency and exclusive binding/storage cost are not instrumented. Imports
already execute code objects; protected try/except native helpers, lazy namespaces,
borrowed callable entry, small JSON scalar parsing and final UTF8 strings are
already delivered. They cannot be counted as new removal opportunities.

**Decision:** close this bounded capture without selecting an ABI/frame rewrite.
Broad call counts do not isolate a dominant removable packet or activation cost.
R403controller and R411tail-borrow regressions remain relevant; do not reintroduce
them under another view/arena. A complete storage/transport replacement remains
eligible when source evidence identifies its lifetime invariant and displaced work.
Move to a different representative workload/source gate rather than another format
micro-optimization or unchanged capture. The performance objective remains open.

Every image/golden/quiet/Closed/exit/join/archive gate passes; persistent trace
hash-verified under lease before RAM unmount, all users joined. Only the obsolete
8afb core's exact committed archived replicas were retired after fresh ownership,
path/hash/all-alias proof; current039and qualified9d images/toolchains retained.
254/252MiB floors preserved; no users or RAM fixture remain. Terminal cached.

[All samples](attribution.json),[compiler facts](compiler-facts.json),
[complete image](identity.json),[raw capture](observations.tgz),
[procedures and custody](procedures.tar.xz).
No runtime edit, prototype, allocation improvement or causal timing claim.
