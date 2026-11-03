# B159 caller operands for eligible native function bodies — source gate

No prototype yet. Predecessor B158 is committed b3f1e9c3; delivery41625654805
proceeds independently. Existing B146 text profile has302/1377 interpreter-self
leaves,54 ordinary call leaves and506 inclusive BeginPendingCall samples; inclusive
counts overlap, and this is older than B151. They justify examining the full call
route, not predicting binding dominance or a speedup.

Current BeginPendingCall still rents/admit/fills bound storage BEFORE asking an
eligible module native body to run without an interpreter frame. It then clears/
returns that temporary. Generic and resumable frames need it; closed native bodies
already cannot expose locals, suspend or invoke guest protocols. NativeReturn
reads only Assigned.Length and Values[index], never presence bits or ownership.
ArgumentPresence is ALREADY inline for ordinary signatures: no boolean-array
reduction claim. Current positional binder already copies directly to final locals;
no redundant CallArgumentValue packets remain on this route.

Selected design to prove: consume a bounded ReadOnlySpan of existing fixed-capacity
caller operands for eligible native module-body evaluation. Convert the existing
native evaluator's input to that span, retaining a wrapper from genuinely bound
calls. No new arena, overlay, storage owner, tagged value, dispatch registry or
call-control backend. Try the shared pure eligibility/body guard before renting
bound locals only when the positional shape is fully supplied, receiver-free and
has no variadic/keyword-only binding. Defaults, keywords, methods, closures and
all resumable/protocol fallbacks retain the existing binder. Do not replicate
native expression evaluation. Remove BoundCallArguments from its private evaluator
rather than stacking a second execution path on top. Exact-arity direct callers
can remove copy/clear/rent/admission cycles; they introduce no resident storage.

Proof obligations: current frame/function entry and stack guards, traceback/active
exception behavior, cancellation, first terminal refusal, original operand owners
and result funding remain. Guard failure is pure and precedes work; never retry
through a binder after native work/admission begins. Borrow ends before caller
operands are removed/reused. No actual asynchronous callback may escape this
synchronous native eligibility. Existing actual async host/callback tests keep
covering the general route. A missing/extra argument uses the existing binder and
Python diagnostic. Mutation of defaults/callable binding cannot change eligibility
or evaluated argument order. Bound receiver/closure cases remain real callers.

Acceptance: independent positional/default/keyword/method/closure/error/identity
semantics, actual callback fallback/suspension, cancellation, terminal refusal,
retained aliases and reusable allowance. Use existing focused native return,
forwarding, argument-binding and ownership tests. Freeze one source pair after
joined builds; compare whole text/CSV/JSON helper scripts and an unaffected report
control, with allocation/peak checks. Choose RAM condition consistently and preserve
adverse observations. A short native-call diagnostic can distinguish eliminated
storage from CPU. No percentage gate; decide retained simplification, documented
regression debt, refinement or withdrawal at a coherent milestone. Broader frames/
operands and repeated frontend work remain open even if this slice succeeds.

Stop source work if proving eligibility needs a second binder, replay after effects
or a new lifetime graph. A bounded confirmation is allowed for unstable controls;
never restore the slower arena/call-control designs without new evidence.
