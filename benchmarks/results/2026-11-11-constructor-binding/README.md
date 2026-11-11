# R233 — shared ordinary constructor binding

2026-11-11, exact committed752d78ce versus the frozen two-line source change in
[identity](diagnostic-identity.json). **Retain the consolidation and allocation
reduction; elapsed/CPU improvement is unestablished.** Publication postponed.

Remove the constructor-only positional-call and positional-signature gates.
Eligible compiled initializers use the same `ArgumentsAreLocals` rule, general
binder and admitted frame runner as ordinary functions. Named, keyword-only and
variadic calls no longer materialize a general type-call packet or enter the
generic constructor/initializer invocation sequence. Positional calls remain on
their existing route. No new binder, dispatch framework, ABI, packet, cache, pool,
owner, native-body proof, option or public API.

The existing binder consumes borrowed operands into final owned locals before a
default-key protocol may suspend. Keep post-allocation initializer selection,
same-instance continuation after admission metadata changes, one instance,
evaluation order, descriptors/custom allocators/metaclasses, mirrored functions,
Python binding errors and `__init__`'s None result requirement. Ordinary general
binding remains necessary for real dynamic cases; none of it is dormant code.

## Correctness and ownership

The extended public constructor cases pass on the unchanged source first:
23 cases cover Python semantics plus genuinely suspending default lookups,
retained named input after global withdrawal, live replacement defaults, terminal
refusal despite suppression/finally-return, cancellation and cleanup to baseline.
Success retains its result charge until disposal. Internal admission-mutation and
pre-initializer-refusal tests also cover named construction without restart.

After the change, fresh Release solution build22.65s/zero warnings/errors and
**97 internal/150 public checks per actual.NET11/.NET10** pass (494total).
[Focused checks](focused-checks.json). No timeouts or assertions relaxed.
The existing worker independently verifies all six complete scripts and exact
file/stdout goldens under both Lython images and local CPython:
[before](semantic-cpython-before.json),[candidate](semantic-cpython-candidate.json).
These are semantic smoke checks, not timing qualification. Full platform/stress/
installed-package qualification applies to earlier752d78ce; this changed source
will join the next coherent delivery checkpoint.

## Complete-script source pair

Frozen commonfec79 worker/optional0.4/auxiliaries; only core differs. Three CSV
scripts share the same5120record/68625B fixture and goldens: ordinary positional,
named and keyword-only construction. Captured/ordinary text and JSON controls keep
their exact maintained source. Windows/.NET11RC3, normal GC/tiering,256warmups per
image and5rotated16-invocation rounds, two identical baseline ALCs in one process.
All image-isolation/golden/stable-balance/final-zero/fixture-cleanup gates pass.

Named and keyword-only CSV CLR allocation is **3.77% lower on both APIs**, matching
ordinary positional construction. Positional/text/JSON and compilation allocation
are flat within diagnostic noise. Estimated execution/root allowance peaks are
unchanged; no physical-heap or RSS claim. The generic packet was already transient.

Elapsed results are diagnostic and mixed: keyword CSV sync−10.84..−7.28%,
async−1.08..+4.59%; keyword-only sync−3.05..+2.84%, async−16.62..−10.38%.
Unchanged source controls move up to7.49% warm and9.00% compile; ordinary text is
adverse up to+4.59% sync/+3.14% async. Captured-text async improves18.58..20.43%
without a corresponding source-path change, further warning against attribution.
No CPU gain, neutrality or explained prior R232 latency debt is claimed.
[All sync](warm-sync/summary.json),[async](warm-async/summary.json),
[compilation](compile/summary.json) retain every original sample and wider CPU.

Warm envelope includes public Run/awaited RunAsync, mediated file I/O, fixture
reset, full checks and result disposal; excludes startup/compile/final caches.
Separate Run-only clocks exclude reset and post-return validation/disposal.
Compilation measures construction/disposal separately. Wider process CPU includes
the diagnostic envelope and is coarse. This is no CPython performance comparison.

Retain because two restrictions and a real packaging/invocation phase disappear,
with demonstrated lower allocation and preserved semantics, rather than applying
a fixed elapsed threshold. Quiet confirmation may address current text/constructor
latency uncertainty at the delivery boundary. The whole-script performance
objective stays open. [Preparation, exact cores/source, original catalog-version
type failure, baseline/candidate checks](preparation.tar.xz),
[verified archive identity](archive-identity.json). No alternative backend remains.
