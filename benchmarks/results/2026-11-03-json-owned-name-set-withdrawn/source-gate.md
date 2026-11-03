Selected source gate: the execution JSON-name dictionary stores a16-byte borrowed
memory key plus an8-byte PyString value, although the latter owns the same bytes.
Replace it with a HashSet of the already-owned strings and BCL alternate UTF8
lookup. Remove duplicated key/value entries and separate normal-key byte hashing;
reuse PyString's existing lazy hash for insertion and guest dictionaries. No new
PyString fields, universal key type, parser, pool, policy or owner. B139's larger
stored keys are not revived. A lookup-only stack request carries bytes and the
existing owner/span so the BCL comparer creation contract remains properly funded.

Affected scripts: repeated-schema JSON/report/config plus short/long all-unique
name controls. Preserve owned immutable names across mutable input/records, escaped/
Unicode/equal keys, duplicate ordering, hooks/reentrancy, active cancellation,
construction overlap, terminal refusal and result/cache users. Preflight the set's
conservative capacity before replacement; refund only the relinquished private
index backing. Do not change per-name alias funding or weaken denial assertions.

Completion: focused semantic/ownership/cancellation Release checks on both targets,
one frozen complete-script source pair and unchanged controls, plus allocated bytes
and admitted peak comparison. Baseline is committedbd3f9dee (B152 image already
frozen). Retain all adverse conditions; bounded correction/refinement is allowed.
Decide retain/debt/withdraw explicitly. This storage redesign proceeds independently
of B152/CI; do not claim full qualification for the uncommitted prototype.

