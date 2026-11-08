# Current complete-text CPU assessment

2026-11-08. Committedb1e17b64/frozen93e926fe; production inventory matches.
The same source now passes [full platform/Stress/installed-core delivery](../2026-11-08-primitive-helper-delivery/delivery-ci.json). No new runtime change or requalification.
Existing text-summary-helpers script3072lines/35840B, full stdout/counts report,
same frozen7cf worker/host/optional0.4. Quiet F4as_v7/.NET11RC3,6sguestwarmup,
2ssettle,6swall CPU-clock/fp199Hz,64completeinvocations/request. PerfMap/JIT
diagnostics: **not normal elapsed evidence**. R108 profiled report, not this text.

5.924guest-clock seconds/5.85guest CPU,1160samples/67unknown leaves(5.78%).
All source/core/aux/Ready/fullgoldens/quiet/Closed/exit/join/maps/fixture/archive/
local verification gates pass. Live120sowner captured; no users or owned RAM remain.
Temporary CLR maps copied/hash-verified after users ended before owned unmount.

| Leaf work | Samples / share |
| --- | ---: |
| Instruction prefix |218 /18.79%|
| Input handling |69 /5.95%|
| Structural hash wrapper/native hash |48 /4.14%|
| Whitespace collection |41 /3.53%|
| Local result publication |38 /3.28%|
| Dictionary get |37 /3.19%|
| Exact small-integer add |36 /3.10%|
| Native call selection |35 /3.02%|
| Subscript store |31 /2.67%|
| Native evaluator |30 /2.59%|

Casing/split group97leaf8.36%; native type proof7leaf0.60%; callbinding and context
construction0leaf. Monitor enter/exit50leaf4.31%resolve to MemoryGovernor.Reserve
through split/input string allocation, not per-fragment commits or dictionary
scalar replacement. Groups overlap; inlining/unwind limits apply. Prefix samples
include useful operations/checks/retirement, not all removable dispatch.

Source assessment rules out proposed duplicate phases: native string hashes
already use cached/context-aware values without general cycle traversal; scalar
dict updates use funded capacity; split fragments commit without shared-budget
resize; scopes share one lock. Direct supplied positional calls already suppress
repeated pre-frame proof via nativeAttempted. Raw method definitions were not
evidence that the measured custom call repeats three probes. Module mirroring is
selective. Inline UTF8 and final-fragment casing/split already remove old copies.

**No runtime prototype selected.** No demonstrated dominant redundant phase yet.
Do not add hash detours, instruction frameworks, credit policies or shared input
windows from these samples: windows change retained-buffer lifetime, and coarse
credit changes admission/refund timing for a small sample share. Preserve safety
checks and actual reservations. This is uncertainty, not permanent design closure.
Next: exact-source delivery/current CPython checkpoint, then bounded offline
prefix-PC/source analysis using this existing capture if justified. Before any
redesign, name whole work/storage eliminated and its semantic/ownership proof.
Custom async debt remains separate and unresolved; no broad CPU claim.

[Decision](decision.json), [attribution](attribution.json), [source audit](source-gate.md),
[identity](identity.json), [receipt](archive-receipt.json),
[all raw/maps/code/unknown evidence](observations.tar.xz).

Offline follow-up uses the **same capture**, with exact native-size/tier matching:
all218prefix leaves resolve to current Tier1 blocks. Hottest blocks include44
instruction fetch/refusal/cancellation,36loop control,30opcode range and9switch
jump-table samples. They are mixed work, not a measured removable dispatch tax.
The input handler is explicitly NoInlining to keep transient guest references
outside the long loop; its lifetime fence stays. The jumped state also carries
the existing asynchronous-disposal continuation, so it is not dead bookkeeping.
No extra capture or prototype; this source gate does not justify unsafe dispatch,
guard removal or a new interpreter framework. [Exact PC blocks](prefix-pc.json)
and [offline classifier](prefix-pc.py) retain the assembly and matching conditions.
