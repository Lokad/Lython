# D46 evaluated call operands and instruction metadata

2026-11-05 before prototype, source9c1d7e74/frozen70e608fc. No VM run yet.
D44 fully local-qualified and full CI running; D45 focused107internal/366public
CSV per actual target and18 final escaping-result cases pass. No performance
claim from source/field counts. Preserve B61/B69 measured slowdowns, D39 map and
D43 diagnostic sample limits. Hypothesis: eliminate duplicate call transport and
retained per-interpreter fields without another activation carrier/arena.

Paused _instructionIndex stays at Call/Subscript until ResumePendingCall succeeds.
CallSite/start/span already reside in immutable instructions/code. Evaluated
ordinary callable resides in an owned operand; a bound method retains function
and receiver immutably. Derive these after existing pre-child awaited cleanup;
never reread a guest name/member. Keep actual lexical context/linear child frames,
final argument-local storage, native borrowing/returns and child creation order.

For type subscriptions preserve selected callables rather than redoing MRO or
descriptor lookup after cleanup. Store a selected ordinary subscription callable
in one additional admitted operand. A raw descriptor getter needs two protocol
arguments plus exact selected function/receiver: compiler reserves four slots
above original subscript inputs, instead of two. Existing operand ownership and
retirement apply; no tuple/bound-method carrier added merely for transport.
The actual returned descriptor value still uses existing prepared subscription
state so generic and genuinely async callbacks retain their established route.

Delete _pendingCallSite/_pendingFunction/_pendingReceiver/_pendingCallStart/
_protocolCallSpan and derive immutable metadata/targets. Keep existing three
semantic call kinds plus None. Completed result uses Python None for native null
so null remains the existing no-completion state; no replacement phase object,
policy switch, interpreter backend, frame pool or child construction before cleanup.
Keep publication/operand retirement inside caller ExecuteUntilCall[Async], after
its async-disposal mode is restored. Error routing/span/annotation and entered
depth balances stay where they are. Generator and injected-error state survives.

Tradeoff: up to two extra object slots for frames whose static maximum is driven
by subscript scratch, balanced against five removed interpreter fields. Actual
CPU/allocation/accounted peaks need measurement; no arithmetic speedup prediction.
No universal tagged values or scalar-identity ledger. One complete-family change.

Acceptance: ordinary/bound/default/nested/native calls, evaluation order/rebinding,
raw descriptor getter and returned callable including AttributeError, mutations
during awaited cleanup, callbacks/closures/locals/generators/exception finally,
managed stack, cooperative cancellation/refusal and retained operands/results.
Use existing focused Release families both actual targets; add only demonstrated
coverage gaps. Before timing validate actual all-source diff and freeze identities.
One<=180s five-job native/ordinaryCSV/text warm/fresh/completed-host async plus
unaffected control as capacity permits, same workers/goldens/quiet/joins/balances.
Record source elimination, capacity tradeoff, elapsed/CPU/allocation limits and
retain/refine/withdraw with regression debt. No automatic threshold veto/retiming.
Batch full platform/stress/installed consumers at coherent retention. The whole
performance objective and independent regex track continue beyond this experiment.
