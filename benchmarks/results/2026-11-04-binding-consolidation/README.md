# Shared ordered binding facts — B197

2026-11-04. Retain the completed consolidation: 127 net production lines removed,
one lexical binding walk and one explicit order invariant, without a new visitor
framework or runtime policy. Final focused checks pass on both actual runtimes;
full platform/stress/installed-package delivery is separate and pending. Publication
and tagging remain postponed. Whole-script performance work remains open.

Baseline runtime 2d46dc8e, qualified through test repair 5d8c7239, frozen
`ba48576af397ef62ce62a493b7ab4eb2ff845874fe44c512e6e5ceadf8e63783`.
Measured final `1b9b3230ca075b32ac8fbd3e117605f93499b9356b4afcd768c76ca4a535ca07`
is e3f83430 plus the archived ordered patch. Exact source/binary identities are
retained; later commit receipts are not substituted for measured binary identity.

## Removed work, preserved semantics

Collect directives, local bindings and annotations in one existing scope walk.
Remove the separate directive traversal, repeated expression binding visit and
the executable builder's incomplete local/assignment/pattern visitors. Function
slots consume shared explicit binding order after the unchanged argument prefix.
Modules intern needed slots through their actual stores and retain durable dynamic
namespaces. Ordered with/try/match traversal preserves header/body interleaving.

Scope facts keep their membership sets and a small immutable local-order array.
Name collection uses ordinary lists/ICollection, not a new ordered-set abstraction
or an assumption about HashSet enumeration. Deduplication freezes first occurrence.
That adds temporary construction work and retained metadata: allocation is measured
below rather than assumed to fall. Compilation's existing conservative admission
and program owner fund this metadata. No new memory allowance, owner or collector.

Optimized function snapshots read canonical frame/cell values in binding order,
then extra namespace entries. A partially mirrored match capture cannot move ahead
of all other locals or replace current cell values. Module/class dictionaries keep
their live namespace behavior. Existing general/lowered consumers remain.

## Corrections and adverse conditions retained

The first prototype pre-interned every module name. Twelve existing ownership
cases failed because definitions disappeared from the durable namespace after
frame cleanup. The correction restores module namespace semantics without broad
mirroring or a module compatibility switch; original patch/failures remain.

The next source passes 776 internal / 3,871 public focused cases per runtime, but
an additional independent check finds `z=1; a=2; list(locals())` wrongly sorted.
Its completed pre-order timing capture remains **diagnostic for that source**, not
qualification of the final design. Explicit order replaces sorting. Compound
snapshot checks then expose the pre-existing partial-mirror ordering issue, fixed
at the existing snapshot boundary. An initial contextlib-based fixture fails
explicitly because that module is unsupported; a guest context-manager protocol
replaces it without adding a module or changing the independent CPython golden.

## Final controlled comparison and decision

Five existing jobs: CSV/report compile-only, fresh config and warm CSV/report.
Two unchanged B194 roles and final source, 5-second warmups, nine rotating ~200 ms
batches, ordinary GC/tiering, quiet endpoint gates after owned cleanup settles.
All identities, full stdout/file goldens, normal closes/exit zero, balances and
joined users pass: 127.564 seconds capture / 128.171 seconds owned service.
No new CPython comparison. Same quiet Azure F4as_v7 / Ubuntu 24.04.4 / .NET 11 RC3.

| Final elapsed | Versus baseline roles | Baseline-role spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| CSV compilation | −0.68% / −0.99% | +0.31% | 0.71% |
| Report compilation | +1.43% / +0.70% | +0.73% | 2.84% |
| Config compile-run | +1.49% / +0.53% | +0.95% | 0.99% |
| CSV warm | −1.92% / −0.73% | −1.19% | 0.37% |
| Report warm | −5.77% / −4.80% | −1.02% | 3.54% |

Report warm is favorable, with baseline IQRs 2.47–3.90%; this is a bounded affected
workload observation, not a general CPU claim or an explanation of the whole
CPython gap. Compilation/fresh-config points remain close to controls and IQR.
**Possible roughly 1% report-compilation/config-fresh cost is accepted uncertainty.**
External CPU includes reset, transport, JIT/GC and coarse process-clock sampling;
do not turn its percentages into guest-only CPU proof.

Separate eight-case compilation-allocation diagnostics show **+0.61–0.92%** for
the five representative scripts, **+1.55–2.15%** for the smaller text controls.
These repeat against the unchanged allocation role. The extra explicit-order
metadata/working construction is accepted for removal of duplicate collectors and
correct ordered snapshots. No compilation allocation-reduction claim. Pre-order
allocation −0.3–0.5% describes its incomplete representation, not the final source.

**Retention:** coherent deletion and one authoritative binding/order representation
justify the measured small allocation increase. Final elapsed evidence establishes
no broad CPU improvement. Preserve all adverse/uncertain points; no further unchanged
retiming is planned. New concrete evidence may justify bounded future correction.
Continue substantive runtime work rather than treating this cleanup as completion.

## Validation and artifacts

Final source: **776 internal / 3,871 public focused Release checks per actual
.NET 11 and .NET 10**, including function/class/super/closure/generator/comprehension,
annotations, live namespaces, cancellation, terminal refusal, ownership and shared
allowance reuse. New public module/argument/repeated/with/try/match snapshot ordering
goldens are independently verified under isolated CPython 3.13.2. Existing ownership
assertions and timeouts are unchanged. One internal synthetic-scope fixture supplies
the now-explicit order; its admission/awaited cleanup assertions remain intact.

Protocol 2 includes invocation, actual mediated RAM-file I/O, full checks and prior
result disposal; fresh includes compilation/disposal. Fixture staging/reset, startup,
response hashing and final cached-owner teardown are excluded from engine clocks.
All users/fixtures are joined/removed. Compile-only excludes execution/I/O. Separate
allocation diagnostics share process GC across isolated images and are not CPU proof.
No equal-heap/RSS or embedding-latency claim. Only inactive generated obj directories
in two named old checkouts were removed for space; sources/bin/frozen evidence stay.

Pre-order capture: 151.380 seconds / 151.986 seconds service; all six catalog jobs
complete, known function-order defect preserved. Archive SHA256:
`e59d7141e9d24d0864a00cb8f32524827579f7aeb148a7dbfebd277a3932f93c`.
Final ordered archive SHA256:
`5d628e5746a3a60d565bafad7abbfc97c19587b496e72caed826f34158b4fd20`.
Both source patches, plans, summaries, raw observations and exact identities are
adjacent/in archives; focused-checks.tgz includes initial failures and final TRX.
