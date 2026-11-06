# R20 source gate — shared immutable record keys

Status: selected design; **not implemented or performance-qualified**. Build on
the canonical dictionary, ownership scopes, executable engine and native helpers.
No new public API, configuration, mapping backend or memory infrastructure.

Hypothesis: repeated JSON/CSV records pay for rebuilding identical native key,
hash, bucket/chain and insertion metadata. Sharing that immutable structure while
keeping each row's values independent can remove work and storage across complete
record/report scripts. R19 observes material JSON reading, but does not establish
that this metadata is its entire cost or predict a speedup.

## Coherent migration and deletions

1. Split the existing native table once into key metadata and values. Replace
   `NativeEntry.Value` coupling throughout PyDict.Storage/Query/copies/mutations;
   keep the real namespace bridge. Preserve stored hashes, insertion identities,
   protocol callbacks, necessary candidate/value snapshots during suspension,
   and the existing public container. Do not retain the old
   table as a second selectable backend. Reserve both arrays before allocation.
   Key-only set storage should omit its unused value payload; dictionaries retain
   independent mutable values. Include sets in storage/admission regressions.
2. Allow a completed string-key record's native key table to become immutable,
   with one existing execution/cache owner. Each row retains its own admitted
   values. Existing-key replacement needs no key clone; key-set/order mutation
   first admits private replacement metadata, then performs the actual change.
   Shared metadata remains funded for surviving rows and aliases. No refcounts,
   identity ledger, weak reclamation or reachability tracing.
3. JSON construction captures one immutable layout hint before parsing. Reuse its
   native names/table on matching records; differing order/names/duplicates remain
   ordinary correct dictionaries. Reentrant/suspended parsers retain their own
   funded hint. Replace ExecutionState.JsonRecordKeys' separate sorted name array,
   comparer/sort and duplicate name-lookup machinery with the actual immutable
   layout. Do not introduce a second schema index beside the old hint.
4. Inspect CSV DictReader for reuse of the same private layout where current
   field names are exact immutable strings. Mutable/custom names and duplicate
   columns must retain their Python behavior. CSV's transferred record arrays,
   existing bulk admission and direct input paths already exist and remain.

Expected savings: per repeated row, replace repeated 40-byte coupled metadata
slots with value references and a shared table; avoid reconstructing those key
hash/chain/insertion fields. Account for every actual new shell/array and changed
lookup, rather than assuming the full parser cost disappears.

## Compatibility, ownership and regression debt

The ordinary native table gains a second array, with extra shell/locality cost.
This is explicit potential regression debt, not an automatically accepted result.
Conservative admission thresholds may change; preserve capacity and replacement
overlap, first refusal, original data on denial and aggregate balances. Fund the
layout through an existing surviving owner before sharing it. Never refund shared
metadata when one row mutates, clears, copies, escapes or is cleaned up.

Preserve insertion/overwrite/delete/reinsert/pop/update/copy/view order, hashes,
identity, cycles and side effects through custom equality and real suspension.
Keys and values of separate rows must mutate independently. Admission callbacks
can alter reachable data: resnapshot/revalidate before publication, never reuse
stale guarded values or replay guest work. Layout mismatch is ordinary data, not
permission to retry or recover after refused admission.

JSON acceptance covers nested/different/reordered/duplicate keys, raw prefix/error
positions, object/pairs hooks, numeric hooks, real async I/O and cache reentrancy.
CSV acceptance covers mutable/custom fieldnames, duplicate columns, extras/rest
values, quoting/newlines and current iterator semantics. Keep retained aliases,
cancellation and shared-budget cleanup/reuse through every changed path.

## Bounded completion gate

Complete canonical storage migration plus JSON sharing and deletion of the old
hint before deciding retention. Do not publish an indefinitely half-migrated mode.
Use focused dictionary/protocol/JSON/CSV/ownership/cancellation Release checks,
then one short source pair: complete report and JSON records, ordinary text/CSV
controls, warm/fresh separated where affected. Reuse existing workers, goldens,
normal GC/tiering and quiet/ownership/archive gates. A short allocation diagnostic
can verify storage removal separately from clocks. Keep every adverse result.

Decide ordinary-container regression debt at this milestone. Bounded refinement
or measurement correction is allowed for a concrete observation; no permanent
ban, fixed percentage veto or open-ended tuning series. Preserve a justified
simplification only with a clear remaining debt decision. Batch platform/Stress/
installed consumers at a coherent delivery boundary. Publication stays postponed.
