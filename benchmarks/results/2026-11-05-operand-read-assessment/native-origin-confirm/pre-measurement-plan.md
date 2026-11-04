# Native-origin refinement after the preserved adverse collection

2026-11-05. B231/0d6767cb completes218s/all gates: textwarm+3.96..4.13%, async
text+3.87..5.23%, stable warmoldspread0.16%; CSVslightlybetter, allocationflat,
compilecontrol-limited/slightallocationincrease. Preserve the entire original
source/measurement, not a failed-design label or an erased condition.

Concrete source difference not included in the static instruction claim: adding
tagging also changed NativeFirst/NativeSecond from raw access to >=0/clamp getters.
Native helper instruction sequences remain unchanged, but every native proof/eval
node now pays these branches. All production accessor callers are in native
guard/evaluation/formatting functions entered under the existing nonnull native-root
contract. Ordinary forwarding is mutually exclusive with a native root and emits
negative read-span tags only there. No guest value, policy or runtime validation
needs a new per-edge clamp; it also hides an invalid tag as an absent input.

Refine the getters to the established native-root invariant, using Debug.Assert
and raw return, keeping ordinary span decoding separate. No instruction/provenance
layout/forwarding changes, new backend, options, JIT hint or removed resource check.
This is a specific hypothesis, not demonstrated attribution of the4%regression.
Before retention: nonincremental Release/focused checks on both actual targets;
one existing complete textwarm comparison directly initial0d67→refinement with two
unchanged initial roles, <=60s. If evidence supports refinement, final complete
affected/compile/control pair versus qualifiedf6 is still required. Otherwise
withdraw the prototype or identify a concrete remaining cause, not a blind series.
All original/adverse conditions remain available. Full delivery only on retained
source; the whole-script performance goal remains open.
