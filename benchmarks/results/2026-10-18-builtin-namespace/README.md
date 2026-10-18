# Module bindings and builtin fallback, 2026-10-18 UTC

Module namespaces now keep explicit bindings and use the existing per-run builtin
table as a fallback. Deleting a shadowed builtin works before and after `globals()`;
deleting a builtin-only name raises NameError. Three failing pure programs per API
are corrected. The existing explicit LA3146 local-read-before-store rejection is
unchanged and is not counted as a fixed runtime bug.

The [six-file runtime patch](candidate.patch) removes the duplicate builtin table,
three alias-set/capacity/charge fields, two alias-recording helpers and their hooks,
builtin identity scans/filtering and reinsertion after deletion. Ordinary name,
global and executable-module-slot lookup use the existing canonical inventory as
their final fallback. Function-local/nonlocal rules and mutable per-run builtin/
type/module/host state remain. There is no namespace overlay, new cache, owner,
binder, execution mode or reclamation mechanism.

The baseline is `0db9eed7908cad3b7a747f120d5b0b40c60d1f8d`; the candidate is that
revision plus the frozen [patch](candidate.patch), not an unspecified later HEAD.
The patch also includes permanent public deletion/fallback regressions and white
fixture updates. [Scope and contract](scope.json) were recorded before admission
and source changes.

| Warm context allocation | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| Fresh root | 19,176 B | 16,240 B | All 32 samples agree; 2,936 B less |
| Child context | 264 B | 240 B | All 32 samples agree; 24 B less |

One fixed before/after pair reused the existing context diagnostic, with two warmup
contexts and 32 measured contexts per route. Mutable int/float/object wrappers are
distinct across root executions, shared with children; exception aliases and
cleanup to zero allowance are preserved. The [diagnostic](diagnostic.cs),
[baseline raw results](baseline-native.trx), [candidate raw results](candidate-native.trx)
and [comparison](comparison.json) preserve the only allocation capture pair.

This is local Windows x64 build 26200 on an Intel Core i7-14700KF, 28 logical
processors, .NET SDK 10.0.300-preview.0.26177.108/runtime 10.0.13. The unchanged
configuration captured earlier that morning is retained in [environment](environment.json).
Compilation, guest execution, public projection, scheduling and process startup
are outside the allocation boundary. There is no elapsed, compile-to-result,
whole-job, embedding-latency, RSS or CPython speedup claim and no repeated capture.
CPython remains the primary external timing baseline in [BENCHMARKS](../../../BENCHMARKS.md).

All 1,491 distinct final focused Release cases pass:268 source-linked,220 namespace/
scope/import/type-alias/isolation,847 builtin,107 ownership/admission and62 complete-
job/cancellation selections, with overlaps deduplicated. These include58 complete
CSV/text/JSON/file jobs. Native callback suspension/cancellation, shared allowance,
retained results/caches, first refusal and cleanup remain covered. Source-linked
fixtures now read canonical builtins from their actual inventory. A denial fixture
requests genuinely new binding storage; two new checks prove an existing admitted
binding can be replaced at a full allowance without allocating another binding.
The original Stress coverage, its assertions and its timeouts are unchanged.

[Public probes](probes.json) preserve all six programs before/after through both
APIs and isolated CPython3.13.2. All five supported programs per API match afterward.
The CLI returns1 because the sixth program retains its explicit LA3146 subset
rejection, not because a supported case fails. See [validation](validation.json).

Retained module estimates now count explicit aliases even when their values are
canonical builtins. This conservative change can affect small admission thresholds;
identical thresholds are not a contract. Existing namespace capacity can be reused.
New names/capacity still admit before mutation, retained values stay funded, and
owner cleanup remains independent of CLR collection. SPEC.md and MEMORY.md state
these contracts. Regex migration remains planning only; core dependencies are
unchanged. Platform/package qualification is recorded separately after publication.

Stop this migration at module/builtin separation; no general namespace framework,
factory inventory, VM or ownership redesign follows from these measurements.
