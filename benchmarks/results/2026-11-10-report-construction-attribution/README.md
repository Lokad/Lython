# R195 — current report construction attribution (partial)

2026-11-10, qualified runtime1a83c64f/frozenac6, same common worker and optional
adapter0.4. F4as_v7 Ubuntu24.04.4/.NET11RC3, stockGC/tiering plus perf-map logging.
Existing complete report-render.large and config-edit.small catalog; no runtime
source change, elapsed comparison or new CPython row.

**Incomplete collection:** report warm capture completed5.774s and verified/closed
normally. Compilation captured6.130s/147456invocations, then the collector's wrong
thread-selection assumption failed: compilation resumes on thread-pool threads,
not process main. The original record, killed worker exit−9 and malformed final
map line remain. Config warm/compile never started; final quiet gate and normal
compilation-worker close/post-verification are missing. Do not label all planned
qualification passed or infer ownership cleanup inside that killed process.

After the same owner ended, a fresh idle lease proved no users. Offline mapping
selects the three actual CPU-active compilation thread-pool threads from recorded
names/deltas, preserving the original collector record. Report uses its actual
execution thread. All unknowns, threadCPU, maps/stacks and malformed lines survive.
This correction needs no capture replay. [Attribution](attribution.json) contains
all selected top leaves, with original [partial raw evidence](observations-partial.tgz).

| Capture | Selected samples | Unknown | Source-supported lead |
| --- | ---: | ---: | --- |
| Report warm | 1104 | 70 /6.34% | ReadJsonString79 (7.16%), container remainder45 (4.08%), exact value21 (1.90%); formatting/VM work also material |
| Report compilation | 1218 | 178 /14.61% | Expression children56 (4.60%), complex statements14 across tiers, formatted children10; several distinct passes, no dominant duplicate proved |

Shares include useful/inlined work and are **not** removable taxes or speedup
forecasts. Warm runtime clocks include mediated RAM I/O, output validation and
prior-result cleanup, exclude fixture reset/compilation. Compile clocks include
construction and prior-script disposal. Wider CPU also includes reset/protocol,
GC/JIT. Startup/actual async latency are unmeasured. BackgroundJIT/BGC deltas stay
visible. No source gain or reference-equivalence claim follows from these profiles.

Source gates: direct valid UTF-8 strings, same-storage CSV input/row adoption,
completed JSON child loops and prepared physical operand positions already exist.
ExecutableStackLayout.Prepare runs once, not twice. Frontend child traversal is
material but several analyses have distinct semantics; do not manufacture a
duplicate-fact/caching project from its presence.

Next coherent hypothesis: replace recursive JSON container result/continuation
transport with one explicit construction operation, retaining scalar parsers and
actual suspension/callback order. Name displaced recursive helpers and preserve
depth/errors/raw_decode, current cancellation, terminal refusal and funded retained
containers before editing. New operation/frame storage must be admitted; there is
no allocation/speedup claim yet. Define debt and whole-report/JSON-lines/config
controls; broader performance and independent regex work remain active.

Recovery joined all users, copied/verified temporary maps before retirement,
recorded and removed only this dead worker's FIFO/socket diagnostic endpoints,
unmounted empty owned RAM, archived under lease and verified local SHA. Free disk
after archive264101888B, above240MiB floor. Exact-canonical terminal B542 archive
and withdrawn8f aliases were retired only after canonical SHA proof; qualifiedac6
survives. [Recovery receipt](partial-archive-receipt.json) reports complete=false.
No service restart, source promotion, full-suite replay or publication occurred.
