# CSV writer owner qualification

Repeated plain and dictionary writer construction previously left committed
shell charges behind after the writers were dropped. Both fixed 5,000-writer
controls failed at a 262,144-byte cap. Writer-owned in-memory history also lacked
a reclamation snapshot; dictionary wrapper funding preceded child admission.

Each writer now registers one existing ownership snapshot for its shell and
history before publication. Dictionary wrappers own their 64-byte shell separately
from the child writer and field-name array. History growth refreshes the existing
snapshot. Wrapper funding follows child admission; denied registration refunds
only unpublished storage. Three creation paths share one registration factory.

This bounded correctness fix adds 39 production lines and removes 13. It introduces
no new accounting tier, scalar identity map, lifetime abstraction, finalizer or
configuration. Ordinary registration metadata can reject a tiny-budget request
earlier. Allocation preflights, live-memory protection, managed stack admission,
capabilities, cancellation and forced exhaustion relief remain enabled.

The committed Release probe completes both unchanged controls with output 4999.
New white-box checks cover dropped versus retained history, child denial and
exact shell/wrapper registration refunds. Eighteen public cases cover dropped
writers/history, retained writers/bound methods/output, escaped getvalue output,
callback failure, genuinely delayed host output and write cancellation. The churn
async cases yield once before the pure loop; real write cases separately yield
inside host I/O. Existing getvalue returns no trailing terminator; an initially
incorrect newline oracle was corrected without changing rendering semantics.

All 10,458 Debug tests and 105 focused Release checks pass. Source/binary/receipt
identities are recorded in evidence.json.
Original failures and committed probe outputs are preserved alongside this report.
Exploratory candidate and initial oracle-failure receipts remain private.
The original exploratory control log did not record its binary hash; it is
historical fault evidence, not a matched timing or binary qualification pair.

This closes the native CSV writer ownership fault. It does not remove the
weak-reclamation dependency on forced CLR collection or establish a performance
or embedding-latency improvement. No VM sample or comparative lane was run.
