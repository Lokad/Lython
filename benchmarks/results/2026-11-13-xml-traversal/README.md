# Native XML traversal: initial evidence

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
