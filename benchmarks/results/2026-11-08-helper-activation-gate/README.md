# Closed-helper preparation source gate

R87/R88,2026-11-08. No runtime prototype or improvement claim in this gate.

Offline mapping of the [R86 CSV profile](../2026-11-08-csv-helper-attribution/README.md)
finds a unique method/tier/code-size match for the8,677-byte Tier1 instruction
prefix. Of its130self samples,36map to loop fetching,24to dispatch,19to inlined
refusal/token checks and9to prologue initialization. Groups include inlined work;
they are not individual-instruction causal costs. The input-handler emitted listing
was not requested, so its73samples remain unattributed to source groups. No extra
VM run or weakened cancellation follows this analysis.

Current7796a24f/corecc6b1b15 source contrast places helper and equivalent inlined
bodies in the same main-function scope; unused helper definitions remain on both
sides. Full stdout/files independently match isolated CPython. Five cases use
three byte-identical current images under the existing short allocation driver;
all image/golden/funding/cleanup gates pass. The original four-case driver refuses
before measurement because it requires five rows; that condition/log is preserved.

Text inlining has neutral allocation and diagnostic elapsed11.13–15.96%lower.
Same-binary helper/inline spreads2.43/7.18% and sequential case order prevent a
controlled CPU claim. CSV inlining allocates1.51–1.52%more and elapsed is mixed;
its helper spread21.08% and protected continue shape also confound attribution.
The report control spread14.77% is retained. These observations concern whole
function/local/retention shape, not binder-only cost. No adverse result discarded.

The selected next source milestone consolidates existing native-body preparation:
reuse canonical receiver declarations and the frontend's return facts, removing
the duplicate method-kind inventory, enum-indexed declaration array and matching
guard/evaluation cases. Preserve existing fusion/type proof, dynamic ordinary
execution, Python behavior, current cancellation, terminal refusal and ownership.
Cover missing Boolean facts in the existing catalog rather than adding another
result-type/proof-marker catalog. No new engine, pool, owner or JIT. A separate
focused/controlled source pair must establish the resulting benefit and control
debt; this gate does not qualify a future implementation or finish the objective.

[Assessment](assessment.json) and [complete evidence](observations.tar.xz) retain
original generated-code data, full current source/fixture/golden metadata, reused
driver identity/source/binary, all samples/failures and the pre-code plan.
