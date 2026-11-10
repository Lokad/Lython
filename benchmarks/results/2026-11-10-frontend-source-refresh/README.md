# R212 — current frontend and invocation source gate

Assessed fully qualified runtime e543bf15 at evidence HEAD6ca794be, 2026-11-10.
No prototype, timing capture or repeated platform qualification. The name-binding
and abstract-statement visitors have **identical committed bytes** to the
[R158 source gate](../2026-11-09-frontend-flow-gate/README.md); [proof](source-gate.json).
Physical CRLF versus normalized Git hashing initially differs for one file; exact
Git content and the original physical hashes both confirm source equality.

The visitors maintain distinct maybe-assigned and resolvable-value facts. Annotation
flow additionally visits dead/deferred scopes, including real regex capability
analysis. Reuse these semantic findings and historical attribution, without claiming
current whole-compilation percentages. The [B187 failures and corrected adversity](../2026-11-03-analysis-eligibility-withdrawn/README.md)
remain relevant. One copied read-only local-name set is a small redundancy, not a
justified substantial flow redesign; no blind merger or eligibility-state revival.

Precedence climbing, shared child enumeration, scope facts, executable imports and
declaration-only function metadata are already delivered. No original function
body/syntax graph remains in ExecutableFunctionBinding. Async startup already uses
the shared ExecutionThreads pool and releases it on real suspension; do not recreate
the [delivered worker reuse](../2026-10-15-async-worker-reuse/README.md).

The subsequent R213 consumer check narrows captured-expression reconstruction to
generator declarations whose direct expressions contain yield. Ordinary annotated/
decorated definitions use DefineFunction and the shared builder directly. Removing
that rare reconstruction stage is not selected as a representative helper improvement.

The next bounded candidate is ordinary bound-helper argument construction: exact
unbound positional calls borrow existing operands, while receivers force the native
argument buffer/binder. Assess the existing complete class-renderer report and prove
borrow/restoration, native completion and ownership before editing. No new call packet,
mapping layer or alternate interpreter. The general binder keeps real dynamic cases.
This is a hypothesis, not measured CPU dominance or a predicted speedup.
