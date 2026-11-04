# D16 / B227 concrete next implementation

2026-11-04. Baseline production exactly ee8020f0 (full41904808069 terminalGREEN),
HEAD de77ba16 plus final evidence receipt. Original code-object/plan/function
representations restored, no D15 count tuple/borrowing constructor/inheritance/
estimate changes. New public regression passes in38focused checks per actual11/10.
Use frozen baseline38c03e5e worker with unchanged auxiliaries for comparison.
No D16 source mutation yet. PLAN.md authorizes this selected milestone.

Delete/replace, without another framework:
1. Flatten existing ExecutableFunctionBinding to actual declaration header fields:
   Name/DeclaredName/Span/Decorators/Parameters/ReturnAnnotation/TypeParameters/
   CodeObject/IsGenerator/Docstring; DefaultCount/HasAnnotations prepared once.
   Remove .Function LoweredFunctionDefinitionStatement wrapper, its Syntax.Body,
   compiled DefaultValues expression map and unused lowered-record copy.
2. InternFunction emits this header directly after preparing existing code.
   PreparedDefinitions captures/clones one header and parameter/decorator records,
   deleting its additional lowered definition/default-map clone. Keep capture
   evaluation order, postponed/generic annotations and original spans.
3. Generator constructor in executable binding consumes prepared name/parameters/
   code/scope facts, not an invented lowered definition. Genuine lowered class
   suites/functions/annotations remain supported. Two white-box constructor reads
   change from binding.Function.Parameters to binding.Parameters.
4. AttachTypeMetadataAsync's three real callers pass actual header fields:
   parameters/return annotation/type-parameter presence/span plus existing scope,
   context/mode. No new header interface/adapter or synthetic lowered wrapper.
   Preserve regular-before-posonly annotation ordering and all Python hooks.
5. Replace BuildDefaultArgumentMap, BuildDefaultArgumentMapAsync and
   MaterializeExecutableDefaultValues with one context-based completed prefix and
   one continuation only for actual pending evaluation. Remove evaluation delegates.
   Four lowered function/lambda sites plus executable definition use it. Existing
   caller working admission lasts through awaited construction/cleanup. No extra
   owner/pool/ledger, no retry after first refusal; check cancellation in loops.
6. Default construction maps have NO mutable/escaping consumer: only function
   constructors read them to create their own mutable tuple/dictionary defaults.
   Accept IReadOnlyDictionary through PyFunctionBase/PyFunction/PyExecutableFunction/
   PyGeneratorFunction/CreateLoweredFunction/CreateLambdaFunction; use existing
   FrozenDictionary.Empty for zero defaults. Keep nonempty private maps, final
   defaults/name keys and retained aliases charged. Existing literal [] constructor
   arguments in internal tests may need an explicit dictionary/read-only empty.
   Do not cache guest values or introduce global name-keyed callable/signature caches.

This removes real retained syntax/maps/clones and repeated construction/analysis.
No claim of dominant warm CPU or predicted speedup. Keep hot code-object layout and
ordinary binding paths untouched; D15 adverse results are preserved, not explained.

Before mutations archive changed baseline files/source map and PLAN. Build Release
nonincrementally, focused internal Function/CallBinding/Executable/Governed/Refusal
and public BindingPlanConstruction, FunctionDefaultMetadata, AsyncFunctionHosting,
PreparedFunctionHostLifetime, OrdinaryFunctionCall, ExecutableGeneric, Lambda,
Typing/Annotation/Decorator/Closure/Generator/ClassCell/Super/Governed/Refusal/
Cancellation families. Prefer class-qualified cases over irrelevant module suites.
Both actual targets at coherent freeze; no architecture-only assertions or relaxed
timeouts. Add external cases only for a demonstrated missing semantic boundary.

One bounded existing-tool source comparison: compile/fresh text and report/config,
warm controls; exact source/binary/aux/CoreLib/hash identities, independent full
goldens, ordinaryGC/tiering, quiet/owner baselines/finalzero and joined cleanup.
Initial warm admission is not fresh overlap/RSS/embedding latency. Separate storage,
allocation, elapsed and CPU claims. Preserve adverse/unstable controls, decide debt
explicitly. Full Windows/Ubuntu actual11/10, originalStress and installed Release
consumers at declaration delivery, not for withdrawn sources. Publication held.

After D16, continue substantial CPU roadmap; do not call one experiment the goal.
Use existing current text/report attribution, with a narrow new measurement only
for an actual missing fact. Regex stays independent; no sibling/package promotion.
