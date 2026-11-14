# Captured-text instruction attribution

2026-11-14UTC, B712/R342, exact committed2f606519/frozen53becdb6. One5s199Hz
cpu-clock/fp capture after8guest-second warmup on the quiet F4as_v7, unchanged
complete3072-line/35,840B captured text script and fixed worker/optional0.4adapter.
Perf maps and same-process JIT disassembly are explicit diagnostic overrides.
This is owning-source attribution, not a timing/allocation/CPython comparison.

963guest leaf samples/4.85guest CPU seconds;74unknown leaves(7.68%, including30
kernel), zero ambiguous leaves. Dictionary-loop Tier1 has168samples(17.45%). Its
unique same-process map/disassembly both have9805bytes, and every sampled IP maps
to an instruction group. The initial mapper rejected a zero-length group sharing
the next label address; preserve that failure and correct only the nondecreasing-
offset check. Empty groups map zero samples; no recapture or raw alteration.

Largest groups mix live instruction metadata, compact-word transport, hashing,
bucket/key probing and machine-counter updates. They are useful or mixed work,
with no single dominant complete call/frame/packet to remove. Stop this gate
without another entry/default-cache/argument-form tweak. Sample skid/inlining
limits source granularity; zero samples do not establish zero cost.

The wider profile identifies94leaves(9.76%)in cased-split preparation,53(5.50%)in
the case writer and18(1.87%)in scalar case mapping. These are separate leaves,
not removable shares. Inspecting the current pipeline shows a stored word-range
inventory followed by per-word casing/compact-byte transport. R230's historical
prepared-range reversal and R293/R324's delivered cache/cursor changes are reviewed.
They establish storage/scan tradeoffs, not a permanent ban on a changed pipeline.

Select R343's bounded bulk UTF8 representation redesign: case once into the owned
reusable cursor buffer, scan final bytes for preflight and consumption, and remove
range inventories/growth/copy/clear machinery. It must preserve eager all-word
admission, retained independent words, active/nested loans, current cancellation,
first refusal and reentrant/awaited cleanup. Extra scans and long-word scratch are
explicit risks to measure, not a speedup forecast. The existing cursor loan remains
one funded owner; no new pool, tracer, policy or backend.

All complete-image/golden/balance/quiet/join/archive gates pass. The120s owner
finishes cleanly; every RAM trace is independently archived/verified before unmount.
No workers/fixtures remain, no image/evidence retirement or VM-floor relaxation.
The measured R341source now[passes full delivery](../2026-11-14-shared-integer-chains/delivery/README.md);
qualification does not turn this diagnostic capture into timing. Publication held.

[Groups](dictionary-loop-instruction-groups.json),[wider attribution](attribution-captured.json),
[raw](observations.tgz),[tools and initial mapper correction](collection-analysis.tar.xz).
