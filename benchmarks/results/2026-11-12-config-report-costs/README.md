# R275 — configuration and report cost source gate

2026-11-12, frozen R274 source43541e78; fixture-only correction1011ed0b leaves all
568production files unchanged. Full correction qualification is active. Fixedfec79
worker/adapter0.4, .NET11RC3, normalGC/tiering plus perf-map generation only;
Ubuntu24.04.4/F4as_v7. These are sampled cost diagnostics, not elapsed comparisons.

Reuse exact existing config-edit.small (16paths/181B), report-render.large and its
UTF16 variant (3,072records/189,441B). Four boundaries: config compilation, config
compile-plus-one-run, report UTF8 warm and UTF16 warm. Captures total23.725seconds;
roughly six seconds each after twelve-second warmup. Full stdout/file goldens,
shared-budget/Closed/zero-exit/join, quiet, archive/localSHA and fixture-unmount gates
pass. All maps, raw perf data/stacks, thread CPU and unknown samples survive.
Terminal cached; no remaining users or sole RAM evidence.

Compilation runs on caller/threadpool threads, not the dedicated interpreter
worker. Keep all active caller/guest/GC CPU; historical guest-cpu-leaves filename
has explicit widerWorkerBoundary/profiledTids/guestTids fields. Compilation has
1,213samples/14.18%unknown; fresh1,182/7.45%; UTF8report1,284/8.72%; UTF16report
1,372/8.89%. Fresh includes substantial kernel/path/reset work outside guest clocks.
Do not turn aggregate leaves into removable-time forecasts or new CPython ratios.

| Boundary | Selected leaf evidence | Source decision |
| --- | --- | --- |
| Config compilation | Expression traversal4.62%, binary-search3.13%, parser primary2.89%/call2.64%/literal2.14%; other work distributed | No demonstrated dominant duplicate compiler pass. Current shared compiler facts/annotation demand and retained-lowered cleanup already exist. |
| Config fresh | Caller/threadpool CPU4.20s, interpreter1.88s; kernel/path work prominent | Builtin descriptors retain per-run owning types. Existing immutable functions/signatures are shared; do not promote the mutable graph from source presence alone. |
| Report UTF8 | Instruction prefix10.20%, field append4.83%, write state4.21%, JSON construction4.13% | Frame/dispatch work is real, but owned workstate already avoids lexical-context materialization until required; no proven duplicate complete frame to delete. |
| Report UTF16 | Instruction prefix8.38%, write state6.63%, WriteEncodedText5.69%, field append4.01% | Source proves manual scalar decoding/surrogate/unit emission, and separate character-count plus UTF16-sizing scans. Select bulk managed transcoding/shared count-sizing as the next complete encoder stage. |

The UTF16 encoder already writes from UTF8 without CLR-string conversion; that
hypothesis is ruled out. R276 must remove the manual writer and duplicate file
count pass, plus three unused reservation/count helpers, without adding a pool,
policy or backend. Preserve exact Python counts, BOM/endian/newline behavior,
validation before effects, active tokens, funded buffers/overlap and awaited users.
A bounded complete UTF16-report/UTF8-control comparison decides the implementation;
these sample shares do not predict a speedup. No new diff work: existing qualified
single-edit patch evidence is already ahead of CPython and remains sufficient.

B629 first launch fails before any sampling because of an inherited script filename;
its original scripts, empty-fixture cleanup, journal and archive survive. B630 fixes
script/catalog paths and preflights referenced files, then completes all four
captures. No runtime change or design inference follows from the launch failure.
Persistent raw/catalog/worker storage is separate from RAM guest fixtures. Existing
240MiBOS floor remains; profile evidence headroom is raised to64MiB. No benchmark
image/evidence retirement. Publication remains postponed; whole-script goal stays open.
