# R118 native mapping update: initial source gate

2026-11-08. Productionb1e17b64/core93e926fe is fully qualified; HEAD e340da93
changes docs/evidence only. No runtime prototype is implemented here.

Current complete-text evidence (R116) includes native hash/get/store work and
instruction transport, while ordinary call binding has no leaf samples. The
candidate is removal of a complete recognized get/add/store pipeline, not another
method inventory, universal value representation or call-control carrier.

Initial source facts:

- PyDict.TryWriteKeyWithoutAdmission already proves native string key, private
  native storage, no protocol keys and the same current MemoryGovernor. It hashes
  and probes, refuses growth before mutation, updates through PublishNativeValue
  and increments dictionary version only when count changes. Reuse these invariants;
  do not invent a new owner or skip foreign-owner funding.
- NativeSlotBytes funds conservative key/value capacity, and ordinary replacement
  uses StoreNativeValue. This does not by itself prove a fused arithmetic scratch
  or old/new magnitude overlap is free. Establish that proof before selection.
- Ordinary native dictionary get separately prepares its hash/probe. An existing
  immutable string key plus bounded native arithmetic may permit a single lookup
  and update without a callback between them. Missing/growing/protocol/large-number
  cases must stay ordinary before observable effects; no fallback after admission.
- Preserve builtin callable identity, RHS-before-LHS re-reads, checked local spans,
  cancellation before actual mutation, Python exceptions, aliases and iteration
  version behavior. Matching only syntax names is insufficient.
- A prepared guarded operation must stay on the existing executable engine and
  retain NoInlining/lifetime/managed-stack rules. No replacement dispatcher, policy
  option, shape cache, per-object ledger or new ownership machinery.

Next bounded work (~20min): inspect the existing compiler assignment/call lowering
and canonical dictionary operations, prove eligibility/funding/effect ordering,
and identify exactly which repeated lookup/call/result-publication work disappears.
Only then select a small coherent prototype. Stop this candidate if it requires
new ownership or semantic exceptions; retain uncertainty without a permanent ban.
Use unchanged complete text/CSV/JSON and custom/float controls for a selected
implementation. The performance objective remains active; no publication.


Decision at safe checkpoint: do not implement this guarded syntax fusion now.
Int32 native operands/result can avoid magnitude admission under GuardIntegerAddition,
and existing native value capacity funds small replacement boxes conservatively.
But preserving dynamic RHS/LHS reads, callable/frame semantics and unsupported
shapes needs a new conditional opcode/descriptor plus the original executable
sequence, updates to CFG reachability/retargeting/stack layout, local/cell/global
eligibility and separate proof spans. It removes repeated work for a narrow
aggregation shape while adding a second path. R116 does not establish that this
combination dominates. This is a complexity/evidence decision, not measured design
failure or permanent closure. No source edit, benchmark or requalification.

Selected next coherent prototype R119: flatten existing per-frame member cache
records into funded value entries and extend those same entries to record-field
resolution across immutable shared CSV/JSON key layouts. No new opcode, engine,
owner, hidden allowance, cache framework or arbitrary guest grammar fusion.
