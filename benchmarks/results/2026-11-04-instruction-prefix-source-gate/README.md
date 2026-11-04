# Shared instruction-loop source gate

2026-11-04. Qualified runtime **f6e51d1c**, frozen core **df206f47**, documentation
d5ba1756. No production prototype. Complete ordinary text script/fixtures/goldens,
existing checked worker/protocol2 and external199Hz perf/frame-pointer collector.
Six-second warmup,2,048complete invocations, **6.13s guestCPU/1,218samples**;
owned collection **17s** below120s. Normal GC/tiering, explicit diagnostic
PerfMapEnabled/JitDisasm/JitStdOutFile only. Exact loaded identity, before/after
goldens/quiet gates, closed/reaped worker/collector and fixture removal pass.
Service inactive/success/MainPID0 and shared lease free. No source-pair timing claim.

[Plan before capture](plan.md), [loaded identity and overrides](identity.json),
[assessment](assessment.json), [raw maps/code/capture/source](observations.tgz).
SHA256 `55d087627b874b81afa574b5c78513affafa185df242f3ff599559871a9519ce`. Privileged archive reads root-owned perf data without resampling;
all evidence is persistent, separately from RAM guest fixtures.

## Observed work

The prefix has201leaf samples (16.50%). All map uniquely by actual method range
size to the saved **8,716-byte Tier1** listing and its basic blocks; no cross-run
address assumption. [Offline mapping](map-prefix.py), [all blocks/offsets/code](prefix-blocks.json).
Unknown80samples/6.57% and one incomplete final map line remain explicit.

| Basic blocks | Samples / all guest samples | Observed work |
| --- | ---: | --- |
| IG07/IG08 |79/1,218 (6.49%)|Loop/index bounds, instruction address and inlined admission/token checks|
| IG10/IG11 |34/1,218 (2.79%)|Opcode selection/jump table|
| IG04/IG05 |12/1,218 (0.99%)|Completion/increment control|
| IG93 |10/1,218 (0.82%)|Copy instruction and invoke prepared-operation path|
| IG40 |8/1,218 (0.66%)|Call receiver/target loading and shape checks|
| IG54 |7/1,218 (0.57%)|Native iteration source/slot loading|

Sampling skid and grouped/inlined useful work limit interpretation. None of these
totals is predicted removable CPU. Required admission/cancellation checks remain.
The source is unchanged since the earlier5c capture; current attribution independently
confirms a substantial loop share, rather than establishing any design's speedup.

## Lifetime map and next design decision

The existing engine already shares argument/local backing, prepares physical slots
and native source links, and forwards adjacent infallible inputs. Ordinary lexical
dictionaries are lazy. Captured contexts/cells can escape; merging them into a
transient interpreter would conflate lifetimes. Retirement disposes real private
cursor/materializer/builder values, and awaits pending cleanup. Store-local's
NoInlining bounds CLR transient roots. These cannot be deleted from generic totals.

The discarded target-scheduling revision shows that fewer async wrappers alone can
regress whole scripts. No new dispatcher, check suppression, tagged representation,
owner framework or blanket JIT hint is justified here. The bounded capture is closed.

Next source milestone: map complete compiler value production/consumption for
text/CSV/report/config, including fixed operands and contiguous calls. Determine
which leaf-load/copy/retirement instructions can disappear through one compiler
value-use design. Preserve failable-read/effect order, assignment expressions,
captured/mirrored/dynamic names, genuine suspension and cleanup. Existing sampling
does not establish a gain; require a concrete storage/instruction reduction and a
bounded controlled complete-script comparison before retaining any redesign.
Stop that source assessment with a selected design or a specific missing proof;
do not substitute another broad profile/local hint campaign. Full new-source
qualification follows only a coherent delivery; current runtime remains fully green.
