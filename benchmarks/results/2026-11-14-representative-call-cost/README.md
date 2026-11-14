# Representative call/record source gate

R363/B749, 2026-11-14 UTC. Committed fa40d674 / frozen73a1b233, full
43625900419 active independently. Quiet F4as_v7 /Ubuntu24.04.4 /four EPYC9V45 /
.NET11RC3, fixed workers/adapter0.4. Two owning five-second 199Hz cpu-clock/fp
captures after eight guest seconds warmup. Normal GC/tiering plus PerfMap only;
diagnostic attribution, no source-pair/allocation/CPython comparison or forecast.

| Complete workload | Guest samples / CPU | Selected leaf owners (includes useful/inlined work) |
| --- | --- | --- |
| 4,096-record invoice |1,062 /5.34s;75unknown,0ambiguous|NativeRunCore130;ordinaryprefix93;nativeinteger65;input44;CSVFeed38/Finish35;nativecall37;StartInline23/StartNativeCall17|
| Early-exit file search |754 /3.82s;167unknown,0ambiguous|ordinaryprefix67;StartInline55/StartNativeCall49;nativecaller36;ForNext35/nativeRunCore35;input31;nativecall20|

Search has128 kernel-unknown leaves. Its wider worker also uses threadpool CPU
0.77+0.44+0.08s, retained separately from selected interpreter CPU. Do not assign
unknowns or turn nearest owners into removable shares. File windows already share
admitted backing; CSV already bulk-admits independent fields and adopts rows; final
argument slots and native tuple-return transport already exist. No source reason
to recreate those mechanisms or revive the slower frame scalar/call-arena designs.

Select R364: one synchronous helper-activation entry across stack-backed small
calls and already-bound calls. Move the existing fixed values/binding into the
same owner as native execution/transfer/cleanup. Delete StartInline and the
intermediate nullable BoundCallArguments copy; retain actual existing storage
ownership and generic pending frame route. Existing overrides/wrappers may forward
to this one entry, without a controller, pool, new frame/value representation or
unbounded stack buffer. The two start methods account for104/754search and40/1062
invoice leaf samples including useful binding; no speedup prediction follows.

Semantic acceptance: positional/default/keyword/receiver binding, mutated defaults,
closures/protocols/errors/evaluation order, exact transfer PC, actual pending I/O,
tuple/scalar aliases, managed stack, cancellation/first-refusal cleanup and funding
until all users join. Preserve wider helpers/callbacks and both public APIs.
Existing focused actual11/10 checks and source-based Python controls first; bounded
complete search/invoice/text/JSON/counter/short/observed before/after, separate
allocation. Finish with retention and explicit regression debt or full withdrawal;
remove displaced entry/descriptor, not another wrapper around both old stages.
Temporary setbacks justify bounded source refinement when evidence supports it.
The wider performance roadmap stays open, publication held.

All image/golden/file/budget/quiet/join/persistent-archive gates pass. Traces are
SHA-verified under the lease before RAM unmount; no users/fixtures remain. Current
images/catalog/toolchain and254/252MiBfloors unchanged; no retirement. Raw SHA256
`502ee85dae23fc0f150df53a949c566110fff2afc6efd730d957347216f74133`.
Initial B748 fails before sampling because the recorded-options inventory still
names removed JIT settings. Original scripts/journal/fixtures survive in the
failed archive; all users join and RAM bytes verify before teardown. B749 corrects
only that inventory and adds an exact pre-worker options preflight. The early
local observation guard also prevents a remote query. No runtime inference from
collector errors, no changed timing boundary or repeated successful collection.

[Invoice](attribution-invoice.json), [search](attribution-search.json),
[raw](observations.tgz), [failed conditions](failed-collector.tgz),
[collection/analysis](collection-and-analysis.tar.xz).
