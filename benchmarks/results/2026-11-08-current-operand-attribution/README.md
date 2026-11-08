# Current execution/operand attribution — 2026-11-08

R93/B454 at committed05d81b83/core68f63d29. This is a bounded source gate and
diagnostic CPU trace, not a runtime prototype or normal-settings speed claim.
The complete ordinary rsplit text script is unchanged from R90(1.80×CPython).

The source gate finds no duplicated common binding/frame phase: small presence
bits are inline, operands bind once into the final reusable workspace, that
workspace already supplies locals, and lexical contexts are deferred. General
callback routes still serve real Python behavior. CSV native parsing also already
scans chunks and compacts admitted input in place. No arena, new value carrier,
cache or bulk-scanning prototype follows merely from these sources.

| Visible CPU leaf | Samples | Guest share |
| --- | ---: | ---: |
| Instruction prefix, including inlined work |187|17.12%|
| Input handling |66|6.04%|
| Native return evaluation |44|4.03%|
| Local result completion |41|3.75%|
| Structural hashing |37|3.39%|
| Native call selection |34|3.11%|
| Native return guard |23|2.11%|
| Unresolved |51|4.67%|

Six-second wall capture, actual5.49guestCPU-s/1092samples, CPU-clock/FP199Hz,
six guest-second warmup, two-second settle. Ubuntu24.04.4/F4as_v7, .NET11RC3;
PerfMap/JIT diagnostics affect execution. Shares include inlining; inclusive
callers overlap and cannot be added as independent causal costs. This does not
establish boxing, binding, instruction fetch or any one helper as the bottleneck.

The current11957-byte Tier1 prefix has a unique method/tier/code-size match.
Its largest mapped blocks mix loop fetch(42samples), instruction/span loading and
inlined refusal/token checks(33), call setup(16), and dispatch range checking(15).
The input-handler listing was not requested, so its source sub-costs remain
unattributed. Different workload/PGO contexts prevent interpreting the earlier
CSV8677-byte prefix as a before/after code-growth comparison. No weakened
cancellation or extra profiler capture follows from this map.

All source/core/auxiliary/Ready/runtime/pre/post full output/quiet/Closed/exit/
join/fixture/archive gates pass. Exact existing B453 core/archive is reused;
temporary CLR maps were copied and verified before owned RAM cleanup. Persistent
raw stacks/maps/JIT listings, unknowns, source-gate notes and original sizing are
preserved in observations.tar.xz. No remaining VM users or fixtures.

The gate closes without a runtime redesign. A separately bounded existing
allocation-tick diagnostic can distinguish major scalar/array/storage traffic
before choosing a coherent removable phase. Sampled types alone will not prove
CPU causality or justify a universal value replacement. The broader performance
objective remains open.
