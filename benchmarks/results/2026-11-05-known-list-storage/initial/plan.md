# D33 coherent list backing policy — before code

2026-11-05. D32 source/CPU gate on450b9162/coreD8: new complete ordinary-helper CSV
corpus passes68public cases per actual11/10 and independent CPython goldens.32s
capture/all identity/quiet/golden/join gates:1131ordinary/1013native samples.
Frame/binding leaf groups2.56/1.86% include useful work, and shared call entry
contaminates broad inclusive activation counts. No frame rewrite selected. CSV
construction remains16.36%leaf/36.60%inclusive. All unknown/maps/adverse history kept.

Hypothesis: known-size collection construction should allocate its actual requested
backing, empty lists need no element array, and clearing should relinquish capacity
without allocating replacement capacity. Current PyList constructor floors every
known size at8; every3-field CSV row/short split pays extra backing/headroom. It also
has a first-growth-at8 exception, and governed clear allocates/reserves8slots just
to become empty. Eliminate those mechanisms as one storage invariant, not CSV parse-
time fusion, another list backend, a capacity switch or a frame/ownership framework.

Concrete replacement/deletion:
- Respect explicit/known capacity (including zero); unknown/default starts empty.
  Keep one constructor and checked nonnegative representation bounds. Remove
  unconditional minimum backing allocation/actualCapacity floor.
- Empty growth starts at existing8slot heuristic; subsequent growth is uniformly
  geometric. Remove special first-growth-at8 exception. Actual old/new replacement
  overlap remains fully admitted, with retained capacity/scalar headroom unchanged.
- Clear and nonpositive in-place repetition share Clear. Drop the old array before
  refunding capacity, retain funded metadata, preserve independent value/alias
  owners. Delete ReplaceWithEmpty's fresh array/reservation/commit machinery and
  separate governed/ungoverned clear routes. No retry, optional-storage bypass,
  hidden allowance or cumulative allocation counter.
- Sorting already uses an empty callback view and retains its original storage.
  A unique managed zero-length array marks that detached view; restore compares
  identity, preserving mutation-then-clear detection without a per-list flag or
  replacement allocation. Clear of an already empty sort view stays inert.

Contract tradeoffs: known-small lists charge less initially; geometric growth can
retain/admit more headroom earlier than old special9slot promotion. Tight-budget
thresholds/effect timing can differ. Clear needs no fresh admission and may succeed
with a full allowance; existing already-terminal refusal still stops guest work.
Sort's original storage remains funded through actual callbacks/cleanup. No public
API/configuration change and no user-visible capacity limit. Fully managed only.

Focused validation: existing List/CSV/String/JSON/native-operation/cancellation/
ownership/refusal families per actual11/10. Update only floor-specific assertions
to enforce same-capacity funding independent of value kinds and actual admission
refusal. Preserve failure detection and semantic/cleanup assertions. Add bounded
full-budget clear/*=0 alias/funding/reuse controls, independent sort clear and
append-then-clear CPython behavior through both APIs. No timeout relaxation.

One <=150s four complete-script source pair: native-helperCSVwarm, ordinary-helper
CSVwarm, textwarm and reportwarmcontrol; two unchanged baseline roles, exact files/
CoreLib/keyhashes, normalruntime, full independent goldens, quiet/balances/joins.
Allocation, elapsed and retained peaks separate; compilation/fresh/async performance
not claimed by that pair. Stop retain/refine/withdraw with explicit debt decision;
bounded correction only for evidence. Full platform/originalStress/installed
consumers batch D31+this at meaningful retained delivery. No publication.
