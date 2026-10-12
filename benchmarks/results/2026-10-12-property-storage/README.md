# Property storage simplification and collection dependency

Properties now use a 128-byte shell plus conservative storage per numeric field,
replacing Lython's final scalar coupon/refcount dictionary. This removes **103 net
production lines**, including the entire 111-line AdoptedScalarCoupons class.
Six tests solely exercising that deleted helper disappear; surviving container
storage tests retain their inputs and limits.

Each numeric accessor/documentation field pays 64 bytes, including Boolean and
all supported CLR integral ingress values. Repeated aliases pay per field.
Ordinary callable/string/None metadata retains its existing shell/payload policy.
Large integers keep their separate payload ownership; an unowned large magnitude
also contributes its byte-count estimate. Construction admits the whole field
subtotal before publication, with existing allocation/registration refunds.

Numeric documentation replacement transfers funded storage without temporary
extra admission. Shrink/deletion releases the outgoing field fee and refreshes
the existing ownership snapshot. Positive growth denies before metadata changes.
The stricter estimates change tight-budget acceptance and accounted peaks.
No cumulative allocation cap, public switch, owner hierarchy or new tracking
tier is introduced. Host mediation, cancellation, stack guards, cycle handling,
public projection and exhaustion GC remain unchanged.

Implementation: 6963be02; escaped-payload test refinement: bdb3fe9a.
Qualified production tree: a03c7bf73723df522eb45292d5dc4a8056bbe836.
The [evidence](evidence.json) pins source trees, actual Debug/Release binary hashes,
terminal focused process receipts and complete-suite TRX hashes. Debug public
library/probe outputs were built at 6963be02; the later test-only commit rebuilt
white-box output. Release outputs were built at bdb3fe9a.

Validation passed **10,231 complete Debug tests** (2,037 white-box, 8,194 public)
and **107 focused checks per configuration** in Debug and Release (71 white,
36 public compatibility cases). Focused groups take 1.2–2.3 seconds including
cleanup. Coverage includes every integer ingress kind, double/decimal, alias
identity, tracked/untracked large magnitudes, saturated replacement, composite
construction denial, failed registration, deletion/snapshot/drop, escaped owned
large documentation after real descriptor collection, getter callbacks,
decorator derivation and genuine asynchronous access/cancellation.

The frozen original property's four-case assessment has two proposed-policy
failures and two passing controls. Shared numeric fields formerly paid one
coupon; replacing a distinct numeric doc at a saturated budget needed an extra
coupon. These are accounting-policy/headroom observations, not Python semantic
bugs. Empty-shell and positive-growth denial controls pass before and after.

A separate, rejected experiment confirms that removing forced GC alone is unsafe.
Its original producer uses the already qualified dictionary production
5f6bf332; an isolated variant deletes **only** GC.Collect().

| Fixed 4,000-row six-field CSV scan, 256 KiB cap | Original | Delete forced collection only |
| --- | --- | --- |
| Drop rows, Run | Success; 33 generation-2 collections | MemoryError; 0 collections |
| Drop rows, RunAsync | Success; 33 generation-2 collections | MemoryError; 0 collections |
| Retain rows, Run | MemoryError; 1 collection | MemoryError; 0 collections |
| Retain rows, RunAsync | MemoryError; 1 collection | MemoryError; 0 collections |

All four cases execute serially in each isolated test process. Original and
rejected-variant groups finish in 1.873 and 1.469 seconds including cleanup;
their actual binary hashes, unchanged caps and raw outcomes are retained.
RunAsync here uses a synchronous pure source; genuine I/O suspension is not
claimed. Counts demonstrate the current reclamation dependency, without
attributing pauses or comparing execution speed. The rejected variant is not
integrated. Iterator advancement cannot release rows or fields that guest code
retains; forced-GC removal awaits a validated live-storage invariant.

This is a simplification/correctness round. No performance benchmark, quiet-VM
run or full comparison lane was collected, and no speedup or GC-removal claim is
made. Broader ownership/reclamation and benchmark plan work remains pending.