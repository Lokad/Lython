# B161 outcome / B162 selected design gate

Identical B160 source873ae625/a890ccf; same synchronous-completion benchmark host,
normal GC/tiering, shared CoreLib/hash, full output checks and zero balances.
Awaited RunAsync costs ~21–23% elapsed/22–25%CPU for CSV/text/JSON and11/14%for
report versus first Run; controls−4.3 to+1.3%elapsed. Core hashes identical.
Text/JSON/report allocation differs only~0.02–0.03%, so per-op heap allocation is
NOT established as the explanation. CSV+6.58% (~88B per record) is a separate
representation/lifetime question. Accounted peaks identical. One68.584s service,
all joins/gates/goldens/cleanup PASS; no runtime code changed. No unchanged replay.

Already delivered: shared executable functions/native-helper return paths under
both APIs; native arithmetic/subscripts bypass async protocol machinery; startup
workers are reused/released through genuine suspension. Do NOT reopen those fixes.
Remaining source asymmetry: separate sync/async instruction loops, generic local
transfer dispatch under async, duplicate native routing and uncached general async
member lookup. Existing async loop still awaits operation helpers around ordinary
work even when completed. Source suggests overhead; no exact causal fraction.

Select coherent B162: one synchronous instruction-prefix driver serving both APIs,
with a small async outer loop waiting ONLY for actual incomplete operations or
pending cursor disposal. Preserve direct synchronous operations/guards. Delete
superseded duplicated opcode loop and duplicate exception progression. Existing
real operation helpers/abrupt routes remain. No new execution backend, native-op
catalogue, pool/arena, policy switch or counter. No new per-frame ownership layer.

State design: the prefix returns its stop reason and pending ValueTask through
locals/out parameters, not a pending-frame object on every call. Completed native
instructions remain inside that non-async loop. One existing async driver per
interpreter awaits incomplete work, routes its exception against the unchanged
instruction index/span, drains relinquished cursor users BEFORE further guest
work, then resumes the same prefix. No recursively awaiting per-suspension chain
or one retained continuation per row. Native workspace stays funded through the
real await; only terminal/yield/call boundaries finalize according to existing
rules. Cancellation outside opcode routing still stops work and awaits cleanup.

Acceptance: both public APIs, existing sync native-function optimizations, closures/
generators/type descriptors/imports, evaluation order and Python error precedence,
finally/with cleanup/exception suppression, current cancellation, terminal first
refusal, retained aliases/results and awaited disposal. Genuine asynchronous
protocols and hosts must suspend; completed operations must not be retried.
Synchronous-mode prefix must never abandon an incomplete operation; preserve its
existing synchronous-contract settlement/error behavior. Keep managed stack safety.

Proof/implementation boundary: consolidate dispatch and completion first. Reuse
existing safe member-cache policy where applicable without a new native whitelist;
dynamic descriptor/member operations keep awaited resolution. Investigate CSV
representation only if still material afterward, not another per-object ledger.
Tests focus on actual suspension/cancellation/refusal/disposal/protocols and semantic
families. No fuel/checkpoint-count tests. Compare frozen complete CSV/text/JSON/
report under BOTH APIs with unchanged controls, allocations and accounted peaks.
Keep RAM conditions and existing tooling. Retain adverse results; bounded refinement
allowed; state remaining debt at completion. Full cross-platform/stress/package
qualification at the coherent retained delivery. Async gap does not explain the
published synchronous CPython gap; overall whole-script objective stays open.
