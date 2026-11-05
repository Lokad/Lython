# D31 bounded guard-dispatch refinement

Recorded with the source refinement before build/measurement. InitialB252135s all exactidentity/
golden/quiet/balance/join gates pass;275internal/1167public per actual11/10.
Report warm−3.19/−3.50%,async−4.44/−5.03%,fresh−3.15/−6.08%(spread3.11/IQR4.1).
Allocation flat. Compile−1.90/−1.35%,+0.03%allocation. Textcontrol+2.86/+1.42%
with1.42%unchangedspread/.47%candidateIQR: preserve as real possible regression.
Original source/raw/conditions remain in native-format-order-d31.

One refinement warranted: restore original tiny GuardNativeBody entry wrapper
for ALL native helpers. The existing generic proof's FinishFormat case delegates
to a dedicated formatted-body proof; general non-format calls no longer enter
a larger guard or perform its extra root-format test. This is a source hypothesis,
not measured inlining/CPU attribution. Keep existing ordered metadata, pure field
proof and actual read/effect order; no new flags/cache/owner/dispatcher/array.
Builder shapes and recursive append-spine execution remain removed. Existing
native eligibility/managedstack/checkpoints, exception context, firstrefusal,
currenttoken, pendingprotocols, aliases and cleanup unchanged.

Focused original275/1167families per actual11/10, then SAME five reportwarm/async/
fresh/compile and textwarmcontrol jobs, normalruntime/two unchangedroles/exactbits/
fullindependentgoldens/quiet/balances/joins, owned180s. Preserve originaladverse
and both sources; retain/debt/withdraw with explicit whole-script tradeoff. No
automatic next variant or fixed percentage veto. Current CPythonB251 measured
0997/core68a only, never substitute this prototype into its ratios. Full delivery
batched at a coherent retained milestone; publication and goal completion deferred.
