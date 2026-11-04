# D14 direct dictionary storage dispatch

Before code,2026-11-04. Current runtimeeff471b3/frozen9588b3c8; full41896187873
actual head verified, both OS .NET11 ordinary pass/.NET10 running at19:30UTC.
Prior turn completed real implementation/measurement/qualification progress.

Current source gate: ordinary PyDict owns its native table since B186 but still
sets _items=this and implements IPyDictStorage. Its own operations call the interface
back into itself, then branch to the native table. The interface also serves real
live module/attribute/environment bridges. GlobalNamespaceStorage._entries and
ObjectNamespaceStorage._extra always hold ordinary PyDict, using raw operations
intentionally: their outer view owns protocol/mutation handling. Preserve that split.
D12 identifies mapping/call/instruction costs, not a measured self-dispatch share.
No speedup predicted. Do not remove caller provenance/ambient cancellation checks.

Selected coherent cleanup: reserve IPyDictStorage for actual live bridges. Remove
PyDict's self implementation and _items=this bookkeeping. Ordinary owning operations
use typed private/internal raw helpers on the native table; an optional bridge keeps
its real forwarding. Narrow the two namespace extras to PyDict/raw helpers, preserving
raw traversal vs Python version-checked iteration. No wrapper, union, owner, table
backend, flag, opcode, arity series or public option. Native funding/capacity/copy/
attach rollback and protocol metadata are unchanged. Test add-vs-update behavior
through the necessary raw method, not through a discarded self-interface assumption.
Remove orphaned operations if no real callers remain; do not leave dead forwarding.

Acceptance: exact keys/order/duplicate replacement, hash/equality reentry/mutation,
retained iterators/aliases/views, live globals/locals/classes/environ, copy/attachment
rollback before publication, admission refusal/cancellation, actual async equality
and awaited cleanup/shared-budget reuse. Focused Release mapping/hash/namespace/
copy/storage/ownership families per actual11/10; no weakened assertions or new
architecture-only tests. Both-API actual suspension coverage stays active.

Freeze a nonincremental build after checks; one controlled ten-case complete-script
pair (CSV/text/JSON/report/config, Run and awaited RunAsync), existing drivers,
6s guest warmup/image/9rotated200ms batches, old/control roles, allocation/peaks
separate. Owned360s limit from prior8cases222s. Exact sources/loaded cores/aux/
CoreLib/hash/goldens/quiet/baseline/finalzero/joins required. No CPython refresh or
unchanged full qualification. Retain adverse data and decide added helper cost and
regression debt. Complete/delete self machinery or restore, then coherent delivery.
Publication remains postponed, regex track independent.

Other inspected options: general native proof/evaluation is real and guards precede
any native effects/admission; no safe complete deletion established. Compiler already
forwards adjacent operands and current hashes already use bulk HashCode.AddBytes.
CSV immutable field slices could save arrays but require new storage/charge-kind
invariants and larger string headers; current snapshots derive full capacity from
one owned array. Do not reintroduce B198-like ownership machinery on this hypothesis.
These are evidence-based deferrals, not permanent bans or fixed-percent rules.

Source cleanup follow-through before freezing: SetItem/EnsureCapacity/
ReleaseCommittedBytes are no longer used through the live bridge interface.
Remove those members and orphan bridge implementations. Keep actual native capacity
preflight on typed owners; centralize invalid live-view refund rejection in the
private native release helper. Namespace extras use the existing native SetItem
implementation directly, not a new mixed-mode setter. No guarantee is relaxed.
