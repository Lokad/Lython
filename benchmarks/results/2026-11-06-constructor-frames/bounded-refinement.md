# B342 adverse result / justified R10 refinement

B342 completed113.649s/four jobs/all gates; archive1dfa63debb65c8b47d11e9709256a62186e66ea0345421548fb85adc1efb8ca6. Preserve all original source, raw observations and adverse conditions. Class warm+1.54/+1.58%, fresh+2.17/+3.96% versus qualified933; unchangedCSV aboutflat, text+0.87/+0.97%. No speedup or allocation claim.

Source inspection identifies one unnecessary semantic phase: eligibility looked
up __init__ before admitted allocation, then selected it again after allocation.
The post-allocation lookup is authoritative. Prepare any proven sealed allocator
under the same existing metatype/JSON restrictions, then select initializer once.
Compiled init uses the runner; all other forms use the already implemented shared
init stage on the same instance. No new state, type whitelist, cache or owner.
This removes work and makes allocation-before-init selection consistent instead
of predicting eligibility from metadata that admission can change.

Add an explicit regression precondition proving initial compiled init eligibility;
the existing mutation test then proves transition to generic init without restart.
Repeat affected semantics/cancellation/ownership both actual runtimes because
no-init/default/dynamic forms now share allocation. One bounded confirmation with
the same four workloads/worker/boundaries versus933 is allowed after those pass.
Keep B342 and confirmation separate; decide residual regression debt explicitly.
Do not compound deltas, open another profiling campaign or claim whole-script
objective complete from this constructor milestone.
