# Ordinary-helper attribution

R84/B447,2026-11-08. Qualified production91f15a3e, exact4c072fed core from the
verified immutable [combined checkpoint](../2026-11-08-runtime-checkpoint/README.md).
The maintained synchronous rsplit-helper text report is2.18–2.23×CPython warm in
that checkpoint. This profile does not isolate call overhead from string processing.

CPU-clock/FP199Hz,6guest-second warmup,2s settle,6s wall capture. Actual5.58guest
CPU-seconds/1,107guest samples;46unknown(4.16%). Diagnostic PerfMap3/JIT listings
preclude normal timing/speedup claims. Top self shares: instruction prefix19.06%
(includes inlined work), input handler3.70%, forward whitespace scan3.43%, hash3.34%,
Monitor.Enter3.16%, local-result completion2.80%, integer addition2.80%, lower2.62%,
split core2.62%, retirement2.35%, receiver lookup2.35%, native selector2.17%.
Inclusive shares overlap. No dominant binder allocation justifies arenas, universal
values or more memory machinery; workspace reuse/direct slots/presence bits exist.

The source gate selects R85 canonical receiver declarations: typed no-argument
string/dictionary targets coexist with ReceiverMethod and a compiler name-selected
load opcode; casing/encoding closures unnecessarily promote contexts. Qualify this
coherent removal with a separate source pair; no automatic native-proof expansion.

All source/core/auxiliary/Ready/runtime/pre-post goldens/quiet/Closed/exit/join/
fixture/archive gates pass. Persistent raw traces/logs/maps/unknowns are retained.
Temporary CLR maps were copied and verified before RAM cleanup; all workers joined,
fixtures removed/unmounted, archive verified under lease and independently locally.
120s owner+30stop+30margin. One-case catalog and reuse of the verified B446 core
archive keep historical measured trace/archive size+25% within8MiB above240MiB OS
floor. No evidence deletion. No active users remain.

[Complete evidence](observations.tar.xz) includes raw observations, contracts, sizing
and exact core/archive provenance. Core bytes remain in the linked immutable
combined-checkpoint archive, rather than being duplicated here.
