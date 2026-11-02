# B123 correctness refinement — bounded scanner checks

Initial two-block complete-workload source pair and allocation diagnostic are
complete/preserved. Report elapsed about6–8% better than unchanged controls,
JSON-lines about8%; allocation–4.80%/–5.97%. CSV flat/text inconclusive. No erased
adverse control. Those observations describe initial-Deserialize.cs.

Review found an existing cancellation bug: modulo equality can miss every scan
checkpoint for an aligned run of two-byte escapes. The lenient rewrite and error-
coordinate translation loops have the same skip. Use threshold crossings in all three,
bounded at1024 input bytes plus at most one skipped byte, with checked-safe end
arithmetic. No checkpoint counter, timer, configuration or runtime hook.

The direct native-scanner regression fills available budget only after admitting
the input, cancels, then invokes the actual scanner independently of its caller's
entry check. Assert structured cancellation before any decoding-storage refusal.
This is a memory/cancellation outcome, not a checkpoint-count assertion. An initial
test wrongly expected CLR OperationCanceledException; the actual runtime uses
LythonRuntimeException with Cancelled attribution. Preserve that error, correct
the assertion, and rerun the old source to demonstrate SharedMemoryAllowance
instead of Cancelled before the crossing fix. Final source must pass on both
actual target runtimes with the existing JSON/callback/ownership suite.

Freeze with a nonincremental build after the old-source regression check. One
bounded confirmation on the two affected complete report/JSON-lines scripts,
two blocks with independent unchanged controls, same conditioning/protocol.
Do not repeat unchanged CSV/text jobs; retain their original controls. Reuse the
same two-case complete-job allocation diagnostic. Preserve initial and refined
images/hashes/raw data. Decide retention/debt after this correctness refinement;
no further local tuning campaign or claim closing the performance objective.
