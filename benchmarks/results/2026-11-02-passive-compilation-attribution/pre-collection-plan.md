# B86 — one passive native compilation attribution, before collection

Original B80 allocation trace has sampler events but1,683/1,818 actual compiler
stacks end in PollGC. It cannot establish ordinary CPU dominance. This distinct
question requires passive CPU samples, not another allocation/latency capture.
No candidate and no comparison/gain claim.

Reuse existing owned B79 perf/protocol driver and B80 borrowed-budget wrapper;
unchanged complete scripts.text-summary-helpers.large, lane compile, current
managed core and ordinary0.4 adapter. Verify current source/binary identities,
CPython3.13.16/full Lython output+file goldens before/after, no guest output during
compile batches, actual worker/collector joins, final budget0/normal fixtures and
both quiet gates. Root source remains4cae38f4, later commits test/evidence only.

One15s systemd owned service, target2s passive cpu-clock199Hz/fp window, hard3s
capture guard including final32-job batch;128 compilation warmups,2s settle.
Normal GC/tiering, only existing DOTNET_PerfMapEnabled=1 diagnostic metadata.
No EventPipe allocation sampling. Record ALL thread CPU, preserve jitdump/map and
raw samples. Derive actual compiler-thread stacks from resolved CompileCore,
not guest names, busiest thread or return/parameter-type mentions. Preserve
missing/ambiguous/native/JIT/GC frames and counts. Samples are not elapsed phases
or exact removable CPU shares. Do not infer a tagged-value/compiler rewrite.

Inspect whether existing syntax-value cache structural hashing is a substantial
actual cost versus parse/analysis/executable construction. Source already shows
record-keyed caches; identity comparison is a hypothesis, not an assumed fix.
If no dominant removable mechanism is demonstrated, close without prototype or
retiming. Any selected design needs a separate frozen semantic/admission/token/
ownership contract and one much shorter complete before/candidate/control trial.
No reuse of B81/B83 failed timings or new dependency/core unsafe code.
