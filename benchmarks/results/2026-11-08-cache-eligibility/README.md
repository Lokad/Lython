# Delete unused member-cache table acquisition

2026-11-08, R120. Baseline committed d05b3359/core19b8b5a5; source identity and
new frozen core are recorded separately. R119 full CI is independent, not proof
for this later source. No new VM run, compile campaign or backend qualification.

Consolidate the existing receiver exclusion: instances, types, super objects,
functions and contextual dynamic receivers never cache member values. Skip their
unused table acquisition. Preserve cacheable receiver admission before effects;
clear an existing stale entry after resolution without allocating a new table.
No new owner, pool, table policy, public option or replacement dispatch.

Focused Release24internal/214public per actual11/10 pass: existing descriptor/
receiver semantics, actual suspended callbacks, cooperative cancellation/refusal,
cacheable storage preflights and record admission callbacks/cleanup. Existing
five unchanged complete scripts run against isolated same-process images with
normalGC/tiering/full stdout/file goldens, zero balances and owned-fixture cleanup.

Custom-operator CSV allocation falls **1.71% across both APIs** versus d05, about
192KiB for4096records: one otherwise-unused48B table per multiply invocation.
Other cases allocate near baseline. This is deletion/allocation evidence, **no CPU
speedup claim**. Desktop timings/control drift remain visible, including sync
text+1.92–8.53% and async custom+0.26–1.38%; they do not establish causal changes.
Compiler and static metadata are unchanged, so no repeat compile qualification.

Retain the cleanup with no CPU claim. Full platform/Stress/installed-core checking
of this source is pending the next coherent delivery boundary; no publication.
Do not net independent source pairs or silently settle earlier regression debt.

[Identity](identity.json), [raw assessment](assessment-local.json),
[design](design.md), [source patch, binary and logs](observations.tgz).
