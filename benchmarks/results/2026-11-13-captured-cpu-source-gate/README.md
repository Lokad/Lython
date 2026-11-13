# Captured-text execution source gate

One warmed8s199Hz external CPU capture on committedc5d05cff/frozen3a2c1cf6,
2026-11-13UTC, quiet Azure F4as_v7/Ubuntu24.04.4, ordinary.NET11RC3 GC/tiering.
Existing workerfec79afd and identical complete captured-text script/fixture/goldens.
The only profiler override enables managed perf symbols. Full runtime delivery is
running; this observation does not independently qualify a release.

1502guest-thread leaf samples across7.58CPU seconds;146unmapped(9.72%), no ambiguous
map leaves. All original thread CPU, reset/protocol/validation boundaries and raw
stacks/maps survive. Identity, before/after full outputs, shared-budget cleanup,
quiet and joined-user gates pass. Every RAM trace is archived and SHA-verified
persistently under the VM lease before unmount; no workers/fixtures remain.
The existing persistent3a core is hard-linked, avoiding another copy or retirement.
The workload-specific batch is256, so its12s warmed execution completes within the
existing bounded warmup loop; original invoice conditions remain unchanged.

| Leaf frame | Samples | Interpretation |
| --- | ---: | --- |
| Existing native dictionary loop |274|Includes hashing, lookup, binding and useful/inlined work|
| Cased whitespace preparation |151|Includes eager admission and boundary/capacity inventory|
| Ordinary instruction prefix |113|Includes dispatch and useful inlined operations|
| Native instruction core |82|Includes closure updates and calls|
| UTF8 casing writer |66|Useful case mapping/output|
| Monitor enter/exit |86|Shared-allowance synchronization; source already has one flat root gate|

These are not removable percentages. Compiler facts disprove mirrored locals in
this script. The per-line helper has five value slots, no newly captured locals and
is already inline-eligible. Main has21slots, one captured cell and80instructions;
its once-per-script frame is not a per-line frame. Source also disproves a repeated
Unicode casing/count pass: the whitespace inventory derives conservative capacities
while finding boundaries, then maps each word once. Range loans, private integer
dictionary values and deferred totals already exist. The standalone allowance
already shares one root lock; no recursive chain of admission locks was found.

The source gate closes without a new runtime prototype. The remaining precise cost
is within the existing bulk dictionary/preparation boundary: useful lookup/casing,
instruction bookkeeping and eager storage admission are not yet separated enough
to justify removing a particular major mechanism. A later proposal must identify
actual displaced work and preserve last-word/total publication, callback reentry,
eager denial-before-effects, Unicode/final-sigma rules, cancellation and retained
aliases. Do not replay the slower whole-line casing intermediate, call arena or
call-control designs unchanged. No elapsed, allocation or CPython gain is claimed.

[CPU attribution](attribution.json), [raw evidence](observations.tgz),
[compiled facts](compiled-facts.json), [metadata probe/failed build conditions](metadata-checks.tar.xz),
[archive receipt](archive-receipt.json). Performance and independent regex work remain
active; publication remains postponed.
