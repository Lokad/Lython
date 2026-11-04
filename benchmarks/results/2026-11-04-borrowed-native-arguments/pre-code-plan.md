# B192 source decision — closed-helper operand calling convention

Selected after B191 current CPU evidence; committed baseline334c7cc7, frozen80e9a32d.
Whole-script goal still OPEN, fresh JSON debt provisional2-5%. Profile leaves do
not predict savings; confirmed text call/native/binding aggregate~9.5%, partial
text~11.1%. Instruction prefix17.5-19.2% remains a larger separate cost. No new
profile campaign or promise this closes the CPython gap.

Actual remaining representation, not a re-proposal of delivered frame/array work:
BeginPendingCall binds/rents/fills BoundCallArguments BEFORE TryExecuteWithoutFrame.
BindPositionalFunctionArguments rents owned values, sets presence, copies every
argument, fills defaults; ReturnBoundArguments clears/returns storage. Native
helpers already avoid ExecutionContext/interpreter allocation, but still pay this
bound-value construction. NativeReturn.cs carries the ownership-bearing bound
record recursively while only TryReadNativeOperand reads Values and Assigned.Length.
Ordinary non-native callees ALREADY reuse bound storage as locals: do not claim
that as new work, reuse caller storage as mutable locals, or replay B61/B69 arenas.

Concrete migration and removed work:
1. Give the existing native body guard/evaluator ONE ReadOnlySpan<object?> input,
   removing BoundCallArguments/ownership/presence metadata from its recursive path.
   Existing genuinely owned invocations adapt Values[..Assigned.Length] once.
   Retain compiled NativeReturn nodes and all pre-work guards; no new IR/descriptor,
   method-name whitelist, scope/cache/pool, options or speculative execution.
2. For ordinary fixed positional calls supplying every declared positional (and
   no bound receiver), use existing BindingPlan/SupportsDirectPositional metadata
   to try the SAME closed native helper before renting binding storage. Preserve
   exact module/class/closure/active-handler guards already in TryExecuteWithoutFrame.
   Caller operand storage stays owned and intact until synchronous evaluation ends.
   Do not store a span on a heap/async state or return an argument view. Returning
   an actual argument OBJECT remains legal and funded by its existing owner.
3. Route equivalent existing operand entry points through this convention so sync
   and async hosting share the same CPU path. Defaults, wrong arity, keywords/star,
   bound receivers, nested/class/closure, capturing/suspending and guard-ineligible
   bodies retain the general binder and real invocation frames. No removed Python
   behavior. Do not add separate required-count/default binding or a second binder.
4. Delete bypassed rent/copy/presence/clear/refund on eligible paths and displaced
   bound-record native signatures/adapters. Keep owned binding where it has a real
   lifetime. Existing frame/argument cleanup invariants stay consolidated.

Semantics and contracts:
- All eligibility/type/callable guards before ANY native effect. If false, ordinary
  binding may proceed; after work begins, no retry/fallback on error/refusal/cancel.
- Same target/argument evaluation order, defaults/keywords/count/type errors,
  builtin shadowing, alias identity, exception chaining/traceback spans, active
  exception restoration, managed stack/recursion admission and current host token.
- Borrowed caller storage already admitted, no redundant allowance for an array
  that is not allocated. Tiny-budget thresholds may improve; no hidden policy.
  New result/storage allocations still preflight, first refusal terminal, retained
  aliases/cache/async users funded. Awaited generic cleanup remains unchanged.
- Receiver prefix/default/protocol lookup must not be performed after exposing a
  borrow to suspension. No partial guest effect followed by generic retry.

Acceptance and bounded comparison:
Existing native-return/helper/forwarding tests, binding/default/star/keyword/arity,
closures/class/super/generators, effects/exception/cancellation/refusal and retained
alias/allowance reuse, both actual targets. Add coverage only for a real boundary:
borrowed native returns alias input; guard fails before effects; refusal inside
native work terminal; genuinely suspended general call owns inputs through cleanup.
Do not assert fuel/checkpoint locations, internal array types or exact tiny budgets.
Source acceptance: eligible ordinary helpers create no transient bound argument
storage and native evaluator has no ownership-bearing record parameter.
One both-API complete CSV/text/JSON/report/config pair plus a helper-heavy control,
short allocation diagnostic/unchanged old controls, compilation separate only if
frontend changes. Keep every adverse result; bounded confirmation/refinement when
justified, no fixed improvement veto. Full platform/package once at retained boundary.

Completion: coherent native argument transport replaced and displaced construction
removed; semantic/ownership checks pass; explicit whole-script/remaining-debt decision.
Then continue full calls/activation/operand or bulk-storage roadmap, not declare
performance complete for a helper-only win. If source proof exposes a real escaping
span/lifetime need, stop that dependent migration and state the precise tradeoff;
never grow tracing, arbitrary limits, or dormant backends to force it through.
