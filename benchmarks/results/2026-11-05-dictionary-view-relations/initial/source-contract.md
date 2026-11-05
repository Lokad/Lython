# E3b1 native view containment and relations — before code

Baseline353b28b1/frozen48f4690c. Four independent CPython mismatches and native
control preserved in probes.json/goldens.json/before-sync.jsonl. View equality leaks
internal unhashability for list-valued items and misses Python-equal custom keys.
Mapping rest/duplicate patterns also fail; retain these as E3b2, not silently
claimed fixed by the view milestone. GenericAlias public semantic hashes remain
real consumers; do not change global comparer/storage identity.

Select the complete native dictionary-view containment/relation pipeline:
- Remove the raw semantic equality HashSet and its private enumeration helpers.
- Replace dictionary-view membership scans/full-pair hashing with existing native
  dictionary lookup and contextual value equality. An items candidate must be a
  two-tuple; only the key needs hashing. Keep identity for membership values.
- Route ordinary equality/order and explicit relation dunders through one count/
  all-contained policy. No temporary sets; optimize items-to-items by traversing
  stored pairs, eliminating per-record PyTuple creation in this common pipeline.
- Share current dictionary key resolution; native-key/stored-custom equality and
  value comparison can suspend. Protocol-key callbacks retain documented sync-only
  restriction. Do not hold ambient thread scopes across awaits. Any ChainMap
  enumerator callbacks must run under current provenance, not a prior caller.
- Reserve source backing before callback enumeration; hold it until all actual
  users/cleanup finish even after clear. Reuse existing temporary admission and
  owners. Conservative earlier denial is documented; no new memory infrastructure.

Affected workloads: configuration/report dictionary checks, row/manifest comparison,
CSV/JSON dictionary data and ordinary keys membership. Hypothesis: eliminate
O(N) membership scans and temporary sets/tuple records; compare warm whole view
script and complete representative control on quiet VM after semantic gates.
No claim that this dominates overall CPython gap. Record source pair and existing
current control/CPython reference, adverse samples and source/refinement identities.

Acceptance: independent Python goldens for keys/items/set combinations, unhashable
values, malformed items, equality/order, explicit methods, custom/reflected key and
value effects, source mutation; focused existing families. Real suspended lookup/
value/truth after source clear keeps backing funded; native swallowed cancellation/
refusal and admission before effects remain terminal with correct cleanup/budget
reuse. Full actual11/10 Windows/Ubuntu/Stress/installed consumer checks at delivery.
Do not turn this into a generic dispatch framework or storage-backend rewrite.

## Focused source gate and discoveries

Four original mismatches preserved. The two view failures are corrected in both
APIs; mapping-rest and duplicate-pattern failures intentionally remain E3b2.
Nine independent CPython3.13.2 goldens pass; the first eight-case run exposed
native-view direct-method NotImplemented versus reflected ChainMap equality.
Retain its failure; the correction preserves original source/target callback order.
Twelve white-box regressions pass: eight actual suspended value/truth comparisons
after clearing both 32-entry sources, one suspended native-key/stored-custom-key
lookup, native swallowed-refusal/cancellation and preflight-before-effects.
Zero-warning/error Release build; focus372public/128internal per actual11/10 pass.
Removed24 duplicate relation bodies and two linear membership helpers; common
items-to-items relations allocate no item tuples or temporary sets. ChainMap
enumerators re-establish current provenance only while advancing, never over await.
Baseline353 Counter delivery42119563175 remains independently in progress; this
new view source is not covered by that run. No new storage backend or policy.
