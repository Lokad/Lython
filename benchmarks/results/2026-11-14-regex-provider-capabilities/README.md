# Optional regex adapter: dev84 capability assessment

2026-11-14, Lython-only integration assessment. Read-only producer sourceae463cb4
and immutable0.5.0-dev.84 package hashes/repository revisions are recorded in
[assessment.json](assessment.json). The current source is later than the package
candidate; do not infer package/source byte equivalence.

Three integration gaps remain: independently funded one-match work/results,
native UTF8 named-group enumeration/copy, and token-independent cursor creation
with cancellable preparation. Existing token-free cursor overloads do not supply
cancellation during preparation; current-token overloads retain the construction
token. Group lookup is distinct from enumeration. Program/cache ownership must not
be silently reused for shorter-lived operation scratch or escaping snapshots.

This inspects public package XML and current source contracts only. It does not
qualify producer semantics, interruption latency, performance or managed safety.
No sibling files, normal adapter0.4 pin, core dependency or Lython contract changes.
Completed dev80/15b248dc qualification is not repeated. Adapter-local bridges/dev
producers remain available for independent Lython development; only the affected
final released-adapter qualification waits for the precise capabilities above.

Subsequent read-only [fbf58099 source inspection](source-update-fbf58099.json)
finds the three integration files byte-identical and the precise gaps unchanged.
UTF8 group lookup/source copy is distinct from group-name enumeration; the funded
search cursor supplies no anchored/fullmatch operation. Filtering its first search
result cannot implement `fullmatch("a|ab", "ab")`. No new producer qualification
or normal dependency/pin change follows from unrelated producer improvements.

Read-only[e8b20aa5/dev89freeze source inspection](source-update-e8b20aa5.json)
again finds the entire integration project unchanged fromfbf58099. The three
precise gaps remain; no normal pin change or unchanged qualification follows.
