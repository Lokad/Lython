# Iterative XML relative selection

2026-11-13. Retain frozenc16b44f0 against R328/frozen316142ab. One funded iterative
cursor replaces Select's per-path-component frontier lists; `find` stops at the
first match and `findall` builds only its final admitted guest list. Prepared UTF8
step tags replace per-candidate UTF16 tag conversion. The same cursor supports
namespace-aware descendant steps, including Python's composed order and duplicate
matches. No recursive selector, general XPath engine, cache or public option.

The complete script reads38,493B/512XML settings (1,536children plus the root), looks up64selected
values through relative paths, aggregates them and writes a Unicode report.
Independent CPython3.13.2 stdout/file/input goldens pass; this is semantic
validation, not a new CPython timing baseline. Existing file search is the control.

## Separate allocation and elapsed evidence

Local Release.NET11 process-wide allocation, two fresh processes per source in
reverse order, five64-invocation blocks after warmup:

| Complete invocation | Reference | Candidate | Allocated-byte change |
| --- | ---: | ---: | ---: |
| XML settings/report |3,553,088–3,553,256B|1,960,276–1,960,286B|−44.83%|
| Search control |147,107B|147,107B|0%|

The unchanged public-API tool includes mediated I/O, reset, stdout/file checking
and result cleanup, excluding compilation/startup/final compiled-owner cleanup.
Every execution returns its accounting to baseline and compiled-owner disposal
returns zero. Allocated bytes do not measure live heap, CPU or embedding tail latency.

Quiet F4as_v7 Ubuntu VM, ordinary.NET11 RC3 workstation GC/tiering, identical fixed
B685 workers/optional0.4adapters. Two fresh processes per source/job,12-second
full-request wall warmup budgets, five rotating one-second wall-sized batches:

| Collection | Complete duration | XML elapsed change | XML wider CPU change | Search elapsed / wider CPU change |
| --- | ---: | ---: | ---: | --- |
| B698 original |150.129s|−36.56..−35.79%|−35.22..−34.63%|+2.99..+7.08% /+3.85..+6.95%|
| B699 reversed-order confirmation |150.040s|−35.76..−35.55%|−34.62..−33.71%|−3.39..+5.08% /−2.84..+5.26%|

B699 preserves the same binaries, catalog and clocks, reversing job and process
creation order because B698's unchanged search is adverse. XML process spreads
are0.24%candidate/0.09%reference in confirmation; search is6.72%/1.44%.
The XML direction is supported; the precise magnitude is diagnostic. Search
remains unresolved regression debt, not a demonstrated win or proven harmlessness.
Retain the coherent storage/work removal and new supported syntax; stop unchanged
retiming here. Do not infer that XML changes explain search's process variability.

Warm clocks include mediated RAM-file I/O, exact stdout/file checks and prior-result
cleanup, excluding compilation, startup, fixture reset/protocol and final cache
teardown. Wider process CPU includes reset/protocol. Ranges pair process medians,
not confidence intervals. All identities/goldens/quiet/join/archive gates pass.
Observations are persistent, separate from temporary RAM fixtures; all users join,
fixtures are removed/unmounted, then archives are SHA-verified. Only byte-proved
historical VM working copies are retired; committed original evidence remains.
A read-only preflight found an already-absent historical B693 working archive;
it launched no collection and changed no VM files. The selected old manifests and
660runtime are proved independently before retirement; current316/c16images remain.

## Correctness and delivery boundary

Focused Release checks per actual.NET11/10:27internal and46public cases. Four
new public goldens independently match isolated CPython, covering descendant
composition/duplicates, namespaces, eager errors and1,024deep path selection.
Ownership checks preserve first matches under shared-budget pressure, retained
lists/aliases and cancellation/refusal during real traversal capacity growth.
Scratch cleanup changes no graph funding; namespace/result estimates remain
conservative and may reject tight allowances earlier.

The earlier R328 CI43502337222 passes Ubuntu11 and4,706internal Ubuntu10 cases,
then fails one older slice test's process-global allocation observation
(140,400Bvs131,072B). Its original failure is preserved separately. The corrected
test measures the asserted execution thread from host gate to refusal/completed
render, retaining the128KiB bound, terminal attribution, ownership and final-zero
assertions. Later diagnostic/async cleanup is outside this preflight measurement.
16focused slice/staging cases pass on both targets. Full combined platform/stress/
installed-consumer qualification is pending; publication is held.

[Sources/checks/goldens](source-checks.tar.xz), [allocation summary](allocation-summary.json),
[original raw](b698/observations.tgz) and [confirmation raw](b699/observations.tgz)
record577production inputs, exact hashes, all observations and collector commands.
The unchanged allocation tool source remains in the earlier
[word-buffer evidence](../2026-11-13-word-buffer-loan/source-checks.tar.xz).
Element.iter/itertext/clear and iterparse remain separate R320 work.
