# One runtime method-format operation

R419, committed**c023042d / frozen0393331a** versus committed**58eeea6f / frozen9d071eba**.
The complete1,024record/189,441B JSON-to-Markdown method-format report keeps
the original stdout/file goldens. Baseline full43771040409 is still active at capture;
its ordinary11/10/package checks pass both platforms and Ubuntu stress passes.
This does not claim final delivery of either changed source.

Exactly two production callers used the generic PyStringOps.FormatAsync executor.
One runtime operation now executes the prepared layout through existing lookup/
format kernels. Deletes that executor/file, callback/delegate captures, nullable
callback rendering fallback, redundant full-name field metadata and the native
per-field async bridge. Pure layout preparation, native spec/index helpers and
the real regex-escape caller remain. Actual root/suffix/format protocols are awaited
with the active token; builtin fields append synchronously. No second formatter,
controller, backend, policy, new ownership scheme or new dependency.

Arguments still preflight/copy and stay funded through completion. Layouts retain
the existing conservative execution/result owner, dynamic nested specs retain their
operation reservation and guest-retained spec aliases remain independently funded.
First refusal remains terminal; no retry, background work, GC or heap tracing.
Native promotion/general call packets remain explicit debt, not silently solved.

**Complete report allocation−9.41%**,5,744,806→5,204,028B/invocation. F-string control
flat(−34B). Same strengthened natural-GC driver checks stdout/files/budget baseline
after cleanup; compilation/startup excluded. Separate collections' byte counts are
not treated as exact constants. No allocation-derived CPU claim.

**B814succeeds140.958s**. Warm elapsed**1.86–4.21%lower**,CPU1.27–3.85%lower;
compile-to-result elapsed**1.10–4.24%lower**,CPU1.70–3.74%lower. Ref/candidate control
spreads warm1.62/0.83%,fresh1.92/1.33%. F-string elapsed−1.73..+0.39%,CPU−2.33..+0.60%,
controls0.85/1.30%;CSVelapsed−0.90..+0.47%,CPU−1.92..0.00%,controls0.11/1.26%.
These modest results support retention with coherent deletion and allocation savings;
moving/adverse pairings stay visible. No universal gain, zero regression or unchanged
retiming claim. This does not complete the broader performance objective.

Focused**6internal/691public each actual.NET11/10** pass after clean nonincremental
Release solution builds. Four callback-wiring tests removed; their useful conversion/
numbering/structural assertions become three public CPython-verified programs, including
escaping4096scalar expanded-spec aliases. Two callback-only lifetime tests removed;
six original held-field cases remain with warmed layouts and six genuine held mapping/
property cases now cover success, in-flight cancellation, terminal refusal, active-token
forwarding, retained storage, joined file cleanup and allowance baseline reuse.
The regex-escape golden and layout owner/refusal assertions remain.595source/build/
changed-test inputs frozen; deleted source/test paths recorded separately.

Quiet F4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 normal tiering/
workstationGC. Two processes/source,5s full-request warmup/five rotated~0.5s batches.
Warm includesRun/mediatedRAM I/O/goldens/prior-result cleanup; excludes compilation/
startup/reset/protocol/finalcache teardown. Fresh includes compile/compiled-owner
disposal; widerCPU includesreset/protocol. No standalone compile, pending-I/O,RSS or
embedding-tail comparison. No CPython refresh for this modest source change; the
[preceding checkpoint](../2026-11-15-layout-cpython-checkpoint/README.md) retains its
actual58eeea6f/9d071eba revision rather than being relabeled current HEAD.

Every identity/golden/budget/quiet/Closed/exit/join/archive gate passes; persistent
evidence hash-verifies under lease, no users/RAM remain, terminal cached forever.
The original pre-worker stage refusal missed disk headroom by~9KB; no job/data was
started/deleted there. Bounded correction retires one additional exact archived
working response. Historical55ba core's5aliases and committed B813/B801response replicas
are verified by fresh UID/path/hash/link/all-alias proofs before retirement. Canonical
evidence/current0393/9d/8afbimages remain;254/252MiB floors unchanged.

[Full platform/stress/package delivery passes](delivery/README.md); publication held. [Medians/controls](summary.json),
[allocation](allocation-summary.json),[source/image identity](identity.json),
[checks](checks.json),[raw](observations.tgz),
[source/original tests/procedures/failed staging](source-checks-and-procedure.tar.xz).
