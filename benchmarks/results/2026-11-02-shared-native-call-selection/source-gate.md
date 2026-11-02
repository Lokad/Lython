# B116: consolidate the actual native call boundary

Source609e5de1, no prototype. B113 full41472979014 is cached success and must
never be queried again. B114 is focused-qualified, B115 external checkpoint is
complete. No publication. Latest controlled CSV debt6–7% versus unchanged
controls remains explicit; fresh/warm CPython ratios are diagnostics, not its
causal proof. Do not start another broad profile/ownership/boxing campaign.

Actual repeated selection sites: Executable.Binding.cs around580–605;
Executable.Interpreter.Async.cs around150–180; Executable.NativeReturn.cs
around220–245. Same int/float text, native str, abs and len operations are chosen
through repeated callable/type tests. Sync/async already specialize sum(bytes);
the native-return guard still does not. Existing Func<object,span,context,object>
operations and InvokeSingleInput already own native entry/cancellation/exception
behavior. Reuse them; do not invent a universal callable framework.

Important nonidentical rule: general len specializes non-PyInstance operands;
native-return len only admits its closed sized-type list. A shared selector must
distinguish guaranteed non-suspending bodies from protocol/error-bearing general
cases without silently widening leaf eligibility or losing true suspension.
Builtin len/abs/sum are already shared immutable instances. Int/float signatures
are already static and Python type graphs remain isolated per run. Reconstructing
all immutable builtin metadata is not an unimplemented opportunity.

The return guard currently classifies values into NativeReturnShape, then
evaluation rereads operands and selects a native helper again. Pure operand
loads need no result-shape work merely to recognize a callable (B113 refinement
already removes that one). Do not blindly repeat it. Investigate one shared
actual-value/native-result proof boundary and eliminate repeated predicates and
unneeded reads, preserving whole-expression pre-work eligibility and B99 gains.

Next bounded source step: map actual callable identity/rebinding, arguments and
possible protocol work for the existing operations; select one representation
only if it removes these selectors without new argument arrays, caches, type
hierarchies, per-invocation workspace or policy flags. Count compile metadata and
read costs. Real ExecutionContext/locals/closure identity survives; no frame
fusion or B61/B69/B108 replay. If this reduces to another narrow extra matcher,
reject that implementation and choose another demonstrated material cost.

Before code, write exact deletions and one complete migration/debt decision.
Tests: native aliases/rebinding, invalid args and exact error/source order,
bool/large numeric promotion, all existing native-sized types and unknown results,
custom callbacks that genuinely suspend, cancellation in long native operations,
first admission refusal after previous work, retained arguments/results/caches
and awaited cleanup/budget reuse. Never fall back/retry after native work.
Then one bounded CSV/text/JSON/report source pair and unchanged controls; decide
the remaining CSV debt. Full platform/Stress/installed consumers at the retained
boundary, not after another isolated local edit. Performance objective stays open.
