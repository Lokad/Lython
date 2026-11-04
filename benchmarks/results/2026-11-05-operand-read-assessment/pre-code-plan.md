# D18 fixed-arity operand reads, before production code

2026-11-04. Baseline qualifiedf6e51d1c/frozendf206f47; docs9e361df0, clean tracked
worktree. Existing B132 reflection census adapted locally, no guest execution.
Five complete representative sources map ordinary modules106/85/96/65/79instructions;
all four helpers already have native return roots. No new profiler/framework.

Selected coherent replacement: extend the existing adjacent value forwarding to
checked local reads and fixed-arity subscript/member/iteration/conditional consumers.
Delete its initialized-local/capture-exclusion inventory, preserving each original
read's span and left-to-right order. No forwarding across another semantic operation,
callback, control-flow boundary or argument packet. Contiguous call packets remain
where existing binders need evaluated storage; resource/cleanup operations remain.

Keep the40B instruction and existing native provenance. Its two input-origin ints
become explicit discriminated metadata:−1 absent,>=0native source-instruction index,
<=−2encoded checked-read-span constant index. Only ordinary forwarding emits the
negative span tag; native proofs retain original nonnegative links and skip this
pass. Store original span objects in the existing heterogeneous constants pool,
which already owns operation metadata. No parallel operand plan, extra instruction
fields, runtime framework, guest tagged values, cache, policy or new owner.
Native/provenance accessors must not expose checked-read tags as instruction links.
Freeze completed constants before exposing code; no mutation during execution.

The existing census predicts removal14/9/11/6/7module instructions (conservative,
already-forwarded source). Static instruction storage plus span-reference entries
falls rather than adding16bytes to every instruction (rejected pre-code shape
would grow module instructions21–28%). These counts are not CPU forecasts.
Hypothesis: eliminate a full family of guest dispatch/publication/retirement work
in ordinary aggregation/control flow, addressing the measured hot prefix. Actual
working-set capacity remains conservative/unchanged, so do not claim reduced
admitted peaks or physical retained heap without measurement.

Read/ownership contract:
- Checked reads use LoadLocal with the original span, including unbound/local,
  module fallback/builtin and captured-cell updates. No unchecked binding assumption.
- Evaluate adjacent left then right exactly once before a subscript protocol or
  callback. Retain an actual existing subscript packet across raw type getter/call
  suspension; resumption must not re-read locals changed by that getter.
- Ordinary member calls save the evaluated receiver before later argument effects.
  Iterator sources acquire their existing private operand owner before actual
  iteration; async creation/disposal stays awaited. No new lifetime or owner.
- Unary/binary/augmented operands remain held through actual pending work; failure
  retirement/native cleanup stays exact. Returns/stores/branches keep existing
  TakeOperand semantics, source spans and StoreExecutableLocal.NoInlining boundary.
- Preserve managed stack admission, active token checks in remaining loops/native
  work/host operations, first refused admission, aliases/results and budget reuse.
  No forced GC/tracing/fuel/quotas/hidden caps/unsafe or abandoned background work.

Focused Release existing operand/source-order/mutation/call/comprehension/generator/
function/class/type-subscription/pending/cancellation/refusal/ownership checks;
add only missing externally observable regressions, independently check CPython.
Before retaining: existing three-image complete-script driver, normalGC/tiering,
qualified baseline plus unchanged-role control, exact hashes/CoreLib/goldens/quiet/
baseline/final-zero/joins. Include CSV/text warm+async, configwarm and representative
compile/fresh controls, within240s; no broad framework. Retain adverse results and
explicitly decide compilation/CPU/storage debt, permit bounded evidence-led
refinement. Full Windows/Ubuntu actual11/10/original Stress/installed consumers at
a coherent retained delivery; publication/regex promotion stay postponed.
