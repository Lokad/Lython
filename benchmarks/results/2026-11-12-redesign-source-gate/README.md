# R278 redesign source gate

At qualified41bd869b, no runtime prototype is introduced. The proposed single
managed span/inline prefix, funded live-state promotion and native graph deletion
match [R268](../2026-11-12-ordered-helper-prototype/README.md). Its completed
positional refinement still regressed native search22–26% with stable controls.
Removing argument packaging did not remove recurring entry/instruction dispatch.
The proposed form is not selected; this is not a permanent ban on a different
design. No new timing or unchanged qualification is performed.

Current executable locals and operands already share one array; the binder
accepts spans and deferred contexts support incoming closure cells. These are
not new savings. The archive preserves the source/lifetime map and the private
language-only view sketch, which stopped on nullable-flow CS8602. The sketch
proves neither runtime promotion nor unsafe/ref-safety infeasibility. Stronger
evidence comes from the already completed R268 implementation.

Offline remapping of the existing [R275 configuration trace](../2026-11-12-config-report-costs/README.md)
finds lowering in33/1,213 stacks, parsing/tokenization543, executable construction
215, analysis237 and syntax/scope143. Categories overlap and include useful work;
unknown, truncated and inlined stacks prevent exact phase attribution. This does
not establish a dominant lowering cost or justify an immediate broad frontend
rewrite. Expression traversal leaves have several distinct callers, including
binding-dependent mutation analysis; caching all traversal output as shared
compiler facts would need a semantic proof. Guest-only config-fresh354samples
retain filesystem/path/kernel work and do not show type construction dominating.
No CPU improvement is inferred from these counts. Original maps/stacks remain
in R275; the derived analysis and selected thread identities are archived here.
The corrected classifier uses the declaring type, excluding frontend parameter
types in runtime method signatures. The initial config-attribution.json is kept;
config-compile-attribution.json is authoritative. The principal phase-stack counts
above are unchanged; the initial broad "other frontend" category was overstated.
