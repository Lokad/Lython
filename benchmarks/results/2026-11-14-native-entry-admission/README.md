# Native body admission: prototype withdrawn

R369/B758, 2026-11-14 UTC. Remove the optional one-check prototype completely;
all582production inputs restore exactly to qualified3994dfda/frozenb5a6426e.
Keep the independent real low-stack native function invocation regression.
Forced nonincremental actual11/10 builds and30focused stack/cancellation/cleanup
checks per target pass after restoration. No unchanged timing or full delivery
repeat; [R368qualification](../2026-11-14-protected-helper-activation/delivery/README.md)
remains cached. Publication held; representative performance work continues.

The prototype c3bbdafd removed only NativeExecution.Run's physical stack check
after immediate synchronous StartInline.EnterFunctionCall admission. Production
caller audit finds no suspension/guest operation between them. Logical nesting,
recursion, continuation checks and managed-only policy were preserved. Before
measurement actual11/10 each59internal+155public,124isolatedCPythoncontrols and
16source/phase goldens pass, including real low-stack/native and genuine async
handler/callback unwind. No new option, counter, stack API or storage mechanism.

| Complete warm job | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | ---: | ---: | ---: |
| Protected file search | −7.91..+0.94% | −6.26..+0.37% | 6.06/2.89% |
| Protected invoice | −1.04..+2.49% | −1.92..+3.57% | 1.10/2.38% |
| Short helper | −0.59..+3.84% | −0.25..+4.20% | 2.83/1.50% |
| JSON control | −5.26..−0.40% | −4.79..+1.28% | 1.84/3.13% |

Negative means lower/faster; all independent process-median pairings, not
confidence intervals. Search's unchanged controls move6.06%; short CPU is mainly
adverse with moving controls. The experiment does not establish a dependable CPU
improvement or prove an inherent regression. No allocation/startup/embedding-
latency measurement. This small deletion removes no whole activation, argument
packaging or storage bridge. It makes body admission depend more on its caller;
the original body-local check keeps the supported managed policy explicit at its
actual CLR entry. That limited simplification does not justify further iteration
here. Contextual withdrawal, not a percentage veto, failed-design label or permanent
ban. A larger entry redesign can revisit it with owning evidence.

One four-job140.414s quiet F4as_v7/Ubuntu24.04.4/fourEPYC9V45/.NET11RC3 collection;
normal GC/tiering, fixed worker/adapter0.4/catalog. Four fresh processes/job,
five-second full-request warmups, five rotating half-second batches. Warm includes
mediated RAM I/O, full output/files and prior-result cleanup; excludes compile,
startup/reset/protocol/final cache teardown. Wider CPU includes reset/protocol.
Every original sample/control and IQR remains in the summary/raw.

All image/catalog/golden/file/budget/quiet/join/archive gates pass. Raw SHA256
`d905f038cb43e4a19bcac4cdab413623592221d7d04700ad23a4fb1cba560520`.
Bytes verify under the lease before RAM teardown; no users/fixtures remain.
Initial b9retirement preflight finds an already-absent alias and stops before
mutation. Targeted inspection confirms b9images already gone. Corrected805initial-
range image matches its committed archive; two exact DLLaliases retire only after
all path/UID/inode/link/hash proof under idle lease. Catalog is reused by hardlink;
currentb5ff2/images/catalogs/toolchains and254/252MiBfloors remain. Final free space
266,625,024B. Failed assumptions and complete custody proofs remain in the archive.

Next R370assesses the common boxed scalar-result boundary, preserving the parser,
numeric semantics and existing private storage rather than another call arena.

[All timing](timing-summary.json), [exact restoration](restoration.json),
[raw](observations.tgz), [source/binary/checks/failures/custody](prototype-checks-and-restoration.tar.xz).

Metadata correction: the archived prototype identity's `runtimeBaseCommit`
contains an inherited `1c5af…` template value. The measured reference is
`3994dfda` / `b5a6426e`, as its full DLL hash, 582-source baseline/restoration
proof and original raw worker identities establish. Original bytes remain
unchanged; this correction does not alter the timings or delivery qualification.
