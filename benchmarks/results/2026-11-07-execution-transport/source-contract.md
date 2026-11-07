# Canonical execution transport: source constraints

Current c4729a9f production already uses one final local/operand array, safe
adjacent leaf forwarding and direct local result publication. Closed native
helpers borrow arguments before ordinary binding; other eligible helpers defer
the lexical context. JSON already loops over completed children, entering async
continuations only for pending callbacks. File decoding already borrows unchanged
UTF-8 windows. These are delivered features, not opportunities to reimplement.

For a new transport design, a guest read is a value snapshot. A later callback,
host admission, suspended operation or descriptor may change the receiver/name.
Do not replace that snapshot with a later reread. Unbound-name errors retain their
original ordering and source span. The fixed-arity arithmetic forwarding pass
proves only adjacent infallible leaf reads, not arbitrary producer movement.

Result publication also owns resource retirement. Current CompleteLocalResult
keeps the following StoreLocal as the source-span and pending-cleanup continuation.
It drains/disposes the original input range before exposing a binding; genuine
async cleanup may defer that exposure. Removing that instruction requires a
canonical destination/continuation representation covering both completed and
suspended operations, not a second success-only fast route. Retained inputs and
result capacity stay funded until actual users finish. No admission retry.

BeginPendingCall handles ordinary/constructor/raw protocol callers, borrowed
arguments, defaults, managed stack/recursion, exception provenance and cleanup.
Its source already tries frameless native execution before binding when possible.
Any replacement must remove the displaced entire call phase and preserve all
of those obligations; a new caller arena/control packet is not a deletion.

Use the exact current-process mapping before selecting implementation. Aggregate
prefix samples include boxed-number tests, sequence iteration, result publication,
argument transport and useful semantics. Exclusive samples elsewhere do not add
up to the complete cost of a call. Statistical allocation type shares likewise
do not establish CPU dominance. Keep unassigned blocks and sample skid explicit.

After one bounded capture, choose a coherent source-backed deletion or state the
missing evidence and next alternative. Do not multiply profiles merely to obtain
a desired distribution. An inconclusive mapping remains uncertainty, not proof
that execution transport is cheap or a design permanently failed.

## Selected completed-call migration

B377 maps all233prefix samples out of1,095actual guest samples; IG07/08/09
combined118samples mix loop entry, instruction fetch, cancellation/refusal checks
and switch dispatch. BeginPendingCall/ResumePendingCall each14exclusive leaves.
Do not call the21%aggregate removable. No universal value/dispatcher rewrite.

Move the existing exact-positional/unbound frameless native attempt from
BeginPendingCall to TryExecuteNativeCall, before manufacturing pending state.
Delete that borrowed branch from BeginPendingCall; keep its post-binding native
attempt for defaults/receivers/raw protocols. Required full-count borrow failure
has already been proved read-only; retain the existing after-bind skip for it.
Calls not accepting direct positional binding cannot enter this relocation.

Input snapshots stay in the existing operand storage through the entire call.
Enter the original ordinary managed-call frame, check cancellation/refusal, execute
the existing native helper (including its function/recursive native admission),
leave ordinary admission, then publish/retire inputs. Translate raw callable
exceptions exactly as the old pending path did. Return false only after a read-only
proof or eligibility failure, never after refused admission or actual operation.
Never reuse a proof value after admission. Native operations still own their
preflights/rollback, and existing formatted-body exclusion preserves R26's fix.

No receiver insertion/default binding or actual suspension is moved. Pending
call state remains solely for operations still needing the existing runner. Test
genuine async fallback, closure/active exception/diagnostic behavior, terminal
refusal, cancellation and managed-depth restoration. Compare complete text/report
sync/async with ordinary CSV/JSON controls, exact allocation separately.
