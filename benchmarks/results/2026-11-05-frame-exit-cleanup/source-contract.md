# D59 single frame-exit cleanup invariant, before implementation

D57 source restored, original engine still productionb69a0a49. Expanded routing
regression demonstrates FOUR failures: entry cancellation/refusal inside a guest
handler leaves State.CurrentException pointing at that abandoned handler after
sync/async execution exits. Regular operation errors restore it. Original logs
preserved; this is a demonstrated cleanup hole, not a CPU hypothesis.

Current AbandonFrame performs operand retirement, saved-handler clearing, handler
binding deletion and restoration of entry exception. It is called in five return/
routing positions but omitted by the two actual driver-exit finally blocks, which
separately retire/release storage. Consolidate that invariant at those actual exits.
Remove five return/routing calls, invoke existing AbandonFrame once when the driver
really ends (!HasYield && !HasPendingCall). No abandonment on yield, pending child
call or retained generator storage-only release. Active caller exception remains
correct across real async suspension and nested callbacks.

AbandonFrame no longer needs a passed span: only actual handler bindings require
one; use the current instruction or last valid instruction when needed. Public
empty scripts still have a Complete instruction; no per-frame dummy span allocation.
Retire operands first as before, but restore saved exception in finally even if
retirement/deleting handler names fails. Keep original reverse-disposal/pending-
cursor chain and actual awaited cleanup/funding. Do not change stack-release
failure behavior or introduce cleanup retry/early refund in this bounded patch.

Same Python routing functions and entry-vs-operation distinction. No terminal-
policy change, phase flag, execution strategy, owner, tracing, unsafe code, quota
or new configuration. Native cancellation/refusal remains terminal, first origin
preserved, explicit guest MemoryError catchable. NoInlining lifetime/store/worker
barriers remain: source history establishes useful retention fixes, so the D58
store samples do not justify deleting them.

Acceptance: twelve routing/first-origin/context/effect/balance cases, then focused
exceptions/finally/with/generators/real pending callbacks/shared-admission/native
returns and temporary/worker lifetimes on both actual runtimes. Preserve returned
aliases until actual cleanup; compare compiler/runtime spans and existing failure
propagation. Full local/platform/original stress/installed consumers at coherent
retained delivery. This correctness/consolidation change has no speedup claim;
reuse D58/B287 observations, no additional isolated timing. Larger whole-script
performance roadmap remains open after closing this demonstrated cleanup gap.
