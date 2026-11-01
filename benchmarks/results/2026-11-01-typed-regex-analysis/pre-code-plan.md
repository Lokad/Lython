## B59 — priority: remove legacy regex pseudo-binding diagnostics

Demonstrated current bug: a user object/parameter named `re` with `compile`, raw
`\N{...}` text and a LOCALE-valued attribute is rejected with LA3044/LA3045 even
though no regex is used. Isolated Python succeeds; the original Release probe
records the mismatch. StaticRegexDiagnostics unconditionally rewalks statements/
expressions and maintains separate string/locale dictionaries/sets; it decides by
name spelling rather than the existing typed bindings/provider contract.

Concrete deletion/replacement, before code:
- Remove StaticRegexDiagnostics and its pass, shadow binding tables, branch clones,
  recursive walkers, syntactic regex-name/locale matching and eager argument packing.
- Keep one typed static flow. At known real regex calls use existing argument/return
  contracts. Preserve provable unsupported LOCALE/DEBUG flag diagnostics through
  resolved integer bits, including imported aliases and composed flags. Preserve
  unknown flags for runtime Python protocols. Reuse ordinary scalar facts; exact
  folding of small integer bitwise operations replaces the discarded flag ledger,
  without a general constant evaluator, cache or policy.
- Raw regex patterns are owned by provider semantics, including named escapes,
  escaped backslashes and verbose comments. Delete core LA3044's textual substring
  ban. Intentional diagnostic change: provider pattern failures follow the existing
  catchable runtime PatternError path after inconclusive static inference; capability,
  cancellation and memory refusal retain structured terminal handling. Non-raw
  Python string/f-string named escapes remain explicitly unsupported. No Unicode
  database, dialect substitution, producer change, adapter release or new owner.
- Keep effective flags/captures, current-token forwarding, native UTF-8 inputs,
  retained cache/handle cleanup, first refusal and missing-provider errors.

Acceptance: independent Python goldens for shadowed objects/parameters, escaped
patterns/comments and masked flags; real imported-module alias diagnostics and
existing static side-effect guards. A cooperating named-escape provider proves
that core forwards original bytes/metadata/current tokens rather than imposing
backend syntax; separately test provider rejection and missing capability. Preserve
existing cancellation/refusal/borrow/cleanup/budget-reuse families and semantic
adapter corpus. Update policy-only LA3044 expectations while retaining useful
independent flag/no-side-effect assertions. Focused Release both actual targets,
then established Windows/Ubuntu ordinary/original stress/package consumers at one
retained delivery. No publication or unchanged historical CI queries.

One<=45s existing private same-process comparison, current original/candidate/
identical original, same aux bits/CoreLib/hashes,128 warmups per warm case or1024
per compile case, seven rotating32 warm/4096 compile jobs,2s settle/case: complete
CSV/text compilation and complete warm CSV control. All quiet/golden/balance/cleanup
checks; >100ms batches,<=10% IQR,<=3% original-control drift. No phase subtraction or
external ratio. Report all costs/uncertainty; correctness and deletion are the
retention grounds, not an inferred speedup. A valid>5% regression requires a source
explanation before delivery; failed controls stay inconclusive, never retimed/tuned.
Stop after this bounded consolidation; do not expand it into a regex language/parser
framework or a general compiler rewrite. Call-state redesign remains separate.
