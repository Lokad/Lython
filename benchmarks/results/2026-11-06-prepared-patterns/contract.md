# P01 canonical prepared match work — before code

Baseline9d40a6db/current runtime887002c2; combined delivery42131217218 active.
B305 native mapping128rows regresses+17.54/+15.30%, with all semantic/ownership
fixes retained. Source identifies repeated raw-expression frame entry/string
construction, private seen/key-value arrays and capture-name dictionaries.

Canonical design (one matcher, no alternative execution backend):
- Prepare immutable pattern nodes during existing LoweredScript lowering. Pattern
  expressions use existing lowered expressions/constants; class-name metadata,
  capture indexes, canonical/mangled stores and required working slots are prepared
  once. Runtime consumes this one representation from executable AND lowered class/
  import consumers. Retire runtime PatternSyntax dispatch/expression detours,
  canonical-binding enumeration and per-attempt name dictionaries.
- Consolidate existing string/bytes/large-integer literal ownership on lowered
  nodes, reusing executable constant pooling. Remove SharedLiteralCache CWT and
  repeated literal construction at executable compilation. Keep integer boxing
  behavior where required; payloads follow existing compilation ownership and
  execution/result borrow contracts. No new global cache/owner/identity ledger.
- Capture into indexed slots and keep values invisible to guest locals until a
  whole pattern succeeds. Preserve first-OR canonical order/private-name aliases.
  Frontend validation proves unique names and equal OR capture sets. Successful branches overwrite their own slots; failed branches never publish, so snapshots/rollback are unnecessary.
- Use already-admitted executable value storage for known pattern working/capture
  slots; lower-only callers use the existing temporary reservation mechanism.
  No new pool, arena or allowance. Clear references on every exit. Frame cached/
  pending storage remains funded by its existing owner; earlier frame denial is
  acceptable/documented. Transfer surviving captures to admitted bindings/cells
  before releasing scratch. Real async guards/bodies begin after extraction.
- Prepare property-free, immutable, unique mapping keys once. Dynamic keys retain
  length/all-keys/duplicate/get/value/subpattern phases and existing native seen set
  cleanup. Do not repeat duplicate work proven by frontend/constant facts. Preserve
  native rest copy/delete, missing errors, preflight and terminal failure propagation.
- One pattern matcher retains supported sequence/class/value/OR/AS behavior. Use
  managed nesting admission at actual recursive pattern work. No unsafe/native
  probing, new execution policies/options or silent fallback to the syntax matcher.

Expected deletions: global SharedLiteralCache/three wrappers, duplicate executable
literal preparation, executable/lowered new capture Dictionary per attempt, runtime
canonical-binding dictionary copy/enumerator, raw pattern expression execution and
common literal-key seen/key-capture construction. Syntax remains frontend evidence,
not a second runtime route. Avoid a new generic match VM or another dispatcher.

Compatibility: key/property extraction remains synchronous; guards/bodies can
suspend. Keep captures/aliases, callback order and first refusal, cleanup and cycles.
Source spans and compiler diagnostics remain Python-shaped. Do not expose partial
capture writes through locals(), descriptors, failed alternatives or guards.
Existing rare class/sequence semantics need independent Python checks, not tests
that merely preserve accidental old internals. Do not expand unrelated protocols.

Validation: existing mapping/view/key/closure/private-mangling/cancellation/managed
stack families plus focused goldens for literal reuse, dynamic keys, capture timing,
OR restoration, nested patterns, suspended bodies and pending users retaining compiled constants through awaited cleanup; compiled programs follow the documented requirement to outlive active runs. Remove architecture-only expectations made obsolete by
pre-admitted frame storage; preserve refusal-before-effects and budget-reuse coverage.

Measure bounded current/reference/unchanged-current/CPython runs using existing
workers: valid record-pattern workload plus representative controls; compilation
and compile-to-result as well as warm because preparation moves work. Retain adverse
conditions and debt. Before promotion explicitly decide remaining regression debt;
no fixed percent veto or retiming for desired signs. Full platform/stress/package
qualification batched at a coherent delivery, while independent work continues.
