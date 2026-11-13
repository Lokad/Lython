# Native XML traversal and alias-safe clear

2026-11-13. Retain refined965c7e0f against qualifiedR329/frozenc16b44f0 for
Python compatibility and bounded traversal storage, with no demonstrated CPU gain.
One funded nonrecursive cursor powers iter/itertext; a conservative subtree-depth
bound admits its fixed stack before publication. clear replaces attributes before
discarding child-reference backing, preserving old maps/text/child aliases. No new
pool, ownership subsystem, runtime policy or public option. Incremental input is separate.

Native pulls now share one synchronous path across Run/RunAsync; only user-defined
tag equality enters the constant-space async protocol loop. Both actual.NET11/10
targets pass32internal/55public focused cases, including real async equality,
in-flight cancellation, suppressed admission refusal, alias retention, pre-mutation
refusal, lazy errors, deep trees and exhausted-stack release. Full delivery passes
[43515628922](delivery/README.md): both platforms/actual11/10, original stress and
installed consumers;578inputs/16summaries cached. New dictionary-boundary work is
outside this qualification; publication is held.

B701 repeats the original catalog/clocks on refined965, completing151.668s/all gates:

| Refined result | Allocated-byte change | Elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Aggregation: iter versus findall |−0.41%|−1.05..+2.57%|−0.36..+1.98%|
| Unchanged XML settings |+0.62..+0.63%|−2.16..−0.46%|−2.18..−0.75%|

Aggregation process spreads1.49%candidate/2.09%reference keep the apparent small
difference uncertain; settings candidate spread1.69% also limits precision. The
initial8–10%adverse direction is no longer established, not proof of a universal
improvement. Accept small unresolved aggregation CPU debt and the8B/element depth
metadata cost. No unchanged retiming or further field-packing work at this checkpoint.
Native traversal succeeds with4KiBspare where materialized selection needs capacity
for every match; the0.4%whole-invocation allocation reduction is separate and modest.
Original/adverse conditions and discarded narrowing remain below.

[Final source/checks](native-source-checks.tar.xz), [allocated bytes](native-allocation-summary.json),
[B701raw](b701/observations.tgz) record the578source inputs and exact measured images.

## Original and discarded forms

2026-11-13. Initial frozen53f3eb87 supports iter/itertext/alias-safe clear with a
funded fixed traversal stack. Reference is qualifiedR329/frozenc16b44f0. Complete
aggregation intentionally changes root.findall('.//row') to root.iter('row');
inputs, stdout/report bytes and stockCPython3.13.2 semantic goldens agree. The
unchanged XML settings script exposes parser-metadata cost. No external timing
comparison or whole-runtime improvement is claimed.

B700 on the quietF4as_v7 VM completes two jobs in151.952s, all identity/golden/
quiet/join/archive gates pass. Four fresh Lython processes per job,12-second
full-request wall warmup budgets and five rotating1s wall-sized batches. Guest
clocks include mediated RAM-file I/O, exact output/file checks and prior-result
cleanup; compile/startup/reset/protocol/final-cache cleanup are excluded. Wider
process CPU includes reset/protocol. Ranges pair process medians, not confidence
intervals. Observations stay persistent separately from RAM fixtures; users join
and fixtures are removed/unmounted before persistent archives are SHA-verified.

| Initial result | Allocated-byte change | Elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Aggregation, native iter versus materialized findall |−0.41%|+7.88..+9.86%|+6.60..+9.51%|
| Unchanged XML settings |+0.63%|−2.78..+0.67%|−3.03..+1.10%|

Settings allocation rises about12,288B/1,537elements, consistent with8B/object
for the depth bound. Narrowed1d15eb44 (ushort instead of int) still adds0.63%;
withdraw it and its casts. Do not pursue unsafe layout or a field-packing framework.
Both original and narrowed allocations use two fresh local.NET11 processes per
source, five64-invocation blocks after warmup, unchanged public-API tool, complete
goldens and per-run/final budget-baseline checks. Compilation/startup/final compiled
cleanup are excluded; these are allocated bytes, not live heap or latency.

The initial native pull unconditionally enters an async state machine. Development
now separates the shared synchronous native path from the constant-space async
user-equality loop. Its final timing and delivery are not established by B700.
The source snapshots and all adverse evidence remain; they are prototypes, not
full qualification. Focused checks cover graph/clear aliases, admission before
mutation, actual async tag equality, in-flight cancellation, terminal refusal,
budget reuse, lazy errors, text/order and deep trees. Six new public goldens match
isolated CPython. The first test compilation's target-typed-options ambiguity is
preserved; explicit LythonRunOptions fixes it without a runtime API change.

[Original source/checks](first-source-checks.tar.xz),
[narrowing source/checks](narrowed-source-checks.tar.xz),
[B700raw](b700/observations.tgz) preserve exact578production inputs and identities.
The retirement preflight initially rejected an unlisted second hard link for the
old ZIP manifest. It changed no VM files; both aliases were then independently
proved before retirement. Committed original evidence/currentc16images remain.
Incremental parsing remains separate. Publication is postponed.
