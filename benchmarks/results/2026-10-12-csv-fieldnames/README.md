# CSV field-name array lifetime correction

Repeated DictReader construction previously accumulated committed field-name
table charges after the readers were dropped. At a 262,144-byte cap, both inferred
and explicit-header 5,000-reader controls failed with MemoryError; the plain
csv.reader control and independent CPython versions completed and printed 4999.

The actual field-name arrays now register with the existing CallTemporaries
reclamation path immediately after funding, before publication. Empty tables
receive distinct arrays rather than the shared ToArray singleton. Retained
tables keep their charges; failed registration refunds only unpublished storage.
Header strings retain their existing payload owners. The redundant source
_headerRow holder and its async wrapper are removed: the returned row stays live
during conversion and the funded field-name table keeps its strings afterwards.

Production change: **12 lines added / 14 removed** in one file. No new owner
class, age tier, identity coupon, rollback record or configuration switch.
Allocation preflights, live-memory protection, managed stack admission,
cancellation and forced exhaustion relief remain enabled. Normal registration
metadata can deny a tiny-budget construction earlier; SPEC documents this tradeoff.

The original three fixed controls now all complete at the unchanged cap. New
white-box checks cover distinct empty owners, dropped/retained tables and exact
registration-denial reconciliation. Fourteen public cases cover inferred,
explicit and empty tables, failed writer construction, retained readers/header
strings and one escaped field after source abandonment, in both execution modes.
The async host genuinely yields once before the pure 5,000-reader churn; no
thousands of artificial I/O waits are inserted into the diagnostic.

All 10,436 Debug tests and 96 focused Release checks pass. Exact binary/receipt identities are in
evidence.json. Relevant gates include CSV abandonment, denial, headers, retained
rows, writer history, escaped output and the complete representative scripts.
No new timing, VM sample, CPython comparative lane or latency benefit is claimed.

A separate confirmed writer defect remains: successful 5,000 plain/dictionary
writer churn still denies at this cap. The writer shell (128 bytes), dictionary
wrapper (64 bytes) and retained in-memory history need actual owner snapshots and
failure/callback lifetime validation. This array correction does not solve those
charges or remove the weak-reclamation dependency on forced CLR collection.
PLAN retains the bounded next ownership work and unfinished milestone gates.

