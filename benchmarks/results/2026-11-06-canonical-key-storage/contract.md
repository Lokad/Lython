# K01 — one actual-hash storage authority (before implementation)

Baseline db3d1908/P01; delivery42135195574 active. Existing source proof:
PyDict.NativeEntry owns native ordered payload/buckets, while ProtocolEntry,
ProtocolSideIndex, structural hash and snapshots duplicate protocol keys. PySet
has HashSet plus the same shadow/index. SetProtocolItem and AddProtocolItem
compute actual Python hash AND context-free structural hash; the latter can
still reject a valid custom dataclass hash or read fields unnecessarily.

Hypothesis: storing one actual normalized hash with the actual entry and its
current-insertion identity removes dual probing, structural rehashing, side-list/
index upkeep, and split native/protocol candidate scans. This is a semantic and
architectural reason to select the redesign; existing profiles do not establish
its share of complete-script time. A custom-key diagnostic gain alone will not
close the whole-script objective.

One coherent completion milestone, two source phases:
1. PyDict: make its existing native entry/bucket storage authoritative for every
   key, including contextual hashes. Store insertion identity with the actual
   entry for reentrant comparisons. Preserve native fast paths, raw bridge
   semantics, copy/merge stored hashes and ordered payloads. Remove PyDict's
   ProtocolEntry/list/side-index/structural-hash parallel state and split scans.
2. PySet: reuse the same key-storage authority, remove BCL HashSet plus protocol
   shadows/dual hashes. Delete ProtocolSideIndex and IProtocolSideEntry after
   their real consumers disappear. Extract only the concrete storage actually
   shared by dict/set; no universal dispatch framework, prehashed wrappers or
   dormant backend. Do not promote phase1 as completion of K01.

The existing native table is the starting implementation, not another table
beside it. Entry layout may need an insertion sequence; inspect/measure any
plain-record footprint/CPU cost rather than hiding it in the estimate. No new
resource policy or per-object ownership ledger. Capacity, entry metadata and
replacement overlap admitted conservatively before publication. Existing owners
fund source snapshots and pending users; clear/release/refusal rollback remain
native cleanup. No forcedGC/tracing/retries or new memory-management infrastructure.

Semantics: actual hash invoked once per ordinary query, stored hash for copies,
identity/NotImplemented/reflected equality, collisions, insertion order, clear,
remove/reinsert, positive insertion answers versus current lookup/removal hits,
mutation during comparison, malformed hashes and first structured failure. Async
comparison may suspend: explicit context/token/ownership across await, no retained
ambient assumptions. Context-free internal construction and live namespace/env
bridges need named handling, not silent Python dialect changes.

Affected scripts: CSV/JSON grouping with compound/custom/dataclass keys, record
construction and text dictionaries; sets/Counter/views share acceptance controls.
Use existing tests plus independent Python goldens for overridden hash over
unhashable fields, nested tuple/slice keys and reentrant insertion lifetimes.
Source gate is bounded: inventory actual APIs/bridges, confirm a small golden set,
then implement the canonical store. Stop/refine if the proposed path retains a
second authority or cannot preserve these semantics; do not start another broad
ownership audit. One focused correctness/token/admission run per source change;
no blanket full-suite replay while incomplete.

Before promotion: one bounded VM source pair, unchanged-current controls/CPython,
complete grouping/record script and native plain-record/text controls, warm plus
compile-to-result when preparation changes. Preserve all adverse observations.
Allow justified confirmation/refinement, no fixed percentage veto. Decide explicit
regression debt at completion. Batch platform/stress/installed consumers only at
that coherent boundary. Publication remains postponed.

Implementation refinements from actual source:
- NativeEntry stores Sequence before hash/links/Protocol to keep modeled physical
  layout40bytes; slot estimate168includes existing128byte pair headroom. Attach
  explicitly admits the old native array during replacement construction.
- Class slots are mutable. All PyInstance keys (transitively through tuple/slice)
  therefore remain contextual; two MRO probes per classification disappear.
- Existing global/object/env bridges have real callers and live values. Extend
  their existing raw-storage contract with stored-hash metadata and known-hash
  append; preserve a single query hash across bridge lookup/publication. Existing
  native entry owners retain that metadata, not another bridge-side ledger.
- Snapshot bridge hashes before callbacks, so removal cannot trigger structural
  rehashing of an already captured protocol key. No fallback admission/retry.
