# Same-process native Call result attribution

R530/B960,2026-11-18 UTC. Fully qualified **925397f65 / frozen2c6b304b**,
unchanged normal0.4adapter. One complete helper text normalization script;
eight guest seconds warmup/eight wall seconds capture with the existing profiler.
Declared managed perf maps and method-name Call JIT assembly only; normalGC/tiering.
6656invocations/7.622guest seconds,1609all-thread samples/289unmapped leaves.
Sampling includes reset/protocol/I/O/checks/cleanup, wider than execution clocks.

All157native Call leaves match a unique map range, exact code size and version
of the same process's **3574-byte Tier1** body. Tier0/InstrumentedTier0 assemblies
are also retained; no ambiguous/unmatched Call leaf is attributed. The hottest
block G_M000_IG98 at0x929 has53samples and performs two vector moves, shuttling a
16-byte result local before inlined publication. Other blocks include target reads,
signature checks, prologue/epilogue and useful receiver work. All raw offsets and
assembly remain in [raw evidence](observations.tgz).

This supports inspecting result transport, not declaring53samples recoverable
time: skid, call returns and inlining limit instruction attribution. No elapsed,
CPU speedup, allocation reduction or whole-call-entry removal is proved here.
Earlier slower caller windows/arenas/controllers remain relevant.

The selected R531 prototype borrows stable private result locals through native
and common synchronous publication, retaining snapshots at authoritative storage,
retirement/reentry/suspension boundaries. Its displacement target is intermediary
pass-by-value shuttles, not Python identities, resource checks or ownership.
No wider slots/name metadata, new runner, arena, cache, policy or framework.
Focused semantic/cancellation/refusal/retained-data/cleanup checks precede one
whole-script source pair with controls; actual assembly and measured debt decide
retention. This bounded step does not complete the wider performance objective.

The [initial launcher failure](../initial/README.md) remains preserved: tuple
environment key refused before worker start. B960 corrects that edit only,
reuses the identical verified image/catalog/golden, and makes no design inference
from failure. Quiet/Closed/join/SHAarchive-before-RAM-teardown/floors pass; no
replicas retired in the correction. Terminal cached; no unchanged qualification
replay. Core architecture and publication remain independent of producer releases.

[Instruction attribution](call-body.json),[all-thread attribution](attribution.json),
[identity](identity.json),[custody](archive-receipt.json),[procedures](procedures.tar.xz).
