# Shared-subtext qualification fixture correction

Initial **43814304379 at e1b94a47** failed12internal cases on each platform;
all17,914public ordinary tests passed. Terminal/logs cached, never requery.
The remaining framework/stress/package steps were skipped, so this is not full
qualification. Original logs and fixture sources remain immutable evidence.

The failed fixtures required old independent fragment payload sizes to admit/refuse
or trigger cancellation. Shared windows correctly avoid those copies. Update tests
to pressure complete window metadata, preserving first-fragment-versus-whole admission
and the original allocation bound, no publication, first origin and rollback checks.
Cancel on actual positive admission growth instead of a removed large copy; keep
assertions for current token, no further growth, joined I/O, depths and ownership.
Rename between-scan wording: the aggregate constructor checks cancellation at output
admission; these fixtures do not prove a separate in-flight scan interruption latency.
Existing whitespace-only current-token checks remain. The helper-return refusal
test now verifies the input governor funds the full backing and window metadata,
instead of requiring an extra copied text payload.

No runtime change or relaxed timeout/assertion.77focused and all4,800internal
ordinary pass eachactual.NET11/10; measured core remains1c8e1be5.606production/
build/test inputs frozen for the next delivery(586production/4build/16test).
No repeated runtime performance measurement. New full platform/stress/installed
Release consumer qualification is still required. Publication held.

[Checks](checks.json),[original/corrected sources and TRX](original-and-corrected-tests.tar.xz),
[initial terminal](ci-terminal.json),[initial full logs](ci-logs.zip).


[Corrected full Windows/Ubuntu actual11/10 ordinary/stress and installed Release
consumer delivery passes](../delivery/README.md) at6581454d/measured1c8e1be5. Capture-time
pending/failed states remain historical evidence; no runtime retiming required.
