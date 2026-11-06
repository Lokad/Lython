# Next optional-adapter migration: native cursor working admission

Baseline dev44 packages are immutable, exact identities in identity.json;
84internal/421public per actual11/10 passed against core62141d4d. Normal0.4 pin
and core backend independence remain unchanged. No final safety/publication claim.

Use existing CreateMatchCursor(input,start,token,IUtf8MemoryAllowance) only for the
native range cursor. Bridge its covered storage to one existing Lython reservation
in the cursor's child owner. Resize an aggregate byte balance under a lock; no
per-buffer registrations/ledger, policies, retries, tracing or GC machinery.
Replace the cursor's subject-size working heuristic with native admission plus
a conservative fixed capture/result/adapter-shell reservation. Reserve range and
Lython conversion overlap before native publication; it must not become unfunded
when the producer releases its range staging. Keep the local current-token bridge:
dev44 still retains creation-token authority over advances.

Refusal is terminal: the bridge must throw LythonRegexException(MemoryAdmission)
at the first failed TryResize. Returning false can permit producer optional-storage
fallback; do not allow that to turn refused admission into a successful command.
Preserve/check the first refusal at operation exit if any producer catch can hide
it. Cursor faults/closes on refusal, all native users dispose before the reservation
and owner. Cleanup cannot observe cancellation or refund live native/storage users.
Use existing disposable scopes and reservation hooks, not a new owner subsystem.

Keep one-shot matching heuristics for now: DetailedData APIs do not expose the
working-owner override. Funded compilation's default invocation account is the
program owner, which may differ from Prepare's supplied working owner. Do not
silently charge the wrong owner, route owners through ambient/thread state, or
emulate fullmatch by filtering search. Whole funded-program/cache migration needs
the precise one-shot working-owner surface, not a blanket regex development stop.
String-name enumeration remains an adapter-local bridge; native UTF8 name lookup
alone does not replace the contract's complete name inventory.

Acceptance: independently supplied program and working roots; cursor scratch
funded until cleanup; earlier snapshots/subject borrows remain funded; actual
in-flight creation/advance cancellation under current tokens; refusal at native
admission stops the command even if guest cleanup returns; no later effects;
all balances return/reuse after success/refusal/cancellation. Existing focused
semantic tests both runtimes plus one short same-producer direct-cursor allocation
comparison. Separate bridge/contract correctness from producer performance/safety.
