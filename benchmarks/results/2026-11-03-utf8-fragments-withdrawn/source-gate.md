# B149 source gate / explicit storage contract

Baseline d7b715d3. This is a bounded prototype, not a retained performance claim.
Current text guest CPU includes10.60% whitespace splitting; arrays/copies also
serve contiguous slicing, strip, CSV fields and stream fragments. Use one native
UTF8 window representation and the existing batch owner, not a second VM or pool.

ReadOnlyMemory<byte> replaces the whole-array field. Local scalar length/offset/
hash caches remain local to the value. Each view admits its128B conservative
shell/cache estimate; the original payload keeps its existing batch funding.
Copy on foreign/unowned-to-owned boundaries. Noncontiguous steps still copy.

Two exact release boundaries matter:

1. Unpublished snapshots can refund a source after a helper's completion check.
   On first shared view, its payload must become conservatively batch-retained;
   source rollback may refund only its private shell/cache, never shared bytes.
   Views own no duplicate payload charge. A pair of local ownership flags is
   sufficient; no registration, identity map, tracing, refcount or new budget.
2. TextFileHandle.Read installs a decoded private window, records its charge and
   explicitly returns it at window turnover. Its four returned line/piece slices
   MUST remain independent copies before that turnover. Make that copy boundary
   explicit; do not silently turn those methods into sharing. This preserves
   reclaimable streaming scratch and retained guest line funding independently.

The source also has direct native warning and private cache-key refund paths.
Warnings never expose a guest string; keyword labels are newly constructed
private cache metadata. Do not change those lifetimes in this prototype. Verify
their existing refusal/cancellation/retained-cache coverage rather than expand a
general object-ownership audit. Public projection still copies into result owners.

Displaced implementation: contiguous guest fragment arrays/copy loops, including
bulk whitespace fragment construction. Keep one explicit private-copy primitive
for scratch-to-guest transfer and independent-owner boundaries. No dormant backend,
public options or tiny-size threshold policy. Whole object size and retained-large-
backing costs are explicit regression controls.

Acceptance: UTF8/scalar ranges and nested slices, Unicode split/rsplit/maxsplit,
strip/steps/hash/equality, declined complete-output admission before publication,
rollback after shared aliases, private reader turnover, current cancellation,
retained aliases/results/caches and baseline reuse on two executions. Compare
complete text/CSV/report warm+fresh with long-string/unique-key/ASCII controls
and allocation; retain source, adverse controls and any failed initial setup.
Retain/refine/withdraw by coherent value and remaining debt, not a fixed%veto.
